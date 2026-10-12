using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task ZodRefinementHook_GivenImplementation_GeneratesInvocationAndDeclaration(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public partial class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					partial void OnZodValidate(RefineCtx<Paging> context)
					{
						if (context.Value.DefaultPageSize > 100)
							context.AddIssue("custom", "Too big.", [nameof(DefaultPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("PagingSchema");

		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"global::ZodSharp.Schemas.RefineCtx<global::Testing.Paging> refineContext = new(value, EmptyPath)"
			);
		await Assert.That(generated).ContainsGeneratedCode("InvokeZodRefinementHook(value, refineContext)");
		await Assert.That(generated).ContainsGeneratedCode("refineContext.HasIssues");
		await Assert.That(generated).ContainsGeneratedCode("AddError(ref errors, refinementIssue)");

		var hooks = driverResult.GetSource("PagingSchema.Hooks.g.cs", HintNameMatchMode.Suffix);
		await Assert.That(hooks).Contains("partial class Paging");
		await Assert.That(hooks).Contains("partial void OnZodValidate");
		await Assert.That(hooks).Contains("RefineCtx<global::Testing.Paging>");
		await Assert.That(hooks).Contains("InvokeZodRefinementHook");
	}

	[Test]
	public async Task ZodRefinementHook_GivenNoImplementation_DoesNotDeclareOrInvoke(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public partial class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("PagingSchema");

		// The validator neither allocates a context nor invokes the hook when no body is supplied.
		await Assert.That(generated).DoesNotContain("OnZodValidate", StringComparison.Ordinal);
		await Assert.That(generated).DoesNotContain("refineContext", StringComparison.Ordinal);
	}

	[Test]
	public async Task ZodRefinementHook_GivenNonPartialTarget_DoesNotEmitDeclaration(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);

		var emitted = driverResult.AllSyntaxTrees.Select(static tree => tree.GetText().ToString());
		await Assert
			.That(emitted.Any(text => text.Contains("partial void OnZodValidate", StringComparison.Ordinal)))
			.IsFalse();
	}

	[Test]
	public async Task ZodRefinementHook_Runtime_CrossFieldErrorsMergeWithSchemaErrors(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public partial class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					partial void OnZodValidate(RefineCtx<Paging> context)
					{
						if (context.Value.DefaultPageSize > context.Value.MaxPageSize)
							context.AddIssue(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Paging")!;
		var schemaType = assembly.GetType("Testing.PagingSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("DefaultPageSize")!.SetValue(instance, 1000);
		modelType.GetProperty("MaxPageSize")!.SetValue(instance, 500);

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("custom");
		await Assert.That(errors[0].Path.Length).IsEqualTo(2);
		await Assert.That(errors[0].Path[0]).IsEqualTo("DefaultPageSize");
		await Assert.That(errors[0].Path[1]).IsEqualTo("MaxPageSize");
	}

	[Test]
	public async Task ZodRefinementHook_Runtime_ValidValueReturnsSuccess(CancellationToken cancellationToken)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public partial class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					partial void OnZodValidate(RefineCtx<Paging> context)
					{
						if (context.Value.DefaultPageSize > context.Value.MaxPageSize)
							context.AddIssue(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Paging")!;
		var schemaType = assembly.GetType("Testing.PagingSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("DefaultPageSize")!.SetValue(instance, 1);
		modelType.GetProperty("MaxPageSize")!.SetValue(instance, 500);

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsTrue();
	}
}

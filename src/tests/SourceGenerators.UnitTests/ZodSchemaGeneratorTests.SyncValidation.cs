using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task SyncValidation_GivenParameterlessMethod_GeneratesRefinementCall(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, 100)]
					public int DefaultPageSize { get; set; } = 50;

					public IEnumerable<ValidationError> Validate()
					{
						if (DefaultPageSize > 100)
							yield return new ValidationError("custom", "Too big.", [nameof(DefaultPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("PagingSchema");

		await Assert.That(generated).ContainsGeneratedCode("var refinementErrors = value.Validate();");
		await Assert.That(generated).ContainsGeneratedCode("foreach (var refinementError in refinementErrors)");
		await Assert.That(generated).ContainsGeneratedCode("AddError(ref errors, refinementError)");
	}

	[Test]
	public async Task SyncValidation_GivenRefineContextMethod_GeneratesContextCreation(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, 100)]
					public int DefaultPageSize { get; set; } = 50;

					public IEnumerable<ValidationError> Validate(RefineCtx<Paging> ctx)
					{
						if (ctx.Value.DefaultPageSize > 100)
							yield return new ValidationError("custom", "Too big.", [nameof(DefaultPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("PagingSchema");

		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"global::ZodSharp.Schemas.RefineCtx<global::Testing.Paging> refineCtx = new(value, EmptyPath)"
			);
		await Assert.That(generated).ContainsGeneratedCode("var refinementErrors = value.Validate(refineCtx);");
	}

	[Test]
	public async Task SyncValidation_GivenOverriddenMethodName_UsesConfiguredName(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema(RefinementMethodName = nameof(ValidateRules))]
				public class RulesModel
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> ValidateRules()
					{
						yield break;
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("RulesModelSchema");

		await Assert.That(generated).ContainsGeneratedCode("var refinementErrors = value.ValidateRules();");
	}

	[Test]
	public async Task SyncValidation_Runtime_CrossFieldErrorsMergeWithSchemaErrors(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					public IEnumerable<ValidationError> Validate()
					{
						if (DefaultPageSize > MaxPageSize)
							yield return new ValidationError(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(
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
	public async Task SyncValidation_Runtime_ValidValueReturnsSuccess(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					public IEnumerable<ValidationError> Validate()
					{
						if (DefaultPageSize > MaxPageSize)
							yield return new ValidationError(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(
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

	[Test]
	public async Task SyncValidation_Runtime_RefineContextVariantSurfacesErrors(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					public IEnumerable<ValidationError> Validate(RefineCtx<Paging> ctx)
					{
						if (ctx.Value.DefaultPageSize > ctx.Value.MaxPageSize)
							yield return new ValidationError(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(
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
	}

	[Test]
	public async Task SyncValidation_Runtime_AsyncPathIncludesRefinement(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Paging
				{
					[Range(1, int.MaxValue)]
					public int DefaultPageSize { get; set; } = 50;

					[Range(1, int.MaxValue)]
					public int MaxPageSize { get; set; } = 200;

					public IEnumerable<ValidationError> Validate()
					{
						if (DefaultPageSize > MaxPageSize)
							yield return new ValidationError(
								"custom",
								"DefaultPageSize must be less than or equal to MaxPageSize.",
								[nameof(DefaultPageSize), nameof(MaxPageSize)]);
					}
				}
			}
			""";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Paging")!;
		var validatorType = assembly.GetType("Testing.PagingSchemaValidator")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("DefaultPageSize")!.SetValue(instance, 1000);
		modelType.GetProperty("MaxPageSize")!.SetValue(instance, 500);

		dynamic validator = Activator.CreateInstance(validatorType)!;
		dynamic dynInstance = instance;
		dynamic task = validator.ValidateAsync(dynInstance, CancellationToken.None);
		var result = await task;
		dynamic dynResult = result;
		var errors = (ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That((bool)dynResult.IsSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("custom");
	}
}

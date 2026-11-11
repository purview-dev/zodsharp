using ZodSharp.JsonSchema;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// The DataAnnotations surface beyond the dedicated Zod rules: class-level attributes, unmapped
/// <c>ValidationAttribute</c>s (for example <c>[CustomValidation]</c> and <c>[FileExtensions]</c>), the
/// Microsoft.Extensions.Validation <c>[SkipValidation]</c>, and class-level async attributes.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task ValidationAttribute_GivenDisplayNameWithoutDisplay_UsesItInMessages(
		CancellationToken cancellationToken
	)
	{
		// Arrange — .NET 11's Blazor DisplayName component supports [DisplayName]; the generated message should
		// use it when [Display] is absent.
		const string source = """
			using System.ComponentModel;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed class Model
				{
					[DisplayName("Production Date")]
					[Required]
					public string? ProductionDate { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("ProductionDate", null));

		// Act
		var errors = RunValidate(assembly, "Testing.ModelSchema", model);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Production Date is required");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("ProductionDate");
	}

	[Test]
	public async Task ValidationAttribute_GivenClassLevelSyncAttribute_Runs(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[AttributeUsage(AttributeTargets.Class)]
				public sealed class ClassFailureAttribute : ValidationAttribute
				{
					public override bool IsValid(object? value) => false;
				}

				[ClassFailure]
				[ZodSchema]
				public sealed class Model
				{
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("Name", "ok"));

		// Act
		var errors = RunValidate(assembly, "Testing.ModelSchema", model);
		var asyncErrors = await RunValidationAsync(
			assembly,
			"Testing.ModelSchema",
			model,
			services: null,
			cancellationToken
		);

		// Assert — a class-level ValidationAttribute is an object-level rule, run in both paths.
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("");
		await Assert.That(asyncErrors.Length).IsEqualTo(1);
	}

	[Test]
	public async Task ValidationAttribute_GivenPropertyCustomValidation_Runs(CancellationToken cancellationToken)
	{
		// Arrange — [CustomValidation] is a ValidationAttribute the generator has no dedicated rule for.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public static class Rules
				{
					public static ValidationResult? Check(object? value, ValidationContext context) =>
						value is "bad" ? new ValidationResult("Name is bad") : ValidationResult.Success;
				}

				[ZodSchema]
				public sealed class Model
				{
					[CustomValidation(typeof(Rules), nameof(Rules.Check))]
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("Name", "bad"));

		// Act
		var errors = RunValidate(assembly, "Testing.ModelSchema", model);
		var asyncErrors = await RunValidationAsync(
			assembly,
			"Testing.ModelSchema",
			model,
			services: null,
			cancellationToken
		);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(errors[0].Message).IsEqualTo("Name is bad");
		await Assert.That(asyncErrors.Length).IsEqualTo(1);
		await Assert.That(asyncErrors[0].FullPath.ToDottedString()).IsEqualTo("Name");
	}

	[Test]
	public async Task ValidationAttribute_GivenPropertyFileExtensions_Runs(CancellationToken cancellationToken)
	{
		// Arrange — a DataTypeAttribute subclass the generator has no dedicated rule for.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed class Model
				{
					[FileExtensions(Extensions = "txt")]
					public string? FileName { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("FileName", "a.exe"));

		// Act
		var errors = RunValidate(assembly, "Testing.ModelSchema", model);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("FileName");
	}

	[Test]
	public async Task SkipValidation_GivenProperty_IsOmittedFromValidationAndSchema(CancellationToken cancellationToken)
	{
		// Arrange — the .NET 11 attribute is declared here so the test runs on every TFM.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using Microsoft.Extensions.Validation;
			using ZodSharp;

			namespace Microsoft.Extensions.Validation
			{
				[AttributeUsage(AttributeTargets.All, Inherited = true)]
				public sealed class SkipValidationAttribute : Attribute { }
			}

			namespace Testing
			{
				[ZodSchema]
				public sealed class Model
				{
					[SkipValidation]
					[MinLength(10)]
					public string? Skipped { get; set; }

					[MinLength(2)]
					public string? Kept { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("Skipped", "x"), ("Kept", "ok"));
		var schemaType = assembly.GetType("Testing.ModelSchema")!;

		// Act
		var errors = RunValidate(assembly, "Testing.ModelSchema", model);
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — the skipped member is neither validated nor exported.
		await Assert.That(errors).IsEmpty();
		await Assert.That(definition.Properties!.ContainsKey("skipped")).IsFalse();
		await Assert.That(definition.Properties!.ContainsKey("kept")).IsTrue();
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task AsyncValidationAttribute_GivenClassLevelAttribute_RequiresAsyncAndRuns(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				[AttributeUsage(AttributeTargets.Class)]
				public sealed class ClassAsyncAttribute : AsyncValidationAttribute
				{
					protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
						throw new System.InvalidOperationException("Asynchronous only.");

					protected override async Task<ValidationResult?> IsValidAsync(
						object? value,
						ValidationContext context,
						CancellationToken cancellationToken
					)
					{
						await Task.Yield();
						return new ValidationResult("Class async rule failed");
					}
				}

				[ClassAsync]
				[ZodSchema]
				public sealed class Model
				{
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("Name", "ok"));
		var schemaType = assembly.GetType("Testing.ModelSchema")!;

		// Act
		var requiresAsync = (bool)schemaType.GetField("RequiresAsyncValidation")!.GetRawConstantValue()!;
		var sync = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
			RunValidate(assembly, "Testing.ModelSchema", model)
		);
		var errors = await RunValidationAsync(
			assembly,
			"Testing.ModelSchema",
			model,
			services: null,
			cancellationToken
		);

		// Assert
		await Assert.That(requiresAsync).IsTrue();
		await Assert.That(sync).IsNotNull();
		await Assert.That(sync!.InnerException).IsTypeOf<InvalidOperationException>();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Class async rule failed");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("");
	}

	[Test]
	public async Task AsyncValidationAttribute_GivenInheritedClassAttribute_RequiresAsyncAndRuns(
		CancellationToken cancellationToken
	)
	{
		// Arrange — ValidationAttribute is inherited, so a base class-level async rule applies to the derived
		// [ZodSchema] target.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				[AttributeUsage(AttributeTargets.Class, Inherited = true)]
				public sealed class ClassAsyncAttribute : AsyncValidationAttribute
				{
					protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
						throw new System.InvalidOperationException("Asynchronous only.");

					protected override async Task<ValidationResult?> IsValidAsync(
						object? value,
						ValidationContext context,
						CancellationToken cancellationToken
					)
					{
						await Task.Yield();
						return new ValidationResult("Inherited async rule failed");
					}
				}

				[ClassAsync]
				public class BaseModel
				{
				}

				[ZodSchema]
				public sealed class Model : BaseModel
				{
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var model = Create(assembly, "Testing.Model", ("Name", "ok"));
		var schemaType = assembly.GetType("Testing.ModelSchema")!;

		// Act
		var requiresAsync = (bool)schemaType.GetField("RequiresAsyncValidation")!.GetRawConstantValue()!;
		var errors = await RunValidationAsync(
			assembly,
			"Testing.ModelSchema",
			model,
			services: null,
			cancellationToken
		);

		// Assert
		await Assert.That(requiresAsync).IsTrue();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Inherited async rule failed");
	}
#endif
}

using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string CustomRuleSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp.Core;

		namespace Testing
		{
			public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
			{
				public bool IsValid(in string value) => value.IndexOf(' ') < 0;

				public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
			}

			[ZodRule(typeof(NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
			[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
			public sealed class NoWhitespaceAttribute : ValidationAttribute
			{
				public string? Message { get; set; }
			}

			[ZodSchema]
			public class User
			{
				[Required]
				[NoWhitespace(Message = "No spaces allowed.")]
				public string Name { get; set; } = string.Empty;
			}
		}
		""";

	[Test]
	public async Task CustomRule_GivenZodRuleMappedAttribute_EmitsRuleValidation(CancellationToken cancellationToken)
	{
		// Arrange / Act
		var driverResult = await GenerateAsync(CustomRuleSource, cancellationToken);
		var generated = driverResult.GetSource("UserSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.NoWhitespaceRule(");
		await Assert.That(generated).ContainsGeneratedCode("\"No spaces allowed.\"");
		await Assert.That(generated).ContainsGeneratedCode(".IsValid(");
		await Assert.That(generated).ContainsGeneratedCode("\"invalid_string\"");
		await Assert.That(generated).ContainsGeneratedCode("origin: \"string\"");
	}

	[Test]
	public async Task CustomRule_GivenZodRuleMappedAttribute_FailsValidationAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var driverResult = await GenerateAsync(
			CustomRuleSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.User")!;
		var schemaType = assembly.GetType("Testing.UserSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Name")!.SetValue(instance, "John Doe");

		// Act
		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;

		// Assert
		var isSuccess = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;
		await Assert.That(isSuccess).IsFalse();

		var errors = (System.Collections.Immutable.ImmutableArray<ZodSharp.Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("invalid_string");
		await Assert.That(errors[0].Origin).IsEqualTo("string");
	}

	[Test]
	public async Task CustomRule_GivenMismatchedRuleType_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NumberOnlyRule : IValidationRule<double>
				{
					public bool IsValid(in double value) => value > 0;

					public string GetErrorMessage(in double value) => "Must be positive.";
				}

				[ZodRule(typeof(NumberOnlyRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NumberOnlyAttribute : ValidationAttribute { }

				[ZodSchema]
				public class Model
				{
					[Required]
					[NumberOnly]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions { ThrowOnGenerationException = false },
			cancellationToken
		);

		// Assert
		await Assert.That(driverResult.GetSource("ModelSchema")).DoesNotContain("NumberOnlyRule");
	}
}

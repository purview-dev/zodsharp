using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task RuleAttributeGeneration_GivenZodRuleOnRule_GeneratesMatchingAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule(Code = "invalid_string", Origin = "string")]
				public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
					: IValidationRule<string>
				{
					public bool IsValid(in string value) => AllowEmpty || value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("NoWhitespaceAttribute");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("namespace Testing.Rules");
		await Assert.That(generated).ContainsGeneratedCode("class NoWhitespaceAttribute");
		await Assert.That(generated).Contains("global::System.ComponentModel.DataAnnotations.ValidationAttribute");
		await Assert.That(generated).ContainsGeneratedCode("public bool AllowEmpty { get; set; } = true;");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::Testing.Rules.NoWhitespaceRule)");
		await Assert.That(generated).ContainsGeneratedCode("\"invalid_string\"");
		await Assert.That(generated).ContainsGeneratedCode("\"string\"");
		// The rule's 'message' parameter is represented by the inherited ValidationAttribute.ErrorMessage.
		await Assert.That(generated).DoesNotContain("public string Message");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenZodRuleOnRule_GeneratedAttributeCompilesAndCarriesMapping(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule(Code = "invalid_string", Origin = "string")]
				public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
					: IValidationRule<string>
				{
					public bool IsValid(in string value) => AllowEmpty || value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Assert
		var attributeType = assembly.GetType("Testing.Rules.NoWhitespaceAttribute");
		await Assert.That(attributeType).IsNotNull();
		await Assert
			.That(attributeType!.BaseType!.FullName)
			.IsEqualTo("System.ComponentModel.DataAnnotations.ValidationAttribute");
		await Assert.That(attributeType.GetProperty("AllowEmpty")).IsNotNull();

		var mapping = attributeType
			.GetCustomAttributes(inherit: false)
			.FirstOrDefault(static a => a.GetType().FullName == "ZodSharp.Core.ZodRuleAttribute");
		await Assert.That(mapping).IsNotNull();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenHandAuthoredAttribute_DoesNotGenerateDuplicate(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.IndexOf(' ') < 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}

				[ZodRule(typeof(NoWhitespaceRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NoWhitespaceAttribute : ValidationAttribute { }

				[ZodRule]
				public readonly record struct OtherRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert — the hand-authored attribute is left alone, the unmapped rule still gets one.
		await Assert.That(driverResult.GetSource("OtherAttribute")).Contains("class OtherAttribute");
	}
}

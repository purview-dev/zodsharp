using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Coverage for the rules the JSON Schema exporter now maps explicitly, and for the unsupported-rule
/// marker that replaces the silent empty fallback.
/// </summary>
public class ToJsonSchemaRuleMappingTests
{
	[Test]
	public async Task ToJsonSchema_GivenNonSentinelString_ExportsNonWhitespaceConstraint(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().NonSentinel());

		// Assert
		await Assert.That(jsonSchema.Type).IsEqualTo("string");
		await Assert.That(jsonSchema.MinLength).IsEqualTo(1);
		await Assert.That(jsonSchema.Pattern).IsEqualTo("\\S");
	}

	[Test]
	public async Task ToJsonSchema_GivenNullOrNonWhiteSpace_ExportsNonWhitespaceConstraint(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().AddRule(new NullOrNonWhiteSpaceRule()));

		// Assert
		await Assert.That(jsonSchema.MinLength).IsEqualTo(1);
		await Assert.That(jsonSchema.Pattern).IsEqualTo("\\S");
	}

	[Test]
	public async Task Apply_GivenNonSentinelGuid_ExportsUuidPatternThatRejectsTheEmptyGuid(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — a Guid scalar is validated through its underlying value, so the rule is applied to the
		// string schema the generator builds rather than added to a ZodString.
		JsonSchemaDefinition schema = new() { Type = "string", Format = "uuid" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<Guid>());

		// Assert
		await Assert.That(schema.Pattern).IsNotNull();
		await Assert.That(schema.Pattern!).Contains("00000000-0000-0000-0000-000000000000");
	}

	[Test]
	public async Task ToJsonSchema_GivenNativeEnum_ExportsTheDefinedValues(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.Enum<SampleStatus>());

		// Assert
		await Assert.That(jsonSchema.Enum).IsNotNull();
		await Assert.That(jsonSchema.Enum!).Count().IsEqualTo(3);
		await Assert.That(jsonSchema.Enum!).Contains(0);
		await Assert.That(jsonSchema.Enum!).Contains(1);
		await Assert.That(jsonSchema.Enum!).Contains(2);
	}

	[Test]
	public async Task Apply_GivenRuleWithoutJsonSchemaMapping_ReportsItAsUnsupported(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string" };

		// Act
		JsonSchemaRules.Apply(schema, new UnmappedRule());

		// Assert
		await Assert.That(schema.UnsupportedRules).IsNotNull();
		await Assert.That(schema.UnsupportedRules!).Contains(nameof(UnmappedRule));
	}

	[Test]
	public async Task EnumSchema_GivenDisallowedMember_ExcludesIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var schema = JsonSchemaRules.EnumSchema<SampleStatus>(SampleStatus.Disabled);

		// Assert
		await Assert.That(schema.Enum).IsNotNull();
		await Assert.That(schema.Enum!).DoesNotContain(2);
	}
}

enum SampleStatus
{
	Active = 0,
	Inactive = 1,
	Disabled = 2,
}

/// <summary>A rule with no JSON Schema mapping, standing in for a consumer rule that has not opted in.</summary>
readonly record struct UnmappedRule : IValidationRule<string>, IZodRule
{
	public bool IsValid(in string value) => true;

	public string GetErrorMessage(in string value) => "unmapped";

	string? IZodRule.Code => null;

	string? IZodRule.Origin => null;
}

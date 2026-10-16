using System.Text.RegularExpressions;
using ZodSharp.Rules;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Coverage for Guid/UUID export semantics. A C# <see cref="Guid"/> accepts any 128-bit value, so it must
/// not be exported as Zod's RFC UUID format; a UUID rule must keep its version; and two rules that both
/// constrain <c>pattern</c> must not overwrite one another.
/// </summary>
public class ToJsonSchemaGuidTests
{
	const string NonRfcGuid = "9f41585f-31c5-fc4b-0d04-b1fe5b523139";

	[Test]
	public async Task ToJsonSchema_GivenGuidString_ExportsCanonicalPatternWithoutUuidFormat(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().Guid());

		// Assert — no `format`, so a consumer does not apply its RFC UUID validator.
		await Assert.That(jsonSchema.Type).IsEqualTo("string");
		await Assert.That(jsonSchema.Format).IsNull();
		await Assert.That(jsonSchema.Pattern).IsEqualTo(GuidRule.JsonSchemaPattern);
	}

	[Test]
	public async Task ToJsonSchema_GivenGuidString_AcceptsNonRfcAndRejectsMalformed(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var pattern = Z.ToJsonSchema(Z.String().Guid()).Pattern!;

		// Assert — the reproduction value (version nibble `f`, variant nibble `0`) is a valid Guid.
		await Assert.That(Regex.IsMatch(NonRfcGuid, pattern)).IsTrue();
		await Assert.That(Regex.IsMatch(Guid.Empty.ToString("D"), pattern)).IsTrue();
		await Assert.That(Regex.IsMatch("9f41585f-31c5-fc4b-0d04-b1fe5b52313", pattern)).IsFalse();
		await Assert.That(Regex.IsMatch("not-a-guid", pattern)).IsFalse();
	}

	[Test]
	public async Task ToJsonSchema_GivenVersionlessUuid_ExportsUuidFormatOnly(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().UUID());

		// Assert
		await Assert.That(jsonSchema.Format).IsEqualTo("uuid");
		await Assert.That(jsonSchema.Pattern).IsNull();
	}

	[Test]
	public async Task ToJsonSchema_GivenVersionedUuid_ExportsUuidFormatAndVersionPattern(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var jsonSchema = Z.ToJsonSchema(Z.String().UUID(UuidVersion.V4));

		// Assert — the version is carried by the pattern, since JSON Schema has no versioned uuid format.
		await Assert.That(jsonSchema.Format).IsEqualTo("uuid");
		await Assert.That(jsonSchema.Pattern).IsEqualTo(UUIDRule.JsonSchemaVersionPattern(UuidVersion.V4));
		await Assert.That(Regex.IsMatch("550e8400-e29b-41d4-a716-446655440000", jsonSchema.Pattern!)).IsTrue();
		await Assert.That(Regex.IsMatch("0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c", jsonSchema.Pattern!)).IsFalse();
	}

	[Test]
	public async Task ToJsonSchema_GivenTwoPatternRules_PreservesBothThroughAllOf(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act — StartsWith and EndsWith both constrain `pattern`; neither may overwrite the other.
		var jsonSchema = Z.ToJsonSchema(Z.String().StartsWith("a").EndsWith("z"));

		// Assert
		await Assert.That(jsonSchema.Pattern).IsNull();
		await Assert.That(jsonSchema.AllOf).IsNotNull();
		await Assert.That(jsonSchema.AllOf!).Count().IsEqualTo(2);
		await Assert.That(jsonSchema.AllOf!.Select(branch => branch.Pattern)).Contains("^a");
		await Assert.That(jsonSchema.AllOf!.Select(branch => branch.Pattern)).Contains("z$");
	}

	[Test]
	public async Task ConstrainPattern_GivenTwoPatterns_PromotesToTypedAllOfBranches(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string" };

		// Act
		JsonSchemaRules.ConstrainPattern(schema, "^a");
		JsonSchemaRules.ConstrainPattern(schema, "z$");

		// Assert — each branch carries the type, because Zod's fromJSONSchema drops a type-less branch.
		await Assert.That(schema.Pattern).IsNull();
		await Assert.That(schema.AllOf!).Count().IsEqualTo(2);
		await Assert.That(schema.AllOf!.All(branch => branch.Type == "string")).IsTrue();
	}

	[Test]
	public async Task ConstrainPattern_GivenTheSamePatternTwice_KeepsASingleKeyword(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string" };

		// Act
		JsonSchemaRules.ConstrainPattern(schema, "^a");
		JsonSchemaRules.ConstrainPattern(schema, "^a");

		// Assert
		await Assert.That(schema.Pattern).IsEqualTo("^a");
		await Assert.That(schema.AllOf).IsNull();
	}
}

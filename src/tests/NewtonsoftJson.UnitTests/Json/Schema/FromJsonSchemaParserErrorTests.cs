using Newtonsoft.Json.Linq;
using ZodSharp.Core;
using ZodSharp.JsonSchema.NewtonsoftJson;

namespace ZodSharp.Json.Schema;

/// <summary>
/// Covers the errors produced by schemas imported from JSON Schema: typed index and key paths that keep
/// every error field, <c>minItems</c>/<c>maxItems</c> failures shaped like <c>ZodArray</c>'s, and type
/// mismatches described with Zod's type names.
/// </summary>
public class FromJsonSchemaParserErrorTests
{
	[Test]
	public async Task Validate_GivenInvalidArrayElement_PrefixesTypedIndexAndKeepsEveryField()
	{
		// Defect J7
		// Arrange
		var schema = FromJsonSchemaParser.Parse("""{"type":"array","items":{"type":"array","minItems":2}}""");

		// Act
		var result = schema.Validate(JToken.Parse("[[1,2],[1]]"));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.From(ZodPathSegment.At(1)));
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Minimum).IsEqualTo(2);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	[Test]
	public async Task Validate_GivenTooFewItems_MirrorsZodArrayLengthFailure()
	{
		// Defect J8
		// Arrange
		var schema = FromJsonSchemaParser.Parse("""{"type":"array","minItems":2,"maxItems":3}""");

		// Act
		var result = schema.Validate(JToken.Parse("[1]"));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("too_small");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Minimum).IsEqualTo(2);
		await Assert.That(error.Maximum).IsEqualTo(3);
		await Assert.That(error.Inclusive).IsTrue();
		await Assert.That(error.Message).IsEqualTo("Array must have at least 2 elements, but got 1");
	}

	[Test]
	public async Task Validate_GivenTooManyItems_MirrorsZodArrayLengthFailure()
	{
		// Defect J8
		// Arrange
		var schema = FromJsonSchemaParser.Parse("""{"type":"array","maxItems":1}""");

		// Act
		var result = schema.Validate(JToken.Parse("[1,2]"));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("too_big");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Maximum).IsEqualTo(1);
		await Assert.That(error.Message).IsEqualTo("Array must have at most 1 elements, but got 2");
	}

	[Test]
	[Arguments("""{"type":"string"}""", "42", "Expected string, but got number")]
	[Arguments("""{"type":"number"}""", "\"x\"", "Expected number, but got string")]
	[Arguments("""{"type":"boolean"}""", "{}", "Expected boolean, but got object")]
	[Arguments("""{"type":"object"}""", "[]", "Expected object, but got array")]
	[Arguments("""{"type":"array"}""", "true", "Expected array, but got boolean")]
	[Arguments("""{"type":"null"}""", "1", "Expected null, but got number")]
	public async Task Validate_GivenWrongJsonKind_ReportsZodTypeNames(string jsonSchema, string json, string message)
	{
		// Defect J9
		// Arrange
		var schema = FromJsonSchemaParser.Parse(jsonSchema);

		// Act
		var result = schema.Validate(JToken.Parse(json));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_type");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(error.Message).IsEqualTo(message);
	}

	[Test]
	public async Task Validate_GivenLiteralMismatch_DoesNotEchoTheReceivedValue()
	{
		// Defect J9
		// Arrange
		var schema = FromJsonSchemaParser.Parse("""{"const":"admin"}""");

		// Act
		var result = schema.Validate(JToken.Parse("\"secret-value\""));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_literal");
		await Assert.That(error.Message).IsEqualTo("Expected literal value 'admin', but got string");
	}

	[Test]
	public async Task Validate_GivenInvalidRecordValue_PrefixesKeySegmentAndKeepsEveryField()
	{
		// Defect J10
		// Arrange
		var schema = FromJsonSchemaParser.Parse(
			"""{"type":"object","additionalProperties":{"type":"array","minItems":2}}"""
		);

		// Act
		var result = schema.Validate(JToken.Parse("""{"a.b":[1]}"""));

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.From(ZodPathSegment.Key("a.b")));
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Minimum).IsEqualTo(2);
	}
}

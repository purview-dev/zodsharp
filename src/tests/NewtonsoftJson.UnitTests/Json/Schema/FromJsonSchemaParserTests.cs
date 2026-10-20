using ZodSharp.JsonSchema;
using ZodSharp.JsonSchema.NewtonsoftJson;
using ZodSharp.Rules;

namespace ZodSharp.Json.Schema;

/// <summary>
/// Newtonsoft.Json counterpart of the System.Text.Json JSON Schema import tests: the
/// <c>Z.FromJsonSchema</c> extension member, local and external <c>$ref</c> handling, and the exported
/// serializer options for this package.
/// </summary>
public class FromJsonSchemaParserTests
{
	const string LocalRefSchema =
		/*lang=json,strict*/
		"""
			{
				"$defs": { "name": { "type": "string", "minLength": 1 } },
				"type": "object",
				"properties": { "name": { "$ref": "#/$defs/name" } },
				"required": ["name"]
			}
			""";

	const string ExternalRefSchema =
		/*lang=json,strict*/
		"""
			{
				"type": "object",
				"properties": { "name": { "$ref": "external.json#/$defs/name" } }
			}
			""";

	[Test]
	public async Task FromJsonSchema_GivenLocalDefsRef_ResolvesReference()
	{
		// Arrange
		var schema = Z.FromJsonSchema(LocalRefSchema);

		// Act
		var validResult = schema.Validate(new Dictionary<string, object?> { ["name"] = "Ada" });
		var invalidResult = schema.Validate(new Dictionary<string, object?> { ["name"] = "" });

		// Assert
		await Assert.That(validResult.IsSuccess).IsTrue();
		await Assert.That(invalidResult.IsSuccess).IsFalse();
	}

	[Test]
	public async Task FromJsonSchema_GivenExternalRef_ThrowsNotSupportedExceptionNamingReference()
	{
		// Arrange

		// Act
		var exception = Assert.Throws<NotSupportedException>(() => Z.FromJsonSchema(ExternalRefSchema));

		// Assert
		await Assert.That(exception!.Message).Contains("External $ref 'external.json#/$defs/name' is not supported");
		await Assert.That(exception.Message).Contains("pre-resolve external schemas before importing");
	}

	[Test]
	public async Task Parse_GivenDefinitionWithStringConstraint_ValidatesAgainstConstraint()
	{
		// Arrange
		JsonSchemaDefinition definition = new() { Type = "string", MinLength = 2 };

		// Act
		var schema = FromJsonSchemaParser.Parse(definition);

		// Assert
		await Assert.That(schema.Validate("ab").IsSuccess).IsTrue();
		await Assert.That(schema.Validate("a").IsSuccess).IsFalse();
	}

	[Test]
	public async Task Parse_GivenNullDefinition_ThrowsArgumentNullException()
	{
		// Arrange
		JsonSchemaDefinition definition = null!;

		// Act
		var exception = Assert.Throws<ArgumentNullException>(() => FromJsonSchemaParser.Parse(definition));

		// Assert
		await Assert.That(exception!.ParamName).IsEqualTo("schema");
	}

	[Test]
	public async Task JsonSchemaSerializerOptions_Default_UsesJsonSchemaKeywordAndCamelCaseNames()
	{
		// Arrange
		JsonSchemaDefinition definition = new() { MinLength = 3 };

		// Act
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(definition, JsonSchemaSerializerOptions.Default);

		// Assert
		await Assert.That(json).Contains("\"minLength\"");
		await Assert
			.That(JsonSchemaSerializerOptions.Default.Formatting)
			.IsEqualTo(Newtonsoft.Json.Formatting.Indented);
	}

	[Test]
	public async Task Parse_GivenNullSubSchema_ThrowsArgumentException()
	{
		// Arrange
		JsonSchemaDefinition definition = new()
		{
			Type = "object",
			Properties = new Dictionary<string, JsonSchemaDefinition> { ["name"] = null! },
		};

		// Act
		var exception = Assert.Throws<ArgumentException>(() => FromJsonSchemaParser.Parse(definition));

		// Assert
		await Assert.That(exception!.Message).Contains("null sub-schema");
	}

	[Test]
	public async Task JsonSchemaSerializerOptions_GivenRefAndDefs_UsesJsonSchemaKeywordNames()
	{
		// Arrange
		JsonSchemaDefinition definition = new()
		{
			Ref = "#/$defs/name",
			Defs = new Dictionary<string, JsonSchemaDefinition> { ["name"] = new() { Type = "string" } },
		};

		// Act
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(definition, JsonSchemaSerializerOptions.Default);

		// Assert
		await Assert.That(json).Contains("\"$ref\"");
		await Assert.That(json).Contains("\"$defs\"");
	}

	[Test]
	public async Task Parse_GivenAllOfPatternBranches_EnforcesEveryBranch(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the shape a Guid scalar with [NonSentinel] exports as: the canonical Guid pattern and the
		// non-empty pattern as two allOf branches.
		JsonSchemaDefinition definition = new()
		{
			Type = "string",
			AllOf =
			[
				new JsonSchemaDefinition { Type = "string", Pattern = GuidRule.JsonSchemaPattern },
				new JsonSchemaDefinition { Type = "string", Pattern = NonSentinelRule<Guid>.GuidJsonSchemaPattern },
			],
		};

		// Act
		var schema = FromJsonSchemaParser.Parse(definition);

		// Assert — every branch is applied: the non-RFC Guid passes, the empty Guid and a malformed value fail.
		await Assert.That(schema.Validate("9f41585f-31c5-fc4b-0d04-b1fe5b523139").IsSuccess).IsTrue();
		await Assert.That(schema.Validate("00000000-0000-0000-0000-000000000000").IsSuccess).IsFalse();
		await Assert.That(schema.Validate("not-a-guid").IsSuccess).IsFalse();
	}

	[Test]
	public async Task JsonSchemaSerializerOptions_GivenTemporalPattern_WritesPatternAndUnsupportedKeyword()
	{
		// Arrange
		JsonSchemaDefinition definition = new()
		{
			Type = "string",
			Format = "date-time",
			Pattern = NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern,
			UnsupportedRules = [nameof(NonSentinelRule<>)],
		};

		// Act
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(definition, JsonSchemaSerializerOptions.Default);

		// Assert
		await Assert.That(json).Contains("\"pattern\"");
		await Assert.That(json).Contains("\"x-zodsharp-unsupported\"");
		await Assert.That(json).Contains(nameof(NonSentinelRule<>));
	}

	[Test]
	public async Task Parse_GivenDefaultDateTimeOffsetPattern_RejectsOffsetEquivalentBounds(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition definition = new()
		{
			Type = "string",
			Pattern = NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern,
		};

		// Act
		var schema = FromJsonSchemaParser.Parse(definition);

		// Assert
		await Assert.That(schema.Validate("2024-01-02T03:04:05+05:30").IsSuccess).IsTrue();
		await Assert.That(schema.Validate("0001-01-01T05:30:00+05:30").IsSuccess).IsFalse();
		await Assert.That(schema.Validate("9999-12-31T18:29:59.9999999-05:30").IsSuccess).IsFalse();
	}

	[Test]
	public async Task Parse_GivenUtcTemporalPattern_RejectsBoundsAndNonUtcOffsets(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition definition = new()
		{
			Type = "string",
			Pattern = NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern,
		};

		// Act
		var schema = FromJsonSchemaParser.Parse(definition);

		// Assert
		await Assert.That(schema.Validate("2024-01-02T03:04:05Z").IsSuccess).IsTrue();
		await Assert.That(schema.Validate("0001-01-01T00:00:00Z").IsSuccess).IsFalse();
		await Assert.That(schema.Validate("0001-01-01T05:30:00+05:30").IsSuccess).IsFalse();
		await Assert.That(schema.Validate("2024-01-02T03:04:05+05:30").IsSuccess).IsFalse();
	}
}

using Newtonsoft.Json;
using ZodSharp.JsonSchema;
using ZodSharp.JsonSchema.NewtonsoftJson;

namespace ZodSharp.Json;

/// <summary>
/// Record (dictionary) JSON Schema export and import through the Newtonsoft.Json integration.
/// </summary>
public class NewtonsoftJsonRecordTests
{
	const string RecordSchema =
		/*lang=json,strict*/
		"""
			{
				"type": "object",
				"additionalProperties": { "type": "string", "minLength": 1 }
			}
			""";

	[Test]
	public async Task FromJsonSchema_GivenAdditionalProperties_ValidatesEveryValue()
	{
		// Arrange — an object with no declared properties and a value schema is a record.
		var schema = Z.FromJsonSchema(RecordSchema);

		// Act
		var validResult = schema.Validate(new Dictionary<string, object?> { ["a"] = "x" });
		var invalidResult = schema.Validate(new Dictionary<string, object?> { ["a"] = "" });

		// Assert
		await Assert.That(validResult.IsSuccess).IsTrue();
		await Assert.That(invalidResult.IsSuccess).IsFalse();
		await Assert.That(invalidResult.Errors[0].Path[0]).IsEqualTo("a");
	}

	[Test]
	public async Task FromJsonSchema_GivenAdditionalPropertiesTrue_ValidatesEveryValue()
	{
		// Arrange
		const string source =
			/*lang=json,strict*/
			"""
				{ "type": "object", "additionalProperties": true }
				""";

		var schema = Z.FromJsonSchema(source);

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["a"] = "anything", ["b"] = 42 });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task JsonSchemaSerialization_GivenRecordDefinition_WritesObjectKeywords()
	{
		// Arrange — AdditionalProperties is object-typed; the serialized form is what consumers see.
		JsonSchemaDefinition definition = new()
		{
			Type = "object",
			MinProperties = 1,
			AdditionalProperties = new JsonSchemaDefinition { Type = "string" },
		};

		// Act
		var json = JsonConvert.SerializeObject(definition, JsonSchemaSerializerOptions.Default);

		// Assert
		await Assert.That(json).Contains("\"additionalProperties\"");
		await Assert.That(json).Contains("\"type\": \"string\"");
		await Assert.That(json).Contains("\"minProperties\": 1");
	}
}

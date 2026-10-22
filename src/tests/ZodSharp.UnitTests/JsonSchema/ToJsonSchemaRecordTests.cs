namespace ZodSharp.JsonSchema;

/// <summary>
/// Tests for <see cref="Schemas.ZodRecord{TValue}"/> JSON Schema export.
/// </summary>
/// <remarks>
/// A record is written as a JSON object, so it exports as <c>type: object</c> whose <c>additionalProperties</c>
/// is the value schema. Before the <see cref="IJsonSchemaRecordInfo"/> seam the generic record fell through to
/// the empty-schema fallback.
/// </remarks>
public class ToJsonSchemaRecordTests
{
	[Test]
	public async Task ToJsonSchema_GivenRecord_ExportsObjectWithAdditionalProperties()
	{
		// Arrange
		var schema = Z.Record(Z.Number().Min(0));

		// Act
		var jsonSchema = Z.ToJsonSchema(schema);

		// Assert
		await Assert.That(jsonSchema.Type).IsEqualTo("object");
		await Assert.That(jsonSchema.Properties).IsNull();

		var additionalProperties = jsonSchema.AdditionalProperties as JsonSchemaDefinition;
		await Assert.That(additionalProperties).IsNotNull();
		await Assert.That(additionalProperties!.Type).IsEqualTo("number");
		await Assert.That(additionalProperties.Minimum).IsEqualTo(0d);
	}

	[Test]
	public async Task ToJsonSchema_GivenRecordOfNullableValues_ExportsNullValueBranch()
	{
		// Arrange
		var schema = Z.Record(Z.Nullable(Z.Number()));

		// Act
		var jsonSchema = Z.ToJsonSchema(schema);

		// Assert
		var additionalProperties = jsonSchema.AdditionalProperties as JsonSchemaDefinition;
		await Assert.That(additionalProperties).IsNotNull();
		await Assert.That(additionalProperties!.AnyOf).Count().IsEqualTo(2);
		await Assert.That(additionalProperties.AnyOf!.Any(static branch => branch.Type == "null")).IsTrue();
	}
}

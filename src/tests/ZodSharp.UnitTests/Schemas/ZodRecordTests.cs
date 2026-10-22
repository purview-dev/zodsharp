namespace ZodSharp.Schemas;

public class ZodRecordTests
{
	[Test]
	public async Task Record_GivenAllValuesValid_ReturnsSuccess()
	{
		// Arrange
		var schema = Z.Record(Z.Number().Min(0));

		// Act
		var result = schema.Validate(
			new Dictionary<string, double>
			{
				["a"] = 1.0,
				["b"] = 2.0,
				["c"] = 3.0,
			}
		);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!["a"]).IsEqualTo(1.0);
	}

	[Test]
	public async Task Record_GivenSomeValuesInvalid_ReturnsFailureWithPath()
	{
		// Arrange
		var schema = Z.Record(Z.Number().Min(0));

		// Act
		var result = schema.Validate(new Dictionary<string, double> { ["a"] = 1.0, ["b"] = -5.0 });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path).Contains("b");
	}

	[Test]
	public async Task Record_GivenNull_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Record(Z.String());

		// Act
		var result = schema.Validate(null!);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_type");
	}

	[Test]
	public async Task Record_GivenNullValue_ReturnsFailure()
	{
		// Arrange — a non-nullable value schema rejects a null value, matching z.record(z.string(), T).
		var schema = Z.Record(Z.String());

		// Act
		var result = schema.Validate(new Dictionary<string, string> { ["a"] = null! });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_type");
		await Assert.That(result.Errors[0].Path).Contains("a");
	}

	[Test]
	public async Task Record_GivenNullableValueSchema_AcceptsNullValue()
	{
		// Arrange — null acceptance is the value schema's decision.
		var schema = Z.Record(Z.Optional(Z.String()));

		// Act
		var result = schema.Validate(new Dictionary<string, string?> { ["a"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Record_GivenInvalidValue_PreservesStructuredIssueMetadata()
	{
		// Arrange — the value schema's issue metadata survives the key-prefixed path rewrite.
		var schema = Z.Record(Z.Array(Z.String()).Min(2));

		// Act
		var result = schema.Validate(new Dictionary<string, string[]> { ["a"] = ["only"] });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path[0]).IsEqualTo("a");
		await Assert.That(result.Errors[0].Origin).IsEqualTo("array");
		await Assert.That(result.Errors[0].Minimum).IsEqualTo(2);
	}

	[Test]
	public async Task Record_GivenEmptyDictionary_ReturnsSuccess()
	{
		// Arrange
		var schema = Z.Record(Z.String());

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Count).IsEqualTo(0);
	}
}

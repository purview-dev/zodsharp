namespace ZodSharp.Schemas;

public class ZodObjectTests
{
	[Test]
	public async Task ObjectValidate_GivenCompleteObject_ReturnsSuccess()
	{
		var schema = CreateUserSchema();
		Dictionary<string, object?> data = new()
		{
			["name"] = "John Doe",
			["age"] = 30.0,
			["email"] = "john@example.com",
		};

		var result = schema.Validate(data);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!["name"]).IsEqualTo("John Doe");
	}

	[Test]
	public async Task ObjectValidate_GivenMissingRequiredField_ReturnsFailure()
	{
		var schema = CreateUserSchema();
		Dictionary<string, object?> data = new() { ["name"] = "John Doe", ["age"] = 30.0 };

		var result = schema.Validate(data);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static error => error.Code == "missing_field")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenWrongFieldType_ReturnsFailure()
	{
		var schema = CreateUserSchema();
		Dictionary<string, object?> data = new()
		{
			["name"] = "John Doe",
			["age"] = "not-a-number",
			["email"] = "john@example.com",
		};

		var result = schema.Validate(data);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path).Contains("age");
	}

	[Test]
	public async Task ObjectValidate_GivenInvalidEmail_ReturnsFailure()
	{
		var schema = CreateUserSchema();
		Dictionary<string, object?> data = new()
		{
			["name"] = "John Doe",
			["age"] = 30.0,
			["email"] = "not-an-email",
		};

		var result = schema.Validate(data);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path).Contains("email");
	}

	[Test]
	public async Task ObjectValidate_GivenInt64ForNumberField_CoercesToDoubleAndSucceeds()
	{
		// Newtonsoft.Json / System.Text.Json deserialize integer literals (e.g. `30`)
		// as Int64, not Double. The SchemaWrapper must coerce boxed numeric values
		// so hand-built object schemas validate JSON deserialization output.
		var schema = CreateUserSchema();
		Dictionary<string, object?> data = new()
		{
			["name"] = "John Doe",
			["age"] = 30L,
			["email"] = "john@example.com",
		};

		var result = schema.Validate(data);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!["age"]).IsEqualTo(30.0);
	}

	static ZodObject CreateUserSchema() =>
		Z.Object()
			.Field("name", Z.String().Min(1))
			.Field("age", Z.Number().Min(0).Max(120))
			.Field("email", Z.String().Email())
			.Build();
}

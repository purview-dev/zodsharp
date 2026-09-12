using Newtonsoft.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

public class NewtonsoftJsonExtensionsTests
{
	[Test]
	public async Task DeserializeAndValidate_GivenValidJson_ReturnsSuccess()
	{
		TestUserSchema schema = new();
		var json = /*lang=json,strict*/
			"""{"name":"John","age":30}""";

		var result = schema.DeserializeAndValidate(json);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Name).IsEqualTo("John");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenMalformedJson_ReturnsJsonErrorFailure()
	{
		TestUserSchema schema = new();

		var result = schema.DeserializeAndValidate("{bad json");

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("json_error");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenSchemaInvalidJson_ReturnsValidationFailure()
	{
		TestUserSchema schema = new();
		var json = /*lang=json,strict*/
			"""{"name":"","age":30}""";

		var result = schema.DeserializeAndValidate(json);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static error => error.Path.Contains("Name"))).IsTrue();
	}

	[Test]
	public async Task CreateValidatingConverter_GivenValidJson_DeserializesSuccessfully()
	{
		TestUserSchema schema = new();
		JsonSerializerSettings settings = new() { Converters = { schema.CreateValidatingConverter() } };

		var deserialized = JsonConvert.DeserializeObject<TestUser>(
			/*lang=json,strict*/"""{"name":"John","age":30}""",
			settings
		);

		await Assert.That(deserialized).IsNotNull();
		await Assert.That(deserialized!.Name).IsEqualTo("John");
	}

	[Test]
	public async Task CreateValidatingConverter_GivenInvalidData_ThrowsJsonSerializationException()
	{
		TestUserSchema schema = new();
		JsonSerializerSettings settings = new() { Converters = { schema.CreateValidatingConverter() } };

		var exception = Assert.Throws<JsonSerializationException>(() =>
			JsonConvert.DeserializeObject<TestUser>( /*lang=json,strict*/
				"""{"name":"","age":30}""",
				settings
			)
		);

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception.Message).Contains("Validation failed");
	}

	sealed class TestUser
	{
		[JsonProperty("name")]
		public string? Name { get; set; }

		[JsonProperty("age")]
		public int Age { get; set; }
	}

	sealed class TestUserSchema : IZodSchema<TestUser, TestUser>
	{
		public ValidationResult<TestUser> Validate(TestUser value)
		{
			if (value is null)
			{
				return ValidationResult<TestUser>.Failure(
					new ValidationError("invalid_type", "Expected user, but got null")
				);
			}

			List<ValidationError> errors = [];

			if (string.IsNullOrWhiteSpace(value.Name))
			{
				errors.Add(new ValidationError("too_small", "Name is required", [nameof(TestUser.Name)]));
			}

			if (value.Age < 0)
			{
				errors.Add(new ValidationError("too_small", "Age must be non-negative", [nameof(TestUser.Age)]));
			}

			return errors.Count == 0
				? ValidationResult<TestUser>.Success(value)
				: ValidationResult<TestUser>.Failure(errors);
		}

		public ValueTask<ValidationResult<TestUser>> ValidateAsync(
			TestUser value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

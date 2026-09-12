using System.Text.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

public class SystemTextJsonConverterTests
{
	static JsonSerializerOptions CreateOptions<T>(IZodSchema<T, T> schema) =>
		new() { Converters = { schema.CreateValidatingConverter() } };

	[Test]
	public async Task CreateValidatingConverter_GivenValidJson_DeserializesSuccessfully()
	{
		TestUserSchema schema = new();
		var options = CreateOptions(schema);

		var deserialized = JsonSerializer.Deserialize<TestUser>("""{"Name":"John","Age":30}""", options);

		await Assert.That(deserialized).IsNotNull();
		await Assert.That(deserialized!.Name).IsEqualTo("John");
		await Assert.That(deserialized.Age).IsEqualTo(30);
	}

	[Test]
	public async Task CreateValidatingConverter_GivenNullValue_ReturnsNull()
	{
		TestUserSchema schema = new();
		var options = CreateOptions(schema);

		// System.Text.Json returns null for "null" without invoking the converter's Read
		// when T is a reference type, so no validation is triggered.
		var deserialized = JsonSerializer.Deserialize<TestUser>("null", options);

		await Assert.That(deserialized).IsNull();
	}

	[Test]
	public async Task CreateValidatingConverter_GivenInvalidData_ThrowsJsonException()
	{
		TestUserSchema schema = new();
		var options = CreateOptions(schema);

		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<TestUser>("""{"Name":"","Age":30}""", options)
		);

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception.Message).Contains("Validation failed");
	}

	[Test]
	public async Task CreateValidatingConverter_OnSerialize_ValidatesAndWrites()
	{
		TestUserSchema schema = new();
		var options = CreateOptions(schema);
		TestUser user = new() { Name = "John", Age = 30 };

		var json = JsonSerializer.Serialize(user, options);

		await Assert.That(json).Contains("John");
		await Assert.That(json).Contains("30");
	}

	[Test]
	public async Task CreateValidatingConverter_OnSerialize_GivenInvalidData_ThrowsJsonException()
	{
		TestUserSchema schema = new();
		var options = CreateOptions(schema);
		TestUser user = new() { Name = "", Age = 30 };

		var exception = Assert.Throws<JsonException>(() => JsonSerializer.Serialize(user, options));

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception.Message).Contains("Validation failed");
	}

	[Test]
	public async Task CreateValidatingConverter_GivenNullSchema_ThrowsArgumentNullException()
	{
		IZodSchema<TestUser, TestUser> schema = null!;

		var exception = Assert.Throws<ArgumentNullException>(() => schema.CreateValidatingConverter());

		await Assert.That(exception!.ParamName).IsEqualTo("schema");
	}

	sealed class TestUser
	{
		public string? Name { get; set; }
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

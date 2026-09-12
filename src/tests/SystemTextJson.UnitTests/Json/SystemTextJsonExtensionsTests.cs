using System.Text;
using System.Text.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

public class SystemTextJsonExtensionsTests
{
	static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	[Test]
	public async Task DeserializeAndValidate_GivenValidJson_ReturnsSuccess()
	{
		TestUserSchema schema = new();
		var json = /*lang=json,strict*/
			"""{"Name":"John","Age":30}""";

		var result = schema.DeserializeAndValidate(json);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Name).IsEqualTo("John");
		await Assert.That(result.Value.Age).IsEqualTo(30);
	}

	[Test]
	public async Task DeserializeAndValidate_GivenValidJson_WithCamelCaseOptions_ReturnsSuccess()
	{
		TestUserSchema schema = new();
		var json = /*lang=json,strict*/
			"""{"name":"John","age":30}""";

		var result = schema.DeserializeAndValidate(json, CamelCase);

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
			"""{"Name":"","Age":30}""";

		var result = schema.DeserializeAndValidate(json);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Path.Contains("Name"))).IsTrue();
	}

	[Test]
	public async Task DeserializeAndValidate_GivenNullJson_ThrowsArgumentNullException()
	{
		TestUserSchema schema = new();

		var exception = Assert.Throws<ArgumentNullException>(() => schema.DeserializeAndValidate(null!));

		await Assert.That(exception!.ParamName).IsEqualTo("json");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenNullSchema_ThrowsArgumentNullException()
	{
		IZodSchema<TestUser, TestUser> schema = null!;

		var exception = Assert.Throws<ArgumentNullException>(() => schema.DeserializeAndValidate("{}"));

		await Assert.That(exception!.ParamName).IsEqualTo("schema");
	}

	[Test]
	public async Task DeserializeAndValidateAsync_GivenValidJsonStream_ReturnsSuccess()
	{
		TestUserSchema schema = new();
		var jsonBytes = Encoding.UTF8.GetBytes( /*lang=json,strict*/
			"""{"Name":"John","Age":30}"""
		);
		using MemoryStream stream = new(jsonBytes);

		var result = await schema.DeserializeAndValidateAsync(stream);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Name).IsEqualTo("John");
		await Assert.That(result.Value.Age).IsEqualTo(30);
	}

	[Test]
	public async Task DeserializeAndValidateAsync_GivenMalformedJsonStream_ReturnsJsonErrorFailure()
	{
		TestUserSchema schema = new();
		var jsonBytes = Encoding.UTF8.GetBytes("{bad json");
		using MemoryStream stream = new(jsonBytes);

		var result = await schema.DeserializeAndValidateAsync(stream);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("json_error");
	}

	[Test]
	public async Task ValidateAndSerialize_GivenValidObject_ReturnsSuccessWithJsonString()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "John", Age = 30 };

		var result = schema.ValidateAndSerialize(user);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert
			.That(result.Value)
			.IsEqualTo( /*lang=json,strict*/
				"""{"Name":"John","Age":30}"""
			);
	}

	[Test]
	public async Task ValidateAndSerialize_GivenInvalidObject_ReturnsFailureWithoutSerializing()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "", Age = 30 };

		var result = schema.ValidateAndSerialize(user);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Path.Contains("Name"))).IsTrue();
	}

	[Test]
	public async Task ValidateAndSerialize_WithCamelCaseOptions_ProducesCamelCaseJson()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "Jane", Age = 25 };

		var result = schema.ValidateAndSerialize(user, CamelCase);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert
			.That(result.Value)
			.IsEqualTo( /*lang=json,strict*/
				"""{"name":"Jane","age":25}"""
			);
	}

	[Test]
	public async Task ValidateAndSerialize_GivenNullValue_ReturnsFailure()
	{
		TestUserSchema schema = new();

		var result = schema.ValidateAndSerialize(null!);

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ValidateAndSerialize_GivenNullSchema_ThrowsArgumentNullException()
	{
		IZodSchema<TestUser, TestUser> schema = null!;

		var exception = Assert.Throws<ArgumentNullException>(() => schema.ValidateAndSerialize(new TestUser()));

		await Assert.That(exception!.ParamName).IsEqualTo("schema");
	}

	[Test]
	public async Task ValidateAndSerializeAsync_GivenValidObject_WritesJsonToStream()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "John", Age = 30 };
		using MemoryStream stream = new();

		var result = await schema.ValidateAndSerializeAsync(user, stream);

		await Assert.That(result.IsSuccess).IsTrue();
		stream.Position = 0;
		using StreamReader reader = new(stream, Encoding.UTF8);
		var json = await reader.ReadToEndAsync();
		await Assert.That(json).Contains("John");
		await Assert.That(json).Contains("30");
	}

	[Test]
	public async Task ValidateAndSerializeAsync_GivenInvalidObject_ReturnsFailureWithoutWriting()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "", Age = -1 };
		using MemoryStream stream = new();

		var result = await schema.ValidateAndSerializeAsync(user, stream);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(stream.Length).IsEqualTo(0);
	}

	[Test]
	public async Task ValidateAndSerializeAsync_GivenNullStream_ThrowsArgumentNullException()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "John", Age = 30 };

		var exception = Assert.Throws<ArgumentNullException>(() =>
			schema.ValidateAndSerializeAsync(user, null!).AsTask().GetAwaiter().GetResult()
		);

		await Assert.That(exception!.ParamName).IsEqualTo("output");
	}

	[Test]
	public async Task RoundTrip_SerializeThenDeserialize_ReturnsEquivalentObject()
	{
		TestUserSchema schema = new();
		TestUser original = new() { Name = "Alice", Age = 42 };

		var serializeResult = schema.ValidateAndSerialize(original);
		await Assert.That(serializeResult.IsSuccess).IsTrue();

		var deserializeResult = schema.DeserializeAndValidate(serializeResult.Value!);
		await Assert.That(deserializeResult.IsSuccess).IsTrue();
		await Assert.That(deserializeResult.Value!.Name).IsEqualTo("Alice");
		await Assert.That(deserializeResult.Value.Age).IsEqualTo(42);
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

using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.Json;

public class NewtonsoftJsonExportTests
{
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
				"""{"name":"John","age":30}"""
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
	public async Task ValidateAndSerialize_GivenNullSchema_ThrowsArgumentNullException()
	{
		IZodSchema<TestUser, TestUser> schema = null!;

		var exception = Assert.Throws<ArgumentNullException>(() => schema.ValidateAndSerialize(new TestUser()));

		await Assert.That(exception!.ParamName).IsEqualTo("schema");
	}

	[Test]
	public async Task ValidateAndSerialize_WithCamelCaseSettings_ProducesCamelCaseJson()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "Jane", Age = 25 };
		JsonSerializerSettings settings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

		var result = schema.ValidateAndSerialize(user, settings);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert
			.That(result.Value)
			.IsEqualTo( /*lang=json,strict*/
				"""{"name":"Jane","age":25}"""
			);
	}

	[Test]
	public async Task ValidateAndSerialize_WithIndentedFormatting_ProducesIndentedJson()
	{
		TestUserSchema schema = new();
		TestUser user = new() { Name = "John", Age = 30 };

		var result = schema.ValidateAndSerialize(user, formatting: Formatting.Indented);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Contains('\n', StringComparison.Ordinal)).IsTrue();
	}

	[Test]
	public async Task ValidateAndSerialize_GivenNullValue_ReturnsFailure()
	{
		TestUserSchema schema = new();

		var result = schema.ValidateAndSerialize(null!);

		await Assert.That(result.IsSuccess).IsFalse();
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
		await Assert.That(json).Contains("\"name\"");
		await Assert.That(json).Contains("John");
		await Assert.That(json).Contains("\"age\"");
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
			schema.ValidateAndSerializeAsync(user, null!).GetAwaiter().GetResult()
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

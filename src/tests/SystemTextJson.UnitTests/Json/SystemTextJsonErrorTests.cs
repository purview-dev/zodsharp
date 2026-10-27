using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Covers how System.Text.Json failures become validation errors: one <c>invalid_json</c> code, the JSON
/// location kept as a typed path plus line and position, a generic message, and full errors from the
/// validating converter.
/// </summary>
public class SystemTextJsonErrorTests
{
	[Test]
	public async Task DeserializeAndValidate_GivenUnconvertibleMemberValue_ReportsPathLineAndPositionWithGenericMessage()
	{
		// Defect J1
		// Arrange
		PassThroughSchema<Person> schema = new();
		const string json = """{"Name":"Ada","Age":"forty-two"}""";

		// Act
		var result = schema.DeserializeAndValidate(json);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.Member("Age"));
		await Assert.That(error.Message).IsEqualTo("The JSON value could not be converted to number.");
		await Assert.That(error.Parameters!["line"]).IsEqualTo(0L);
		await Assert.That(error.Parameters!["position"]).IsNotNull();
		await Assert.That(error.Message).DoesNotContain("System.");
		await Assert.That(error.Message).DoesNotContain("forty-two");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenJsonTypeInfoAndBadElement_ReportsIndexedPath()
	{
		// Defect J1
		// Arrange
		PassThroughSchema<Team> schema = new();
		JsonSerializerOptions options = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
		var typeInfo = (JsonTypeInfo<Team>)options.TypeInfoResolver.GetTypeInfo(typeof(Team), options)!;
		const string json = """{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":true}]}""";

		// Act
		var result = schema.DeserializeAndValidate(json, typeInfo);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert
			.That(error.FullPath)
			.IsEqualTo(
				ZodPath.From(ZodPathSegment.Member("Members"), ZodPathSegment.At(1), ZodPathSegment.Member("Age"))
			);
		await Assert.That(error.Message).DoesNotContain("System.");
	}

	[Test]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":}]}""", "Members[1].Age")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":12x}]}""", "Members[1].Age")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":1,}]}""", "Members[1]")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age" 1}]}""", "Members[1]")]
	public async Task DeserializeAndValidate_GivenMalformedJsonInsideAnElement_ReportsTheSerializersPath(
		string json,
		string expectedPath,
		CancellationToken cancellationToken
	)
	{
		// Arrange — the path is the serializer's: the value being read when a syntax error is a value error,
		// and the containing element when the reader fails between members.
		PassThroughSchema<Team> schema = new();
		using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));

		// Act
		var fromString = schema.DeserializeAndValidate(json).Errors.Single();
		var fromStream = (
			await schema.DeserializeAndValidateAsync(stream, cancellationToken: cancellationToken)
		).Errors.Single();

		// Assert
		await Assert.That(fromString.FullPath.ToDottedString()).IsEqualTo(expectedPath);
		await Assert.That(fromStream.FullPath.ToDottedString()).IsEqualTo(expectedPath);
		await Assert.That(fromString.Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	[Test]
	// A value that cannot be converted is located inside the converted element. A syntax error is located at the
	// element itself: the converter (and, for a stream, the serializer's read-ahead) buffers the whole value, so
	// the position inside it is not known.
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":"x"}]}""", "Members[1].Age")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":true}]}""", "Members[1].Age")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":}]}""", "Members[1]")]
	[Arguments("""{"Members":[{"Name":"Ada","Age":1},{"Name":"Bob","Age":1,}]}""", "Members[1]")]
	public async Task DeserializeAndValidate_GivenMalformedJsonUnderAValidatingConverter_ReportsTheConvertedValuePath(
		string json,
		string expectedPath
	)
	{
		// Arrange
		PassThroughSchema<Team> schema = new();
		JsonSerializerOptions options = new() { Converters = { new PersonSchema().CreateValidatingConverter() } };

		// Act
		var error = schema.DeserializeAndValidate(json, options).Errors.Single();

		// Assert
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo(expectedPath);
	}

	[Test]
	[Arguments("""{"Members":[{"Name":"Ada","Age":true}]}""", "number")]
	[Arguments("""{"Members":[{"Name":true,"Age":1}]}""", "string")]
	[Arguments("""{"Scores":{"a":"x"}}""", "number")]
	[Arguments("""{"Grid":[["x"]]}""", "number")]
	[Arguments("""{"Payload":5}""", "string")]
	[Arguments("""{"Members":5}""", "array")]
	public async Task DeserializeAndValidate_GivenUnconvertibleNestedValue_NamesTheExpectedJsonType(
		string json,
		string expected
	)
	{
		// Arrange
		PassThroughSchema<Roster> schema = new();

		// Act
		var error = schema.DeserializeAndValidate(json).Errors.Single();

		// Assert
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.Expected)).IsEqualTo(expected);
		await Assert.That(error.Message).IsEqualTo($"The JSON value could not be converted to {expected}.");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenMalformedOrNullJson_ReportsSingleInvalidJsonCode()
	{
		// Defect J2
		// Arrange
		PassThroughSchema<Person> schema = new();

		// Act
		var malformed = schema.DeserializeAndValidate("{bad json");
		var nullDocument = schema.DeserializeAndValidate("null");

		// Assert
		await Assert.That(malformed.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert.That(nullDocument.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert.That(nullDocument.Errors.Single().Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert
			.That(nullDocument.Errors.Single().Message)
			.IsEqualTo("The JSON value could not be converted to object.");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenTypeTheSerializerCannotHandle_ReturnsInvalidJsonInsteadOfThrowing()
	{
		// Defect J3
		// Arrange
		PassThroughSchema<IShape> schema = new();

		// Act
		var result = schema.DeserializeAndValidate("""{"Sides":3}""");

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert.That(result.Errors.Single().Message).DoesNotContain("System.");
	}

	[Test]
	public async Task DeserializeAndValidate_GivenNestedValidatingConverterFailure_ReturnsFullErrorsUnderConverterPath()
	{
		// Defect J4
		// Arrange
		PassThroughSchema<Household> schema = new();
		JsonSerializerOptions options = new() { Converters = { new PersonSchema().CreateValidatingConverter() } };
		const string json = """{"Owner":{"Name":"","Age":30}}""";

		// Act
		var result = schema.DeserializeAndValidate(json, options);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(PersonSchema.NameRequiredCode);
		await Assert.That(error.Message).IsEqualTo("Name is required");
		await Assert.That(error.Origin).IsEqualTo("string");
		await Assert.That(error.Minimum).IsEqualTo(1);
		await Assert
			.That(error.FullPath)
			.IsEqualTo(ZodPath.From(ZodPathSegment.Member("Owner"), ZodPathSegment.Member("Name")));
	}

	[Test]
	public async Task Deserialize_GivenValidatingConverterFailure_ThrowsZodJsonValidationExceptionWithErrors()
	{
		// Defect J4
		// Arrange
		JsonSerializerOptions options = new() { Converters = { new PersonSchema().CreateValidatingConverter() } };
		const string json = """{"Owner":{"Name":"","Age":30}}""";

		// Act
		var exception = Assert.Throws<ZodJsonValidationException>(() =>
			JsonSerializer.Deserialize<Household>(json, options)
		);

		// Assert
		await Assert.That(exception!.Message).Contains("Validation failed");
		await Assert.That(exception.Errors.Single().FullPath.ToDottedString()).IsEqualTo("Owner.Name");
	}

	[Test]
	public async Task DeserializeAndValidateAsync_GivenUnconvertibleValueInStream_ReportsPathAndCode()
	{
		// Defect J1
		// Arrange
		PassThroughSchema<Person> schema = new();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"Name":"Ada","Age":[1]}"""));

		// Act
		var result = await schema.DeserializeAndValidateAsync(stream);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.Member("Age"));
	}

	public sealed class Person
	{
		public string? Name { get; set; }

		public int Age { get; set; }
	}

	public sealed class Team
	{
		public List<Person>? Members { get; set; }
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1819:Properties should not return arrays",
		Justification = "The fixture exercises how the serializer reads arrays."
	)]
	public sealed class Roster
	{
		public List<Person>? Members { get; set; }

		public Dictionary<string, int>? Scores { get; set; }

		public int[][]? Grid { get; set; }

		public byte[]? Payload { get; set; }
	}

	public sealed class Household
	{
		public Person? Owner { get; set; }
	}

	public interface IShape
	{
		int Sides { get; }
	}

	sealed class PassThroughSchema<T> : IZodSchema<T, T>
	{
		public ValidationResult<T> Validate(T value) => ValidationResult<T>.Success(value);

		public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
			new(Validate(value));
	}

	sealed class PersonSchema : IZodSchema<Person, Person>
	{
		public const string NameRequiredCode = "name_required";

		public ValidationResult<Person> Validate(Person value) =>
			string.IsNullOrEmpty(value.Name)
				? ValidationResult<Person>.Failure(
					new ValidationError(
						NameRequiredCode,
						"Name is required",
						[nameof(Person.Name)],
						origin: "string",
						minimum: 1,
						category: ZodIssueCategories.TooSmall
					)
				)
				: ValidationResult<Person>.Success(value);

		public ValueTask<ValidationResult<Person>> ValidateAsync(
			Person value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

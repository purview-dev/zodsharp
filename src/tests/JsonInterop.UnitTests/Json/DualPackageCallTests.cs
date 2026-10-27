using System.Text;
using Newtonsoft.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Both JSON packages declare their extensions in namespace <c>ZodSharp</c>. These calls compile only when the
/// argument-light forms resolve to one overload (System.Text.Json's) instead of failing with CS0121, so this file
/// is itself the regression test; the assertions check where each call went.
/// </summary>
public class DualPackageCallTests
{
	const string PersonJson = """{"Name":"Ada"}""";

	[Test]
	public async Task DeserializeAndValidate_GivenBothPackagesAndNoOptions_ResolvesToSystemTextJson(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		PersonSchema schema = new();
		using MemoryStream first = new(Encoding.UTF8.GetBytes(PersonJson));
		using MemoryStream second = new(Encoding.UTF8.GetBytes(PersonJson));

		// Act
		var fromString = schema.DeserializeAndValidate(PersonJson);
		var fromStream = await schema.DeserializeAndValidateAsync(first);
		var fromStreamWithToken = await schema.DeserializeAndValidateAsync(second, cancellationToken);
		var invalid = schema.DeserializeAndValidate("{bad");

		// Assert
		await Assert.That(fromString.Value?.Name).IsEqualTo("Ada");
		await Assert.That(fromStream.Value?.Name).IsEqualTo("Ada");
		await Assert.That(fromStreamWithToken.Value?.Name).IsEqualTo("Ada");
		await Assert.That(invalid.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	[Test]
	public async Task ValidateAndSerialize_GivenBothPackagesAndNoOptions_ResolvesToSystemTextJson(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		PersonSchema schema = new();
		Person person = new() { Name = "Ada" };
		using MemoryStream first = new();
		using MemoryStream second = new();

		// Act
		var json = schema.ValidateAndSerialize(person);
		var toStream = await schema.ValidateAndSerializeAsync(person, first);
		var toStreamWithToken = await schema.ValidateAndSerializeAsync(person, second, cancellationToken);

		// Assert — System.Text.Json writes compact JSON; Newtonsoft's async default is indented.
		await Assert.That(json.Value).IsEqualTo(PersonJson);
		await Assert.That(toStream.IsSuccess).IsTrue();
		await Assert.That(toStreamWithToken.IsSuccess).IsTrue();
		await Assert.That(Encoding.UTF8.GetString(first.ToArray())).IsEqualTo(PersonJson);
	}

	[Test]
	public async Task DeserializeAndValidate_GivenNewtonsoftSettings_StillResolvesToNewtonsoft()
	{
		// Arrange
		PersonSchema schema = new();
		JsonSerializerSettings settings = new();

		// Act
		var result = schema.DeserializeAndValidate(PersonJson, settings);
		var converter = NewtonsoftJsonExtensions.CreateValidatingConverter(schema);

		// Assert
		await Assert.That(result.Value?.Name).IsEqualTo("Ada");
		await Assert.That(converter).IsAssignableTo<JsonConverter>();
	}

	public sealed class Person
	{
		public string? Name { get; set; }
	}

	sealed class PersonSchema : IZodSchema<Person, Person>
	{
		public ValidationResult<Person> Validate(Person value) => ValidationResult<Person>.Success(value);

		public ValueTask<ValidationResult<Person>> ValidateAsync(
			Person value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// The <see cref="IServiceProvider"/> overloads of <c>DeserializeAndValidateAsync</c> and
/// <c>ValidateAndSerializeAsync</c>: the services reach the validator.
/// </summary>
public class SystemTextJsonServiceAwareTests
{
	[Test]
	public async Task DeserializeAndValidateAsync_GivenServices_PassesThemToTheValidator(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		BannedNames banned = new("Mallory");
		using MemoryStream stream = new(
			Encoding.UTF8.GetBytes( /*lang=json,strict*/
				"""{"name":"Mallory"}"""
			)
		);

		// Act
		var result = await new ServiceAwareValidator().DeserializeAndValidateAsync(
			stream,
			ServiceAwareJsonContext.Default.Person,
			banned,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(ServiceAwareValidator.BannedCode);
	}

	[Test]
	public async Task DeserializeAndValidateAsync_GivenServicesAndMalformedJson_ReturnsInvalidJson(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("{not json"));

		// Act
		var result = await new ServiceAwareValidator().DeserializeAndValidateAsync(
			stream,
			ServiceAwareJsonContext.Default.Person,
			new BannedNames(),
			cancellationToken
		);

		// Assert
		await Assert.That(result.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	[Test]
	public async Task ValidateAndSerializeAsync_GivenServices_WritesOnlyWhenTheServiceAwareRulePasses(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		BannedNames banned = new("Mallory");
		ServiceAwareValidator validator = new();
		using MemoryStream rejected = new();
		using MemoryStream accepted = new();

		// Act
		var rejectedResult = await validator.ValidateAndSerializeAsync(
			new Person { Name = "Mallory" },
			rejected,
			ServiceAwareJsonContext.Default.Person,
			banned,
			cancellationToken
		);
		var acceptedResult = await validator.ValidateAndSerializeAsync(
			new Person { Name = "Alice" },
			accepted,
			ServiceAwareJsonContext.Default.Person,
			banned,
			cancellationToken
		);

		// Assert
		await Assert.That(rejectedResult.IsSuccess).IsFalse();
		await Assert.That(rejected.Length).IsEqualTo(0);
		await Assert.That(acceptedResult.IsSuccess).IsTrue();
		await Assert
			.That(Encoding.UTF8.GetString(accepted.ToArray()))
			.IsEqualTo( /*lang=json,strict*/
				"""{"name":"Alice"}"""
			);
	}

	internal sealed class Person
	{
		public string? Name { get; set; }
	}

	/// <summary>A service provider that is its own only service: the names that are not allowed.</summary>
	sealed class BannedNames(params string[] names) : IServiceProvider
	{
		public bool Contains(string? name) => name is not null && names.Contains(name, StringComparer.Ordinal);

		public object? GetService(Type serviceType) => serviceType == typeof(BannedNames) ? this : null;
	}

	/// <summary>Fails a banned name, which it can only know through the services it is given.</summary>
	sealed class ServiceAwareValidator : IZodSchemaValidator<Person>
	{
		public const string BannedCode = "banned";

		public ValidationResult<Person> Validate(Person value) => ValidationResult<Person>.Success(value);

		public ValueTask<ValidationResult<Person>> ValidateAsync(
			Person value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));

		public ValueTask<ValidationResult<Person>> ValidateAsync(
			Person value,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			new(
				services?.GetService(typeof(BannedNames)) is BannedNames banned && banned.Contains(value.Name)
					? ValidationResult<Person>.Failure(new ValidationError(BannedCode, "Name is banned"))
					: ValidationResult<Person>.Success(value)
			);
	}
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SystemTextJsonServiceAwareTests.Person))]
sealed partial class ServiceAwareJsonContext : JsonSerializerContext;

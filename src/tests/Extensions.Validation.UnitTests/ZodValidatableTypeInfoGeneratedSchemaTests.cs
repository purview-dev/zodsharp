using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Extensions.Validation.Fixtures;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// End to end over validators produced by the ZodSharp source generator.
/// </summary>
public class ZodValidatableTypeInfoGeneratedSchemaTests
{
	[Test]
	public async Task ValidateAsync_GivenGeneratedSchema_PlacesEveryErrorOnItsOwningObject(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		GeneratedPerson person = new()
		{
			Name = "ab",
			Address = new() { Street = null },
			PreviousAddresses = [new() { Street = "1 High St" }, new() { Street = null }],
		};
		using var provider = CreatePersonProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<GeneratedPerson>().ValidateAsync(person, context, cancellationToken);

		// Assert
		var errors = context.All();
		var name = errors.Single(static e => e.Path == "Name");
		await Assert.That(name.Name).IsEqualTo("Name");
		await Assert.That(name.Container).IsSameReferenceAs(person);

		var street = errors.Single(static e => e.Path == "Address.Street");
		await Assert.That(street.Name).IsEqualTo("Street");
		await Assert.That(street.Container).IsSameReferenceAs(person.Address);

		var previous = errors.Single(static e => e.Path == "PreviousAddresses[1].Street");
		await Assert.That(previous.Name).IsEqualTo("Street");
		await Assert.That(previous.Container).IsSameReferenceAs(person.PreviousAddresses[1]);
		await Assert.That(errors.Length).IsEqualTo(3);
	}

	[Test]
	public async Task ValidateAsync_GivenValidGeneratedSchema_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		GeneratedPerson person = new()
		{
			Name = "abc",
			Address = new() { Street = "1 High St" },
		};
		using var provider = CreatePersonProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<GeneratedPerson>().ValidateAsync(person, context, cancellationToken);

		// Assert
		await Assert.That(context.ValidationErrors).IsNull();
	}

	[Test]
	public async Task ValidateAsync_GivenErrorInsideAStructMember_AttributesItToTheReferenceTypeOwner(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a struct cannot own a field (Blazor's FieldIdentifier rejects a value-type model, and a boxed copy is
		// not the instance a form binds), so the error belongs to the class that holds the struct.
		GeneratedDelivery delivery = new()
		{
			Postcode = new() { Value = "abc" },
			Alternatives = [new() { Value = "12345" }, new() { Value = "xyz" }],
		};
		ServiceCollection services = new();
		services.AddZodSharpFactory(static factory => factory.Register(new GeneratedDeliverySchemaValidator()));
		services.AddZodSharpValidation();
		using var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<GeneratedDelivery>().ValidateAsync(delivery, context, cancellationToken);

		// Assert
		var errors = context.All();
		var postcode = errors.Single(static e => e.Path == "Postcode.Value");
		await Assert.That(postcode.Container).IsSameReferenceAs(delivery);
		await Assert.That(postcode.Name).IsEqualTo(nameof(GeneratedDelivery.Postcode));

		var alternative = errors.Single(static e => e.Path == "Alternatives[1].Value");
		await Assert.That(alternative.Container).IsSameReferenceAs(delivery);
		await Assert.That(alternative.Name).IsEqualTo(nameof(GeneratedDelivery.Alternatives));
		await Assert.That(errors.Length).IsEqualTo(2);
	}

	static ServiceProvider CreatePersonProvider()
	{
		ServiceCollection services = new();
		// Registered through the generic overload: until generated validators implement IZodUntypedValidator,
		// assembly scanning (the non-generic Register) does not make them resolvable by runtime type.
		services.AddZodSharpFactory(static factory => factory.Register(new GeneratedPersonSchemaValidator()));
		services.AddZodSharpValidation();
		return services.BuildServiceProvider();
	}
}

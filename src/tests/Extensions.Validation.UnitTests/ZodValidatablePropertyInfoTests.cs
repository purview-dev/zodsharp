using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace ZodSharp.Extensions.Validation;

public class ZodValidatablePropertyInfoTests
{
	[Test]
	public async Task TryFindProperty_GivenValidatableMember_ReturnsPropertyInfo()
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(CreateValidator()));
		var info = provider.GetTypeInfo<Customer>();

		// Act
		var found = info.TryFindProperty("Email", provider.GetValidationOptions(), out var property);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(((ZodValidatablePropertyInfo)property!).MemberName).IsEqualTo("Email");
	}

	[Test]
	public async Task TryFindProperty_GivenMemberWithoutOwnRules_ReturnsFalse()
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(CreateValidator()));
		var info = provider.GetTypeInfo<Customer>();

		// Act
		var found = info.TryFindProperty("Address", provider.GetValidationOptions(), out var property);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(property).IsNull();
	}

	[Test]
	public async Task TryFindProperty_GivenValidatorWithoutMemberValidation_ReturnsFalse()
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(new ScriptedValidator<Customer>()));
		var info = provider.GetTypeInfo<Customer>();

		// Act
		var found = info.TryFindProperty("Email", provider.GetValidationOptions(), out _);

		// Assert
		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task ValidateAsync_GivenInvalidMember_ReportsOnlyThatMembersErrors(CancellationToken cancellationToken)
	{
		// Arrange
		Customer customer = new();
		using var provider = CreateProvider(static factory => factory.Register(CreateValidator()));
		var context = provider.CreateContext();
		provider.GetTypeInfo<Customer>().TryFindProperty("Email", context.ValidationOptions, out var property);

		// Act
		await property!.ValidateAsync(customer, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Email");
		await Assert.That(error.Name).IsEqualTo("Email");
		await Assert.That(error.Container).IsSameReferenceAs(customer);
		await Assert.That(error.ErrorMessage).IsEqualTo("Email is required");
	}

	[Test]
	public async Task Validate_GivenInvalidMember_ReportsSynchronously()
	{
		// Arrange
		Customer customer = new();
		using var provider = CreateProvider(static factory => factory.Register(CreateValidator()));
		var context = provider.CreateContext();
		provider.GetTypeInfo<Customer>().TryFindProperty("Email", context.ValidationOptions, out var property);

		// Act
		property!.Validate(customer, context);

		// Assert
		await Assert.That(context.Single().Path).IsEqualTo("Email");
	}

	[Test]
	public async Task ValidateAsync_GivenValidMember_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(CreateValidator()));
		var context = provider.CreateContext();
		provider.GetTypeInfo<Customer>().TryFindProperty("Email", context.ValidationOptions, out var property);

		// Act
		await property!.ValidateAsync(new Customer { Email = "a@b.c" }, context, cancellationToken);

		// Assert
		await Assert.That(context.ValidationErrors).IsNull();
	}

	static MemberAwareValidator<Customer> CreateValidator()
	{
		MemberAwareValidator<Customer> validator = new(
			static _ => [Error("custom", "Whole-object error")],
			static (customer, member) =>
				member == "Email" && customer.Email is null
					? [Error("too_small", "Email is required", M("Email"))]
					: [],
			static (customer, path) => path.Length == 1 ? (customer, path[0].Name!) : null
		);
		validator.ValidatableMembers.Add("Email");
		return validator;
	}
}

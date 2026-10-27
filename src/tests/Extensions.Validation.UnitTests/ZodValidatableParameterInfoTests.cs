using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace ZodSharp.Extensions.Validation;

public class ZodValidatableParameterInfoTests
{
	[Test]
	public async Task ValidateAsync_GivenComplexParameter_DoesNotPrefixWithParameterName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new();
		var (provider, info) = Create("order", Error("too_small", "Street", M("Customer"), M("Address"), M("Street")));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		await info.ValidateAsync(order, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Customer.Address.Street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer.Address);
	}

	[Test]
	public async Task ValidateAsync_GivenRootError_KeysByParameterNameWithoutContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (provider, info) = Create("order", Error("custom", "Order is invalid"));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		await info.ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("order");
		await Assert.That(error.Name).IsEqualTo("order");
		await Assert.That(error.Container).IsNull();
		await Assert.That(context.ZodReport!.Errors[0].FullPath.ToDottedString()).IsEqualTo("order");
	}

	[Test]
	public async Task ValidateAsync_GivenNullRequiredParameter_ReportsNullValueKeyedByParameterName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (provider, info) = Create("order", Error("custom", "Should not run"));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		await info.ValidateAsync(null, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("order");
		await Assert.That(error.Name).IsEqualTo("order");
		await Assert.That(error.Container).IsNull();
		var reported = context.ZodReport!.Errors.Single();
		await Assert.That(reported.Code).IsEqualTo(ZodIssueCodes.InvalidType);
		await Assert.That(error.ErrorMessage).IsEqualTo(reported.Message);
	}

	[Test]
	public async Task ValidateAsync_GivenNullOptionalParameter_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		var (provider, info) = Create("optional", Error("invalid_type", "Should not run"));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		await info.ValidateAsync(null, context, cancellationToken);

		// Assert
		await Assert.That(context.ValidationErrors).IsNull();
	}

	[Test]
	public async Task ValidateAsync_GivenCollectionParameter_PrefixesElementsWithParameterNameAndIndex(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		List<Order> orders = [new(), new()];
		var (provider, info) = Create("orders", Error("too_small", "Email", M("Customer"), M("Email")));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		await info.ValidateAsync(orders, context, cancellationToken);

		// Assert
		var errors = context.All();
		string[] paths = [.. errors.Select(static e => e.Path).Order(StringComparer.Ordinal)];
		await Assert.That(paths).IsEquivalentTo(["orders[0].Customer.Email", "orders[1].Customer.Email"]);
		var second = errors.Single(static e => e.Path == "orders[1].Customer.Email");
		await Assert.That(second.Container).IsSameReferenceAs(orders[1].Customer);
		await Assert.That(context.CurrentValidationPath).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task Validate_GivenCollectionParameter_ValidatesSynchronously()
	{
		// Arrange
		var (provider, info) = Create("orders", Error("too_small", "Email", M("Customer"), M("Email")));
		using var _ = provider;
		var context = provider.CreateContext();

		// Act
		info.Validate(new List<Order> { new() }, context);

		// Assert
		await Assert.That(context.Single().Path).IsEqualTo("orders[0].Customer.Email");
	}

	static (Microsoft.Extensions.DependencyInjection.ServiceProvider Provider, IValidatableParameterInfo Info) Create(
		string parameter,
		ZodSharp.Core.ValidationError error
	)
	{
		var provider = CreateProvider(factory => factory.Register(new ScriptedValidator<Order>(error)));
		if (!provider.GetValidationOptions().TryGetValidatableParameterInfo(Parameter(parameter), out var info))
			throw new InvalidOperationException($"Parameter '{parameter}' was not claimed.");

		return (provider, info);
	}

	static ParameterInfo Parameter(string name) =>
		typeof(ZodValidatableParameterInfoTests)
			.GetMethod(nameof(Handler), BindingFlags.NonPublic | BindingFlags.Static)!
			.GetParameters()
			.Single(p => p.Name == name);

	[SuppressMessage("Usage", "IDE0060:Remove unused parameter", Justification = "Parameter metadata fixture.")]
	[SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Read by reflection.")]
	static void Handler(Order order, List<Order> orders, Order? optional = null) { }
}

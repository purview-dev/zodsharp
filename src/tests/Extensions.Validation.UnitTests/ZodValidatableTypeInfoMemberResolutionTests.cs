using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

public class ZodValidatableTypeInfoMemberResolutionTests
{
	[Test]
	public async Task ValidateAsync_GivenNestedMemberError_ResolvesOwningObjectAndLeafName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new();

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Street is required", M("Customer"), M("Address"), M("Street")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("Customer.Address.Street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer.Address);
		await Assert.That(error.ErrorMessage).IsEqualTo("Street is required");
	}

	[Test]
	public async Task ValidateAsync_GivenListElementError_ResolvesElementAsContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { Lines = [new(), new()] };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("Lines"), I(1), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("Lines[1].Sku");
		await Assert.That(error.Name).IsEqualTo("Sku");
		await Assert.That(error.Container).IsSameReferenceAs(order.Lines[1]);
	}

	[Test]
	public async Task ValidateAsync_GivenArrayElementError_ResolvesElementAsContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { LineArray = [new(), new()] };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("LineArray"), I(0), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("LineArray[0].Sku");
		await Assert.That(error.Container).IsSameReferenceAs(order.LineArray[0]);
	}

	[Test]
	public async Task ValidateAsync_GivenEnumerableElementError_ResolvesElementAsContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		OrderLine first = new();
		OrderLine second = new();
		Order order = new() { LineSequence = Yield(first, second) };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("LineSequence"), I(1), M("Quantity")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("LineSequence[1].Quantity");
		await Assert.That(error.Name).IsEqualTo("Quantity");
		await Assert.That(error.Container).IsSameReferenceAs(second);

		static IEnumerable<OrderLine> Yield(OrderLine a, OrderLine b)
		{
			yield return a;
			yield return b;
		}
	}

	[Test]
	public async Task ValidateAsync_GivenHashSetElementError_ResolvesElementInEnumerationOrder(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		OrderLine line = new();
		Order order = new() { LineSet = [line] };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("LineSet"), I(0), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("LineSet[0].Sku");
		await Assert.That(error.Container).IsSameReferenceAs(line);
	}

	[Test]
	public async Task ValidateAsync_GivenStringKeyedDictionaryError_ResolvesEntryAsContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new()
		{
			ByCode = new() { ["abc"] = new(), ["def"] = new() },
		};

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("ByCode"), K("def"), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("ByCode[def].Sku");
		await Assert.That(error.Name).IsEqualTo("Sku");
		await Assert.That(error.Container).IsSameReferenceAs(order.ByCode["def"]);
	}

	[Test]
	public async Task ValidateAsync_GivenIntKeyedDictionaryError_ResolvesEntryByInvariantKey(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new()
		{
			ById = new() { [7] = new(), [1000] = new() },
		};

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("ById"), K(1000), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("ById[\"1000\"].Sku");
		await Assert.That(error.Container).IsSameReferenceAs(order.ById[1000]);
	}

	[Test]
	public async Task ValidateAsync_GivenPathEndingInIndex_ResolvesOwnerOfCollection(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { Lines = [new()] };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("invalid_type", "Null line", M("Lines"), I(0)),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("Lines[0]");
		await Assert.That(error.Name).IsEqualTo("Lines");
		await Assert.That(error.Container).IsSameReferenceAs(order);
	}

	[Test]
	public async Task ValidateAsync_GivenNullableValueTypeMemberError_ResolvesRootAsContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { Quantity = null };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("invalid_type", "Quantity is required", M("Quantity")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("Quantity");
		await Assert.That(error.Name).IsEqualTo("Quantity");
		await Assert.That(error.Container).IsSameReferenceAs(order);
	}

	[Test]
	public async Task ValidateAsync_GivenRootError_ReportsModelLevelError(CancellationToken cancellationToken)
	{
		// Arrange
		Order order = new();

		// Act
		var (error, _) = await ValidateOrderAsync(order, Error("custom", "Order is invalid"), cancellationToken);

		// Assert
		await Assert.That(error.Path).IsEqualTo(string.Empty);
		await Assert.That(error.Name).IsEqualTo(string.Empty);
		await Assert.That(error.Container).IsSameReferenceAs(order);
	}

	[Test]
	public async Task ValidateAsync_GivenIndexPastEndOfCollection_DegradesToNearestAncestor(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { Lines = [new()] };

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("too_small", "Sku", M("Lines"), I(5), M("Sku")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Path).IsEqualTo("Lines[5].Sku");
		await Assert.That(error.Name).IsEqualTo("Lines");
		await Assert.That(error.Container).IsSameReferenceAs(order);
	}

	[Test]
	public async Task ValidateAsync_GivenSyntheticMemberPath_KeepsMemberNameOnNearestObject(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new();

		// Act
		var (error, _) = await ValidateOrderAsync(
			order,
			Error("custom", "Totals mismatch", M("Customer"), M("Totals")),
			cancellationToken
		);

		// Assert
		await Assert.That(error.Name).IsEqualTo("Totals");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer);
	}

	[Test]
	public async Task ValidateAsync_GivenValidatorWithMemberResolver_UsesResolverResult(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		object sentinel = new();
		MemberAwareValidator<Order> validator = new(
			static _ => [Error("too_small", "Street", M("Customer"), M("Address"), M("Street"))],
			static (_, _) => [],
			(_, _) => (sentinel, "Resolved")
		);
		using var provider = CreateProvider(factory => factory.Register(validator));
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Container).IsSameReferenceAs(sentinel);
		await Assert.That(error.Name).IsEqualTo("Resolved");
	}

	[Test]
	public async Task ValidateAsync_GivenDictionaryBackedRuntimeSchema_ReportsFullPathWithoutContainer(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.String().Min(3)).Build();
		using var provider = CreateProvider(factory =>
			factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(schema))
		);
		var context = provider.CreateContext();
		Dictionary<string, object?> model = new() { ["name"] = "ab" };

		// Act
		await provider.GetTypeInfo<Dictionary<string, object?>>().ValidateAsync(model, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("name");
		await Assert.That(error.Name).IsEqualTo("name");
		await Assert.That(error.Container).IsNull();
	}

	static async Task<(
		Microsoft.Extensions.Validation.ValidationError Error,
		ValidateContext Context
	)> ValidateOrderAsync(Order order, ZodValidationError error, CancellationToken cancellationToken)
	{
		using var provider = CreateProvider(factory => factory.Register(new ScriptedValidator<Order>(error)));
		var context = provider.CreateContext();
		await provider.GetTypeInfo<Order>().ValidateAsync(order, context, cancellationToken);
		return (context.Single(), context);
	}
}

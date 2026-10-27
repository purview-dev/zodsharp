using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Extensions.Validation;
using static ZodSharp.Blazor.Fixtures.Errors;

namespace ZodSharp.Blazor;

/// <summary>
/// Route 1: the standard <c>&lt;DataAnnotationsValidator /&gt;</c> with <c>AddZodSharpBlazor()</c> and nothing else.
/// </summary>
public class DataAnnotationsValidatorRouteTests
{
	[Test]
	public async Task ValidateAsync_GivenNestedCollectionAndDictionaryErrors_PlacesEachOnItsOwningField()
	{
		// Arrange
		Order order = new()
		{
			Lines = [new(), new()],
			ByCode = new() { ["a"] = new(), ["b"] = new() },
		};
		FakeValidator<Order> validator = new()
		{
			Errors = static _ =>
				[
					At("invalid_string", "Email is invalid", M("Customer"), M("Email")),
					At("too_small", "Quantity too small", M("Lines"), I(1), M("Quantity")),
					At("too_small", "Sku required", M("ByCode"), K("b"), M("Sku")),
					At("custom", "Order is invalid"),
				],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(valid).IsFalse();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Customer, "Email")))
			.IsEquivalentTo(["Email is invalid"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Lines[1], "Quantity")))
			.IsEquivalentTo(["Quantity too small"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.ByCode["b"], "Sku")))
			.IsEquivalentTo(["Sku required"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, string.Empty)))
			.IsEquivalentTo(["Order is invalid"]);
		await Assert.That(editContext.GetValidationMessages(new FieldIdentifier(order.Lines[0], "Quantity"))).IsEmpty();
	}

	[Test]
	public async Task FieldChange_GivenMemberValidator_RunsOnlyThatMembersRules()
	{
		// Arrange
		Order order = new();
		FakeValidator<Customer> customer = new()
		{
			Errors = static _ => [At("custom", "nested leak", M("ConfirmEmail"))],
			MemberErrors = static (_, member) =>
				member == nameof(Customer.Email) ? [At("invalid_string", "Email is invalid", M("Email"))] : [],
		};
		FakeValidator<Order> root = new() { Errors = static _ => [At("custom", "root leak", M("Reference"))] };
		await using var context = FormHost.CreateContext(factory =>
		{
			factory.Register(root);
			factory.Register(customer);
		});
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());
		FieldIdentifier email = new(order.Customer, nameof(Customer.Email));

		// Act
		await form.ChangeAsync(editContext, email);

		// Assert
		await Assert.That(editContext.GetValidationMessages(email)).IsEquivalentTo(["Email is invalid"]);
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Email is invalid"]);
		await Assert.That(customer.MemberCalls).IsEquivalentTo([nameof(Customer.Email)]);
		await Assert.That(customer.Calls).IsEqualTo(0);
		await Assert.That(root.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task FieldChange_GivenAsyncMemberRule_IsPendingUntilItCompletes()
	{
		// Arrange
		Order order = new();
		TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		FakeValidator<Order> validator = new()
		{
			MemberErrors = static (_, _) => [At("custom", "Reference taken", M("Reference"))],
			MemberGate = (_, _) => release.Task,
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);
		var pending = editContext.IsValidationPending(reference);
		release.SetResult();

		// Assert
		await Assert.That(pending).IsTrue();
		await FormHost.WaitUntilAsync(() => !editContext.IsValidationPending(reference), "still pending");
		await Assert.That(editContext.GetValidationMessages(reference)).IsEquivalentTo(["Reference taken"]);
		await Assert.That(editContext.IsValidationFaulted(reference)).IsFalse();
	}

	[Test]
	public async Task FieldChange_GivenThrowingMemberRule_MarksFieldFaulted()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			MemberErrors = static (_, _) => [],
			MemberGate = static (_, _) => Task.FromException(new InvalidOperationException("boom")),
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);

		// Assert
		await FormHost.WaitUntilAsync(() => editContext.IsValidationFaulted(reference), "not faulted");
		await Assert.That(editContext.IsValidationPending(reference)).IsFalse();
	}

	[Test]
	public async Task ValidateAsync_GivenStringLocalizer_LocalizesMessagesByCode()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services =>
				services.AddSingleton<IStringLocalizerFactory>(
					new FakeStringLocalizerFactory(new() { ["too_small"] = "Trop court" })
				)
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(Order.Reference))))
			.IsEquivalentTo(["Trop court"]);
	}

	[Test]
	public async Task AddZodSharpBlazor_GivenServices_RegistersNoValidationReportSink()
	{
		// Arrange
		ServiceCollection services = new();

		// Act
		services.AddZodSharpBlazor();

		// Assert
		await Assert.That(services.Any(static d => d.ServiceType == typeof(IZodValidationReportSink))).IsFalse();
		await Assert.That(services.Any(static d => d.ServiceType == typeof(ZodValidatableInfoResolver))).IsTrue();
		await Assert.That(services.Any(static d => d.ServiceType == typeof(TimeProvider))).IsTrue();
	}
}

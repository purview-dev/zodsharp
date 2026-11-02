using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Extensions.Validation;
using static ZodSharp.Blazor.Fixtures.Errors;

namespace ZodSharp.Blazor;

/// <summary>
/// Route 2: <c>&lt;ZodValidator /&gt;</c>.
/// </summary>
public class ZodValidatorTests
{
	[Test]
	public async Task ValidateAsync_GivenErrors_PlacesMessagesAndKeepsFullErrors()
	{
		// Arrange
		Order order = new() { Lines = [new(), new()] };
		var lineError = At("too_small", "Quantity too small", M("Lines"), I(1), M("Quantity"));
		FakeValidator<Order> validator = new()
		{
			Errors = _ => [At("invalid_string", "Email is invalid", M("Customer"), M("Email")), lineError],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(valid).IsFalse();
		FieldIdentifier quantity = new(order.Lines[1], nameof(OrderLine.Quantity));
		await Assert.That(editContext.GetValidationMessages(quantity)).IsEquivalentTo(["Quantity too small"]);
		await Assert.That(editContext.GetZodErrors(quantity)).IsEquivalentTo([lineError]);
		await Assert
			.That(editContext.GetZodErrors(() => order.Customer.Email).Single().Code)
			.IsEqualTo("invalid_string");
		await Assert.That(editContext.GetZodErrors().Count).IsEqualTo(2);
		await Assert.That(validator.ReceivedServices).IsNotNull();
	}

	[Test]
	public async Task ValidateAsync_GivenFixedErrors_ReplacesPreviousRun()
	{
		// Arrange
		Order order = new();
		var fail = true;
		FakeValidator<Order> validator = new()
		{
			Errors = _ => fail ? [At("too_small", "Reference too short", M("Reference"))] : [],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);
		fail = false;

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(valid).IsTrue();
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
		await Assert.That(editContext.GetZodErrors()).IsEmpty();
	}

	[Test]
	public async Task FieldChange_GivenSubmitTrigger_DoesNotValidate()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			MemberErrors = static (_, _) => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(ZodValidationTrigger.Submit));

		// Act
		await form.ChangeAsync(editContext, new FieldIdentifier(order, nameof(Order.Reference)));

		// Assert
		await Assert.That(validator.MemberCalls).IsEmpty();
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
	}

	[Test]
	public async Task FieldChange_GivenChangeTrigger_ValidatesOnlyTheChangedMember()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("custom", "should not run", M("Customer"), M("Email"))],
			MemberErrors = static (_, member) => [At("too_small", $"{member} too short", M(member))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(ZodValidationTrigger.Change));
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);

		// Assert
		await Assert.That(validator.MemberCalls).IsEquivalentTo([nameof(Order.Reference)]);
		await Assert.That(validator.Calls).IsEqualTo(0);
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Reference too short"]);
		await Assert.That(editContext.GetZodErrors(reference).Single().Code).IsEqualTo("too_small");
	}

	[Test]
	public async Task FieldChange_GivenInputTriggerWithDebounce_ValidatesOnlyAfterTheDelay()
	{
		// Arrange
		Order order = new();
		ManualTimeProvider time = new();
		FakeValidator<Order> validator = new()
		{
			MemberErrors = static (_, _) => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services => services.AddSingleton<TimeProvider>(time)
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(ZodValidationTrigger.Input, debounceMilliseconds: 300));
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);
		var callsBeforeDelay = validator.MemberCalls.Count;
		var pendingDuringDelay = editContext.IsValidationPending(reference);
		time.Advance(TimeSpan.FromMilliseconds(299));
		var callsJustBefore = validator.MemberCalls.Count;
		time.Advance(TimeSpan.FromMilliseconds(1));
		await FormHost.WaitUntilAsync(() => !editContext.IsValidationPending(reference), "debounced validation");

		// Assert
		await Assert.That(callsBeforeDelay).IsEqualTo(0);
		await Assert.That(pendingDuringDelay).IsTrue();
		await Assert.That(callsJustBefore).IsEqualTo(0);
		await Assert.That(validator.MemberCalls.Count).IsEqualTo(1);
		await Assert.That(editContext.GetValidationMessages(reference)).IsEquivalentTo(["Reference too short"]);
	}

	[Test]
	public async Task FieldChange_GivenBurstOfInputChanges_ValidatesOnce()
	{
		// Arrange
		Order order = new();
		ManualTimeProvider time = new();
		FakeValidator<Order> validator = new() { MemberErrors = static (_, _) => [] };
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services => services.AddSingleton<TimeProvider>(time)
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(ZodValidationTrigger.Input, debounceMilliseconds: 250));
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);
		time.Advance(TimeSpan.FromMilliseconds(100));
		await form.ChangeAsync(editContext, reference);
		time.Advance(TimeSpan.FromMilliseconds(100));
		await form.ChangeAsync(editContext, reference);
		time.Advance(TimeSpan.FromMilliseconds(250));
		await FormHost.WaitUntilAsync(() => !editContext.IsValidationPending(reference), "debounced validation");

		// Assert
		await Assert.That(validator.MemberCalls.Count).IsEqualTo(1);
	}

	[Test]
	public async Task FieldChange_GivenNewerChangeWhileValidating_CancelsAndIgnoresTheSupersededRun()
	{
		// Arrange
		Order order = new();
		TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
		var runs = 0;
		FakeValidator<Order> validator = new()
		{
			MemberErrors = (value, _) => [At("custom", $"run for '{value.Reference}'", M("Reference"))],
			MemberGate = (_, cancellationToken) =>
				Interlocked.Increment(ref runs) == 1 ? first.Task.WaitAsync(cancellationToken) : Task.CompletedTask,
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		order.Reference = "a";
		await form.ChangeAsync(editContext, reference);
		order.Reference = "ab";
		await form.ChangeAsync(editContext, reference);
		first.TrySetResult();
		await FormHost.WaitUntilAsync(() => !editContext.IsValidationPending(reference), "second run");

		// Assert
		await Assert.That(validator.MemberTokens[0].IsCancellationRequested).IsTrue();
		await Assert.That(editContext.GetValidationMessages(reference)).IsEquivalentTo(["run for 'ab'"]);
		await Assert.That(editContext.IsValidationFaulted(reference)).IsFalse();
	}

	[Test]
	public async Task FieldChange_GivenAsyncMemberRule_ReportsPendingThenResolves()
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
		var form = context.Render(editContext, FormHost.Zod());
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);
		var pending = editContext.IsValidationPending(reference);
		release.SetResult();
		await FormHost.WaitUntilAsync(() => !editContext.IsValidationPending(reference), "resolved");

		// Assert
		await Assert.That(pending).IsTrue();
		await Assert.That(editContext.GetValidationMessages(reference)).IsEquivalentTo(["Reference taken"]);
	}

	[Test]
	public async Task FieldChange_GivenThrowingValidator_MarksFaultedAndLogs()
	{
		// Arrange
		Order order = new();
		using CapturingLoggerProvider logs = new();
		FakeValidator<Order> validator = new()
		{
			MemberErrors = static (_, _) => [],
			MemberGate = static (_, _) => Task.FromException(new InvalidOperationException("boom")),
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services => services.AddLogging(builder => builder.AddProvider(logs))
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		FieldIdentifier reference = new(order, nameof(Order.Reference));

		// Act
		await form.ChangeAsync(editContext, reference);
		await FormHost.WaitUntilAsync(() => editContext.IsValidationFaulted(reference), "faulted");

		// Assert
		var entry = logs.Entries.Single(static e => e.Category == typeof(ZodValidator).FullName);
		await Assert.That(entry.Level).IsEqualTo(LogLevel.Error);
		await Assert.That(entry.Message).Contains(nameof(Order.Reference));
		await Assert.That(entry.Exception).IsTypeOf<InvalidOperationException>();
	}

	[Test]
	public async Task FieldChange_GivenNoMemberValidation_KeepsOnlyTheChangedFieldsErrors()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ =>
				[
					At("too_small", "Reference too short", M("Reference")),
					At("invalid_string", "Email is invalid", M("Customer"), M("Email")),
				],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ChangeAsync(editContext, new FieldIdentifier(order.Customer, nameof(Customer.Email)));

		// Assert
		await Assert.That(validator.Calls).IsEqualTo(1);
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Email is invalid"]);
	}

	[Test]
	public async Task FieldChange_GivenOtherFieldsMessages_LeavesThemInPlace()
	{
		// Arrange
		Order order = new();
		var email = true;
		FakeValidator<Order> validator = new()
		{
			Errors = _ =>
				email
					?
					[
						At("too_small", "Reference too short", M("Reference")),
						At("invalid_string", "Email is invalid", M("Customer"), M("Email")),
					]
					: [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);
		email = false;

		// Act
		await form.ChangeAsync(editContext, new FieldIdentifier(order.Customer, nameof(Customer.Email)));

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Reference too short"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(Order.Reference))))
			.IsEquivalentTo(["Reference too short"]);
	}

	[Test]
	public async Task FieldChange_GivenValidateNestedOnFieldChange_UpdatesTheWholeOwningObject()
	{
		// Arrange
		Order order = new();
		FakeValidator<Customer> customer = new()
		{
			Errors = static value =>
				value.Email == value.ConfirmEmail ? [] : [At("custom", "Emails differ", M("ConfirmEmail"))],
			MemberErrors = static (_, _) => [],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(customer));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(schema: null, validateNestedOnFieldChange: true));
		FieldIdentifier email = new(order.Customer, nameof(Customer.Email));
		FieldIdentifier confirm = new(order.Customer, nameof(Customer.ConfirmEmail));

		// Act
		order.Customer.Email = "a@example.com";
		await form.ChangeAsync(editContext, email);
		var afterMismatch = editContext.GetValidationMessages(confirm).ToArray();
		order.Customer.ConfirmEmail = "a@example.com";
		await form.ChangeAsync(editContext, email);

		// Assert
		await Assert.That(afterMismatch).IsEquivalentTo(["Emails differ"]);
		await Assert.That(editContext.GetValidationMessages(confirm)).IsEmpty();
		await Assert.That(customer.MemberCalls).IsEmpty();
	}

	[Test]
	public async Task FieldChange_GivenNoNestedValidation_DoesNotTouchSiblingFields()
	{
		// Arrange
		Order order = new();
		FakeValidator<Customer> customer = new()
		{
			Errors = static _ => [At("custom", "Emails differ", M("ConfirmEmail"))],
			MemberErrors = static (_, _) => [],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(customer));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ChangeAsync(editContext, new FieldIdentifier(order.Customer, nameof(Customer.Email)));

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
		await Assert.That(customer.MemberCalls).IsEquivalentTo([nameof(Customer.Email)]);
	}

	[Test]
	public async Task ValidateAsync_GivenRuntimeObjectSchema_PlacesErrorsOnTheClrFields()
	{
		// Arrange
		Order order = new()
		{
			Reference = "ab",
			Customer = new() { Email = "not-an-email" },
		};
		var schema = Z.Object()
			.Field("Reference", Z.String().Min(3))
			.Field("Customer", Z.Object().Field("Email", Z.String().Email()).Build())
			.Build();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(schema: ZodModelSchema.For<Order>(schema)));

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(valid).IsFalse();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(Order.Reference))))
			.IsNotEmpty();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Customer, nameof(Customer.Email))))
			.IsNotEmpty();
		await Assert.That(editContext.GetZodErrors(() => order.Reference).Single().Category).IsEqualTo("too_small");
	}

	[Test]
	public async Task FieldChange_GivenRuntimeObjectSchema_UpdatesOnlyTheChangedField()
	{
		// Arrange
		Order order = new()
		{
			Reference = "ab",
			Customer = new() { Email = "not-an-email" },
		};
		var schema = Z.Object()
			.Field("Reference", Z.String().Min(3))
			.Field("Customer", Z.Object().Field("Email", Z.String().Email()).Build())
			.Build();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod(schema: ZodModelSchema.For<Order>(schema)));

		// Act
		await form.ChangeAsync(editContext, new FieldIdentifier(order, nameof(Order.Reference)));

		// Assert
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(Order.Reference))))
			.IsNotEmpty();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Customer, nameof(Customer.Email))))
			.IsEmpty();
	}

	[Test]
	public async Task Render_GivenDataAnnotationsValidator_ShowsEachMessageOnceAndWarnsOnce()
	{
		// Arrange
		Order order = new();
		using CapturingLoggerProvider logs = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services => services.AddLogging(builder => builder.AddProvider(logs))
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Both(FormHost.DataAnnotations(), FormHost.Zod()));

		// Act
		await form.ValidateAsync(editContext);
		await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Reference too short"]);
		await Assert.That(editContext.GetZodErrors(() => order.Reference).Single().Code).IsEqualTo("too_small");
		await Assert.That(logs.Entries.Count(static e => e.Level == LogLevel.Warning)).IsEqualTo(1);
	}

	[Test]
	public async Task Render_GivenZodValidatorFirst_StillDetectsDataAnnotationsValidator()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Both(FormHost.Zod(), FormHost.DataAnnotations()));

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Reference too short"]);
	}

	[Test]
	public async Task Render_GivenEnableClientValidation_SetsTheClientValidationKeyUntilDisposed()
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);

		// Act
		var form = context.Render(editContext, FormHost.Zod());
		var whileRendered = editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out _);
		await context.DisposeComponentsAsync();

		// Assert
		await Assert.That(whileRendered).IsTrue();
		await Assert.That(editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out _)).IsFalse();
	}

	[Test]
	public async Task Render_GivenClientValidationDisabled_DoesNotSetTheClientValidationKey()
	{
		// Arrange
		Order order = new();
		await using var context = FormHost.CreateContext();
		EditContext editContext = new(order);

		// Act
		context.Render(editContext, FormHost.Zod(enableClientValidation: false));

		// Assert
		await Assert.That(editContext.Properties.TryGetValue(typeof(DataAnnotationsValidator), out _)).IsFalse();
	}

	[Test]
	public async Task ValidateAsync_GivenRegisteredReportSink_LeavesItUntouched()
	{
		// Arrange
		Order order = new();
		RecordingSink sink = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			services => services.AddSingleton<IZodValidationReportSink>(sink)
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsNotEmpty();
		await Assert.That(sink.Calls).IsEmpty();
	}

	[Test]
	public async Task ClearZodErrors_GivenValidatorErrors_RemovesMessagesAndErrors()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		await form.ValidateAsync(editContext);

		// Act
		await form.InvokeAsync(editContext.ClearZodErrors);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
		await Assert.That(editContext.GetZodErrors()).IsEmpty();
	}

	[Test]
	public async Task ValidateAsync_GivenMessageFormatter_RendersMessagesThroughIt()
	{
		// Arrange
		Order order = new();
		FakeValidator<Order> validator = new()
		{
			Errors = static _ => [At("too_small", "Reference too short", M("Reference"))],
		};
		await using var context = FormHost.CreateContext(
			factory => factory.Register(validator),
			options: static o => o.MessageFormatter = new PrefixFormatter("!")
		);
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["!Reference too short"]);
	}

	sealed class PrefixFormatter(string prefix) : Core.IZodMessageFormatter
	{
		public string Format(in Core.ValidationError validationError, System.Globalization.CultureInfo culture) =>
			prefix + validationError.Message;
	}
}

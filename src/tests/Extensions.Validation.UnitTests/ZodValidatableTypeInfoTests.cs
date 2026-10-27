using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

public class ZodValidatableTypeInfoTests
{
	[Test]
	public async Task ValidateAsync_GivenCurrentValidationPath_PrefixesPath(CancellationToken cancellationToken)
	{
		// Arrange
		Order order = new();
		using var provider = CreateProvider(static factory =>
			factory.Register(
				new ScriptedValidator<Order>(Error("too_small", "Street", M("Customer"), M("Address"), M("Street")))
			)
		);
		var context = provider.CreateContext();
		context.CurrentValidationPath = "Batch.Orders[2]";

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(order, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Batch.Orders[2].Customer.Address.Street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer.Address);
		await Assert
			.That(context.ZodReport!.Errors[0].FullPath.ToDottedString())
			.IsEqualTo("Batch.Orders[2].Customer.Address.Street");
	}

	[Test]
	public async Task ValidateAsync_GivenNestedUnderDataAnnotationsWalker_PrefixesWithMemberPath(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory =>
			factory.Register(
				new ScriptedValidator<Order>(Error("too_small", "Street", M("Customer"), M("Address"), M("Street")))
			)
		);
		services.AddZodSharpValidation();
		using var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();
		Envelope envelope = new() { Title = "ok" };

		// Act
		await provider.GetTypeInfo<Envelope>().ValidateAsync(envelope, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Order.Customer.Address.Street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(envelope.Order.Customer.Address);
		await Assert.That(context.CurrentValidationPath).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task ValidateAsync_GivenMaxDepthReached_ThrowsLikeMicrosoftExtensionsValidation(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory => factory.Register(new ScriptedValidator<Order>()));
		services.AddZodSharpValidation();
		using var provider = services.BuildServiceProvider();
		var zodContext = provider.CreateContext();
		zodContext.CurrentDepth = zodContext.ValidationOptions.MaxDepth;
		var generatedContext = provider.CreateContext();
		generatedContext.CurrentDepth = generatedContext.ValidationOptions.MaxDepth;

		// Act
		var zod = await Assert
			.That(() => provider.GetTypeInfo<Order>().ValidateAsync(new Order(), zodContext, cancellationToken))
			.Throws<InvalidOperationException>();
		var generated = await Assert
			.That(() =>
				provider.GetTypeInfo<Envelope>().ValidateAsync(new Envelope(), generatedContext, cancellationToken)
			)
			.Throws<InvalidOperationException>();

		// Assert
		await Assert.That(zod!.Message).StartsWith("Maximum validation depth of 32 exceeded at '' in 'Order'.");
		await Assert
			.That(zod.Message.Replace("'Order'", "'Envelope'", StringComparison.Ordinal))
			.IsEqualTo(generated!.Message);
	}

	[Test]
	public async Task ValidateAsync_GivenDepthBelowMax_Validates(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static factory =>
			factory.Register(new ScriptedValidator<Order>(Error("custom", "Invalid")))
		);
		var context = provider.CreateContext();
		context.CurrentDepth = context.ValidationOptions.MaxDepth - 1;

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		await Assert.That(context.All().Length).IsEqualTo(1);
		await Assert.That(context.CurrentDepth).IsEqualTo(context.ValidationOptions.MaxDepth - 1);
	}

	[Test]
	public async Task Validate_GivenAsyncOnlyRules_Throws()
	{
		// Arrange
		using var provider = CreateProvider(static factory =>
			factory.Register(new AsyncOnlyValidator<Order>(Error("custom", "Taken")))
		);
		var context = provider.CreateContext();

		// Act
		var exception = await Assert
			.That(() => provider.GetTypeInfo<Order>().Validate(new Order(), context))
			.Throws<InvalidOperationException>();

		// Assert
		await Assert.That(exception!.Message).Contains(typeof(Order).FullName!);
		await Assert.That(context.ValidationErrors).IsNull();
	}

	[Test]
	public async Task ValidateAsync_GivenAsyncOnlyRules_AwaitsAndPassesServices(CancellationToken cancellationToken)
	{
		// Arrange
		AsyncOnlyValidator<Order> validator = new(Error("custom", "Taken", M("Reference")));
		using var provider = CreateProvider(factory => factory.Register(validator));
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		await Assert.That(context.Single().Path).IsEqualTo("Reference");
		await Assert.That(validator.ReceivedServices).IsSameReferenceAs(provider);
	}

	[Test]
	public async Task Validate_GivenSyncValidator_ReportsErrors()
	{
		// Arrange
		Order order = new();
		using var provider = CreateProvider(static factory =>
			factory.Register(new ScriptedValidator<Order>(Error("too_small", "Email", M("Customer"), M("Email"))))
		);
		var context = provider.CreateContext();

		// Act
		provider.GetTypeInfo<Order>().Validate(order, context);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Customer.Email");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer);
	}

	[Test]
	public async Task ValidateAsync_GivenValidValue_CompletesSynchronouslyWithoutErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(new ScriptedValidator<Order>()));
		var context = provider.CreateContext();

		// Act
		var task = provider.GetTypeInfo<Order>().ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		await Assert.That(task.IsCompletedSuccessfully).IsTrue();
		await Assert.That(context.ValidationErrors).IsNull();
		await Assert.That(context.ZodReport).IsNull();
	}

	[Test]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "TUnit's assertion builders count towards coupling; the test is one scenario."
	)]
	public async Task ValidateAsync_GivenErrors_ReportAndSinkReceiveIdenticalFullFidelityErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var original = ZodValidationError.Create(
			"too_small",
			"Sku must be 3 characters",
			ZodPath.From(M("Lines"), I(0), M("Sku")),
			parameters: new Dictionary<string, object?> { ["minimum"] = 3 },
			origin: "string",
			minimum: 3,
			category: ZodIssueCategories.TooSmall
		);
		RecordingSink sink = new();
		using var provider = CreateProvider(
			factory => factory.Register(new ScriptedValidator<Order>(original)),
			services => services.AddSingleton<IZodValidationReportSink>(sink)
		);
		var context = provider.CreateContext();
		context.CurrentValidationPath = "Root";

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order { Lines = [new()] }, context, cancellationToken);

		// Assert
		var report = context.ZodReport;
		await Assert.That(report).IsNotNull();
		await Assert.That(report!.HasErrors).IsTrue();
		var call = sink.Calls.Single();
		await Assert.That(call.Type).IsEqualTo(typeof(Order));
		await Assert.That(call.Prefix).IsEqualTo("Root");
		await Assert.That(call.Errors.SequenceEqual(report.Errors)).IsTrue();

		var reported = report.Errors.Single();
		await Assert.That(reported).IsEqualTo(original.WithPathPrefix(M("Root")));
		await Assert.That(reported.Code).IsEqualTo("too_small");
		await Assert.That(reported.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(reported.Origin).IsEqualTo("string");
		await Assert.That(reported.Minimum).IsEqualTo(3);
		await Assert.That(reported.FullPath.ToDottedString()).IsEqualTo(context.Single().Path);
	}

	[Test]
	public async Task ValidateAsync_GivenMessageFormatter_UsesIt(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(
			static factory =>
				factory.Register(new ScriptedValidator<Order>(Error("custom", "Invalid", M("Reference")))),
			options: static options => options.MessageFormatter = new FixedFormatter("formatted: ")
		);
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order(), context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("formatted: Invalid");
	}

	[Test]
	public async Task ValidateAsync_GivenPathNamer_RendersPathWithNamerButKeepsMemberName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new();
		using var provider = CreateProvider(
			static factory =>
				factory.Register(
					new ScriptedValidator<Order>(Error("too_small", "Street", M("Customer"), M("Address"), M("Street")))
				),
			options: static options =>
				options.PathNamer = ZodPathNamer.FromFunc(static name => char.ToLowerInvariant(name[0]) + name[1..])
		);
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(order, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("customer.address.street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer.Address);
	}

	[Test]
	public async Task ValidateAsync_GivenUnionWithoutValidator_FollowsActiveCaseUnderValue(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new();
		using var provider = CreateProvider(static factory =>
			factory.Register(
				new ScriptedValidator<Order>(Error("too_small", "Street", M("Customer"), M("Address"), M("Street")))
			)
		);
		var context = provider.CreateContext();
		Shipment shipment = order;

		// Act
		await provider.GetTypeInfo<Shipment>().ValidateAsync(shipment, context, cancellationToken);

		// Assert
		var error = context.Single();
		await Assert.That(error.Path).IsEqualTo("Value.Customer.Address.Street");
		await Assert.That(error.Name).IsEqualTo("Street");
		await Assert.That(error.Container).IsSameReferenceAs(order.Customer.Address);
		await Assert.That(context.CurrentValidationPath).IsEqualTo(string.Empty);
		await Assert.That(context.CurrentDepth).IsEqualTo(0);
	}

	[Test]
	public async Task ValidateAsync_GivenUnionCaseWithoutValidator_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(new ScriptedValidator<Order>()));
		var context = provider.CreateContext();
		Shipment shipment = new Address();

		// Act
		await provider.GetTypeInfo<Shipment>().ValidateAsync(shipment, context, cancellationToken);

		// Assert
		await Assert.That(context.ValidationErrors).IsNull();
	}

	[Test]
	public async Task ValidateAsync_GivenAlsoRunDataAnnotations_ReportsBothResolversErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory =>
			factory.Register(
				new ScriptedValidator<DualModel>(Error("custom", "ZodSharp: Name is required.", M("Name")))
			)
		);
		services.AddZodSharpValidation(static options => options.AlsoRunDataAnnotations = true);
		using var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<DualModel>().ValidateAsync(new DualModel(), context, cancellationToken);

		// Assert
		string[] messages = [.. context.All().Select(static e => e.ErrorMessage).Order(StringComparer.Ordinal)];
		await Assert
			.That(messages)
			.IsEquivalentTo(["DataAnnotations: Name is required.", "ZodSharp: Name is required."]);
	}

	[Test]
	public async Task ValidateAsync_GivenAlsoRunDataAnnotationsWithIdenticalMessage_ReportsItOnce(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory =>
			factory.Register(
				new ScriptedValidator<DualModel>(Error("custom", "DataAnnotations: Name is required.", M("Name")))
			)
		);
		services.AddZodSharpValidation(static options => options.AlsoRunDataAnnotations = true);
		using var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<DualModel>().ValidateAsync(new DualModel(), context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("DataAnnotations: Name is required.");
	}
}

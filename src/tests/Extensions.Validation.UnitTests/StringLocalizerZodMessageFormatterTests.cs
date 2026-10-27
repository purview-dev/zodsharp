using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

public class StringLocalizerZodMessageFormatterTests
{
	static readonly ZodValidationError MinLengthError = ZodValidationError.Create(
		"too_small",
		"Sku must be at least 3 characters",
		ZodPath.From(M("Lines"), I(0), M("Sku")),
		parameters: new Dictionary<string, object?> { ["minimum"] = 3, ["displayName"] = "Sku" }
	) with
	{
		MessageFormat = "{displayName} must be at least {minimum} characters",
	};

	[Test]
	public async Task Format_GivenMessageFormatKey_PrefersItOverEveryOtherKey()
	{
		// Arrange
		var formatter = CreateFormatter(
			new()
			{
				["{displayName} must be at least {minimum} characters"] = "explicit {minimum}",
				["OrderLine_Sku_too_small"] = "member",
				["OrderLine_too_small"] = "type",
				["too_small"] = "code",
			}
		);

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("explicit 3");
	}

	[Test]
	public async Task Format_GivenMemberKey_PrefersItOverTypeAndCodeKeys()
	{
		// Arrange
		var formatter = CreateFormatter(
			new()
			{
				["OrderLine_Sku_too_small"] = "member {displayName}",
				["OrderLine_too_small"] = "type",
				["too_small"] = "code",
			}
		);

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("member Sku");
	}

	[Test]
	public async Task Format_GivenTypeKey_PrefersItOverCodeKey()
	{
		// Arrange
		var formatter = CreateFormatter(new() { ["OrderLine_too_small"] = "type", ["too_small"] = "code" });

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("type");
	}

	[Test]
	public async Task Format_GivenOnlyCodeKey_UsesIt()
	{
		// Arrange
		var formatter = CreateFormatter(new() { ["too_small"] = "At least {minimum}" });

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("At least 3");
	}

	[Test]
	public async Task Format_GivenNoMatchingKey_ReturnsErrorMessage()
	{
		// Arrange
		var formatter = CreateFormatter([]);

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Sku must be at least 3 characters");
	}

	[Test]
	public async Task Format_GivenLiteralMessageKey_UsesMessageAsKey()
	{
		// Arrange
		var formatter = CreateFormatter(new() { ["Order is invalid"] = "Commande invalide" });

		// Act
		var message = formatter.Format(
			Error("custom", "Order is invalid"),
			typeof(Order),
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(message).IsEqualTo("Commande invalide");
	}

	[Test]
	public async Task Format_GivenDisplayNameKey_LocalizesDisplayName()
	{
		// Arrange
		var formatter = CreateFormatter(
			new() { ["too_small"] = "{displayName} trop court", ["OrderLine_Sku_DisplayName"] = "Référence" }
		);

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Référence trop court");
	}

	[Test]
	public async Task Format_GivenCulture_LooksUpInThatCulture()
	{
		// Arrange
		var formatter = CreateFormatter(new() { ["too_small"] = "too short", ["fr-FR:too_small"] = "trop court" });

		// Act
		var message = formatter.Format(MinLengthError, typeof(OrderLine), CultureInfo.GetCultureInfo("fr-FR"));

		// Assert
		await Assert.That(message).IsEqualTo("trop court");
	}

	[Test]
	public async Task Format_GivenUnknownDeclaringTypeAndNoSharedResource_ReturnsErrorMessage()
	{
		// Arrange
		var formatter = CreateFormatter(new() { ["too_small"] = "code" });

		// Act
		var message = formatter.Format(MinLengthError, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Sku must be at least 3 characters");
	}

	[Test]
	public async Task Format_GivenSharedResourceType_UsesItWhenDeclaringTypeIsUnknown()
	{
		// Arrange
		FakeStringLocalizerFactory factory = new(new() { ["too_small"] = "code" });
		StringLocalizerZodMessageFormatter formatter = new(factory, sharedResourceType: typeof(SharedResource));

		// Act
		var message = formatter.Format(MinLengthError, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("code");
		await Assert.That(factory.RequestedTypes).IsEquivalentTo([typeof(SharedResource)]);
	}

	[Test]
	public async Task ValidateAsync_GivenStringLocalizerFactoryRegistered_LocalizesWithOwningTypeKeys(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		FakeStringLocalizerFactory factory = new(new() { ["OrderLine_Sku_too_small"] = "Référence trop courte" });
		using var provider = CreateProvider(
			static zod => zod.Register(new ScriptedValidator<Order>(MinLengthError)),
			services => services.AddSingleton<IStringLocalizerFactory>(factory)
		);
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order { Lines = [new()] }, context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("Référence trop courte");
		await Assert.That(factory.RequestedTypes).Contains(typeof(OrderLine));
	}

	[Test]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Maintainability",
		"CA1506:Avoid excessive class coupling",
		Justification = "TUnit's assertion builders count towards coupling; the test is one scenario."
	)]
	public async Task ValidateAsync_GivenLocalizerProvider_HonoursIt(CancellationToken cancellationToken)
	{
		// Arrange
		FakeStringLocalizerFactory factory = new(new() { ["too_small"] = "shared" });
		ServiceCollection services = new();
		services.AddSingleton<IStringLocalizerFactory>(factory);
		services.AddZodSharpFactory(static zod => zod.Register(new ScriptedValidator<Order>(MinLengthError)));
		services.AddZodSharpValidation();
		services.AddValidation(static options =>
			options.LocalizerProvider = static (_, f) => f.Create(typeof(SharedResource))
		);
		using var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order { Lines = [new()] }, context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("shared");
		await Assert.That(factory.RequestedTypes).IsEquivalentTo([typeof(SharedResource)]);
	}

	[Test]
	public async Task ValidateAsync_GivenNoStringLocalizerFactory_UsesErrorMessage(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static zod => zod.Register(new ScriptedValidator<Order>(MinLengthError)));
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<Order>().ValidateAsync(new Order { Lines = [new()] }, context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("Sku must be at least 3 characters");
	}

	[Test]
	public async Task Format_GivenRuntimeSchemaErrors_LocalizesThemByTheRuleTemplates()
	{
		// Arrange
		var minLength = FirstError(Z.String().Min(3), "a", "Sku");
		var range = FirstError(Z.Number().Range(1, 10), 11d, "Qty");
		var email = FirstError(Z.String().Email(), "nope", "Mail");
		var formatter = CreateFormatter(
			new()
			{
				["fr-FR:String must be at least {minimum} characters long, but got {count}"] =
					"{displayName} : au moins {minimum} caractères (reçu {count})",
				["fr-FR:Value must be between {minimum} and {maximum}"] =
					"{displayName} : entre {minimum} et {maximum}",
				["fr-FR:Invalid email format"] = "{displayName} : adresse e-mail invalide",
			}
		);
		var french = CultureInfo.GetCultureInfo("fr-FR");

		// Act
		string[] messages =
		[
			formatter.Format(minLength, typeof(OrderLine), french),
			formatter.Format(range, typeof(OrderLine), french),
			formatter.Format(email, typeof(OrderLine), french),
		];

		// Assert
		await Assert
			.That(messages)
			.IsEquivalentTo([
				"Sku : au moins 3 caractères (reçu 1)",
				"Qty : entre 1 et 10",
				"Mail : adresse e-mail invalide",
			]);
	}

	// A runtime schema's error, located under a member as an object schema would place it.
	static ZodValidationError FirstError<T>(ZodType<T> schema, T value, string member) =>
		schema.Validate(value).Errors.Single().WithPathPrefix(ZodPathSegment.Member(member));

	static StringLocalizerZodMessageFormatter CreateFormatter(Dictionary<string, string> resources) =>
		new(new FakeStringLocalizerFactory(resources));

	sealed class SharedResource;
}

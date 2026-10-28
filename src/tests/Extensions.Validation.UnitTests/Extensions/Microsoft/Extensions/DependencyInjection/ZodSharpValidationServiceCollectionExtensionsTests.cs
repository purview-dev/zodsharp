using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace Microsoft.Extensions.DependencyInjection;

public class ZodSharpValidationServiceCollectionExtensionsTests
{
	// The Microsoft.Extensions.Validation generator runs on this test project (it references the package with
	// analyzers), so every AddValidation() call below is intercepted exactly as in an application: the
	// generated DataAnnotations resolver is inserted at index 0 and a runtime parameter resolver is appended.
	// Both are file-local classes, so their runtime names carry a file-scoped prefix; match on the suffix.
	const string GeneratedResolverName = "GeneratedValidatableInfoResolver";
	const string RuntimeParameterResolverName = "RuntimeValidatableParameterInfoResolver";

	[Test]
	public async Task AddZodSharpValidation_GivenAddValidationCalledBefore_PlacesZodResolverFirst()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpValidation();

		// Act
		var resolvers = services.BuildServiceProvider().GetValidationOptions().Resolvers;

		// Assert
		await Assert.That(resolvers[0]).IsTypeOf<ZodValidatableInfoResolver>();
		await Assert.That(resolvers[1].GetType().Name).EndsWith(GeneratedResolverName);
	}

	[Test]
	public async Task AddZodSharpValidation_GivenAddValidationCalledAfter_PlacesZodResolverFirst()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpValidation();
		services.AddValidation();

		// Act
		var resolvers = services.BuildServiceProvider().GetValidationOptions().Resolvers;

		// Assert
		await Assert.That(resolvers[0]).IsTypeOf<ZodValidatableInfoResolver>();
		await Assert.That(resolvers[1].GetType().Name).EndsWith(GeneratedResolverName);
	}

	[Test]
	public async Task AddZodSharpValidation_GivenAddValidationCalledBeforeAndAfter_PlacesZodResolverFirst()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpValidation();
		services.AddValidation(static options => options.MaxDepth = 16);

		// Act
		var options = services.BuildServiceProvider().GetValidationOptions();

		// Assert
		await Assert.That(options.Resolvers[0]).IsTypeOf<ZodValidatableInfoResolver>();
		await Assert
			.That(
				options.Resolvers.Count(static r =>
					r.GetType().Name.EndsWith(GeneratedResolverName, StringComparison.Ordinal)
				)
			)
			.IsEqualTo(2);
		await Assert.That(options.MaxDepth).IsEqualTo(16);
	}

	[Test]
	public async Task AddZodSharpValidation_GivenAddValidationNotCalled_RegistersOnlyZodResolver()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpValidation();

		// Act
		var resolvers = services.BuildServiceProvider().GetValidationOptions().Resolvers;

		// Assert
		await Assert.That(resolvers.Count).IsEqualTo(1);
		await Assert.That(resolvers[0]).IsTypeOf<ZodValidatableInfoResolver>();
	}

	[Test]
	public async Task AddZodSharpValidation_GivenTypeKnownToBothResolvers_ValidatesWithZodSharp(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory =>
			factory.Register(
				new ScriptedValidator<DualModel>(Error("too_small", "ZodSharp: Name is required.", M("Name")))
			)
		);
		services.AddZodSharpValidation();
		var provider = services.BuildServiceProvider();
		var context = provider.CreateContext();

		// Act
		await provider.GetTypeInfo<DualModel>().ValidateAsync(new DualModel(), context, cancellationToken);

		// Assert
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("ZodSharp: Name is required.");
	}

	[Test]
	public async Task AddZodSharpValidation_GivenAfterGeneratedPlacement_PlacesZodResolverAfterGeneratedResolver()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpValidation(static options => options.Placement = ZodResolverPlacement.AfterGenerated);

		// Act
		var resolvers = services.BuildServiceProvider().GetValidationOptions().Resolvers;

		// Assert
		await Assert.That(resolvers[0].GetType().Name).EndsWith(GeneratedResolverName);
		await Assert.That(resolvers[1]).IsTypeOf<ZodValidatableInfoResolver>();
		await Assert.That(resolvers[2].GetType().Name).EndsWith(RuntimeParameterResolverName);
	}

	[Test]
	public async Task AddZodSharpValidation_GivenCalledTwice_RegistersOneResolverAndAppliesBothConfigurations()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpValidation(static options => options.AlsoRunDataAnnotations = true);
		services.AddZodSharpValidation(static options => options.PathNamer = ZodPathNamer.FromFunc(static n => n));
		var provider = services.BuildServiceProvider();

		// Act
		var resolvers = provider.GetValidationOptions().Resolvers;
		var options = provider.GetRequiredService<IOptions<ZodValidationOptions>>().Value;

		// Assert
		await Assert.That(resolvers.Count).IsEqualTo(1);
		await Assert.That(resolvers[0]).IsSameReferenceAs(provider.GetRequiredService<ZodValidatableInfoResolver>());
		await Assert.That(options.AlsoRunDataAnnotations).IsTrue();
		await Assert.That(options.PathNamer).IsNotNull();
	}

	[Test]
	public async Task AddZodSharpValidation_GivenNoFactoryRegistered_RegistersSchemaFactory()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpValidation();

		// Act
		var factory = services.BuildServiceProvider().GetService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory).IsNotNull();
	}

	[Test]
	[Arguments(true, true)]
	[Arguments(false, false)]
	public async Task AddZodSharpValidation_GivenValidateParameters_ClaimsParametersOnlyWhenAsked(
		bool validateParameters,
		bool expected,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpFactory(static factory => factory.Register(new GeneratedPersonSchemaValidator()));
		services.AddZodSharpValidation(configure: null, validateParameters);
		await using var provider = services.BuildServiceProvider();
		var resolver = provider.GetRequiredService<ZodValidatableInfoResolver>();
		var parameter = typeof(ZodSharpValidationServiceCollectionExtensionsTests)
			.GetMethod(
				nameof(Handler),
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
			)!
			.GetParameters()[0];

		// Act
		var claimed = resolver.TryGetValidatableParameterInfo(parameter, out _);
		var typeClaimed = resolver.TryGetValidatableTypeInfo(typeof(GeneratedPerson), out _);

		// Assert - type validation (forms, ValidateAsync) is unaffected either way.
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(resolver.ValidatesParameters).IsEqualTo(expected);
		await Assert.That(claimed).IsEqualTo(expected);
		await Assert.That(typeClaimed).IsTrue();
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task AddZodSharpValidation_GivenATypeOnlyRegistrationAndAParameterRequest_ClaimsParametersInEitherOrder(
		bool typeOnlyFirst,
		CancellationToken cancellationToken
	)
	{
		// Arrange - AddZodSharpBlazor registers type validation only; the application's own AddZodSharpValidation()
		// still asks for parameters, whichever comes first.
		ServiceCollection services = new();
		if (typeOnlyFirst)
		{
			services.AddZodSharpValidation(configure: null, validateParameters: false);
			services.AddZodSharpValidation();
		}
		else
		{
			services.AddZodSharpValidation();
			services.AddZodSharpValidation(configure: null, validateParameters: false);
		}

		await using var provider = services.BuildServiceProvider();

		// Act
		var resolver = provider.GetRequiredService<ZodValidatableInfoResolver>();

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(resolver.ValidatesParameters).IsTrue();
	}

	[Test]
	[Arguments(true, false)]
	[Arguments(false, true)]
	public async Task ValidateParameters_GivenAnExplicitValue_OverridesTheRegistration(
		bool registrationAsks,
		bool configured,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpValidation(options => options.ValidateParameters = configured, registrationAsks);
		await using var provider = services.BuildServiceProvider();

		// Act
		var resolver = provider.GetRequiredService<ZodValidatableInfoResolver>();

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(resolver.ValidatesParameters).IsEqualTo(configured);
	}

	static void Handler(GeneratedPerson person) => _ = person;
}

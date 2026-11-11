using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// <c>AlsoRunDataAnnotations</c> composes ZodSharp with the platform's generated resolver. Field-level
/// validation must fall through to that resolver for a property ZodSharp does not own.
/// </summary>
public class ZodCompositeTypeInfoTests
{
	[Test]
	public async Task TryFindProperty_GivenAlsoRunDataAnnotations_FindsTheNextResolversProperty(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the ZodSharp validator has no member-level validation, so only DataAnnotations can validate
		// the Name field on its own.
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpFactory(static factory => factory.Register(new ScriptedValidator<DualModel>()));
		services.AddZodSharpValidation(static options => options.AlsoRunDataAnnotations = true);
		using var provider = services.BuildServiceProvider();
		var typeInfo = provider.GetTypeInfo<DualModel>();
		var validationOptions = provider.GetValidationOptions();
		var context = provider.CreateContext();

		// Act
		var found = typeInfo.TryFindProperty(nameof(DualModel.Name), validationOptions, out var property);
		if (property is not null)
			await property.ValidateAsync(new DualModel(), context, CancellationToken.None);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(context.Single().ErrorMessage).IsEqualTo("DataAnnotations: Name is required.");
	}
}

using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Microsoft.Extensions.Validation's <c>[SkipValidation]</c> on a type (it also targets properties and
/// parameters) removes it from the resolver chain, even when a ZodSharp validator is registered.
/// </summary>
public class ZodValidatableInfoResolverSkipTests
{
	[Test]
	public async Task Resolver_GivenTypeWithSkipValidation_DoesNotClaimIt(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(new ScriptedValidator<SkippedModel>()));

		// Act
		var claimed = provider.GetValidationOptions().TryGetValidatableTypeInfo(typeof(SkippedModel), out _);

		// Assert
		await Assert.That(claimed).IsFalse();
	}
}

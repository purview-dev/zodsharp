using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace Microsoft.Extensions.Validation;

public class ZodValidationOptionsExtensionsTests
{
	[Test]
	public async Task ValidateAsync_GivenZodValidatedValue_ReturnsFullFidelityReport(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var expected = Error("too_small", "Email", M("Customer"), M("Email"));
		using var provider = CreateProvider(factory => factory.Register(new ScriptedValidator<Order>(expected)));

		// Act
		var report = await provider.GetValidationOptions().ValidateAsync(new Order(), provider, cancellationToken);

		// Assert
		await Assert.That(report.HasErrors).IsTrue();
		await Assert.That(report.Errors.Single()).IsEqualTo(expected);
	}

	[Test]
	public async Task ValidateAsync_GivenValidValue_ReturnsEmptyReport(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static factory => factory.Register(new ScriptedValidator<Order>()));

		// Act
		var report = await provider.GetValidationOptions().ValidateAsync(new Order(), provider, cancellationToken);

		// Assert
		await Assert.That(report.HasErrors).IsFalse();
	}

	[Test]
	public async Task ValidateAsync_GivenUnknownType_ReturnsEmptyReport(CancellationToken cancellationToken)
	{
		// Arrange
		using var provider = CreateProvider(static _ => { });

		// Act
		var report = await provider.GetValidationOptions().ValidateAsync(new Order(), provider, cancellationToken);

		// Assert
		await Assert.That(report.HasErrors).IsFalse();
	}

	[Test]
	public async Task ValidateAsync_GivenDataAnnotationsErrors_ConvertsThemToCustomErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ServiceCollection services = new();
		services.AddValidation();
		services.AddZodSharpValidation();
		using var provider = services.BuildServiceProvider();

		// Act
		var report = await provider.GetValidationOptions().ValidateAsync(new DualModel(), provider, cancellationToken);

		// Assert
		var error = report.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(ZodIssueCategories.Custom);
		await Assert.That(error.Message).IsEqualTo("DataAnnotations: Name is required.");
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("Name");
	}
}

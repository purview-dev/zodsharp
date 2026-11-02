using System.Globalization;

namespace ZodSharp.Core;

public class DefaultZodMessageFormatterTests
{
	[Test]
	public async Task Format_GivenCompleteMessageFormat_FormatsInTheRequestedCulture()
	{
		// Arrange
		var error = ValidationError.Create("too_small", "Must be at least 1.5.", ZodPath.Member("Price")) with
		{
			MessageFormat = "Must be at least {minimum}.",
			Lower = ErrorBound.Including(1.5),
		};

		// Act
		var message = DefaultZodMessageFormatter.Instance.Format(error, CultureInfo.GetCultureInfo("fr-FR"));

		// Assert
		await Assert.That(message).IsEqualTo("Must be at least 1,5.");
	}

	[Test]
	public async Task Format_GivenUnresolvedPlaceholder_ReturnsTheMessage()
	{
		// Arrange
		var error = ValidationError.Create("custom", "Original.", ZodPath.Root) with
		{
			MessageFormat = "Needs {unknown}.",
		};

		// Act
		var message = DefaultZodMessageFormatter.Instance.Format(error, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Original.");
	}

	[Test]
	public async Task Format_GivenNoMessageFormat_ReturnsTheMessage()
	{
		// Arrange
		var error = ValidationError.Create("custom", "Literal {braces}.", ZodPath.Root);

		// Act
		var message = DefaultZodMessageFormatter.Instance.Format(error, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Literal {braces}.");
	}
}

using System.Globalization;

namespace ZodSharp.Core;

public class ZodMessageFormatTests
{
	[Test]
	[Arguments("Use {name}")]
	[Arguments("Unbalanced { brace")]
	[Arguments("Unbalanced } brace")]
	[Arguments("{")]
	[Arguments("}")]
	[Arguments("{1}")]
	[Arguments("{0,abc}")]
	public async Task Format_GivenMalformedOrUnresolvableFormat_DoesNotThrow(string format)
	{
		// Act
		var message = ZodMessageFormat.Format(format, ["value"], provider: CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsNotNull();
	}

	[Test]
	public async Task Format_GivenUnresolvablePlaceholder_WritesItLiterally()
	{
		// Act
		var message = ZodMessageFormat.Format("Use {name} here", parameters: null, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("Use {name} here");
	}

	[Test]
	public async Task Format_GivenNamedAndPositionalValues_ResolvesBoth()
	{
		// Arrange
		Dictionary<string, object?> parameters = new() { ["min"] = 3 };

		// Act
		var message = ZodMessageFormat.Format(
			"{0} must be at least {min} long",
			["Name"],
			parameters,
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(message).IsEqualTo("Name must be at least 3 long");
	}

	[Test]
	public async Task Format_GivenEscapedBraces_WritesSingleBraces()
	{
		// Act
		var message = ZodMessageFormat.Format("{{literal}} {0}", ["x"], provider: CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(message).IsEqualTo("{literal} x");
	}

	[Test]
	public async Task Format_GivenFormatSpecifier_UsesInvariantCultureByDefault()
	{
		// Arrange
		var previous = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new("de-DE");
		try
		{
			// Act
			var message = ZodMessageFormat.Format("{0:0.0}", [1.5], provider: null);

			// Assert
			await Assert.That(message).IsEqualTo("1.5");
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	[Test]
	public async Task Format_GivenError_ResolvesBuiltInFields()
	{
		// Arrange
		var error = ValidationError.Create(
			"too_small",
			"ignored",
			ZodPath.ParseDotted("Items[0].Quantity"),
			minimum: 1,
			inclusive: true
		);

		// Act
		var message = ZodMessageFormat.Format(
			"{member} at {path} must be >= {minimum} ({code})",
			error,
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(message).IsEqualTo("Quantity at Items[0].Quantity must be >= 1 (too_small)");
	}

	[Test]
	public async Task TryFormatComplete_GivenMissingParameter_ReturnsFalse()
	{
		// Arrange
		var error = ValidationError.Create("tenant", "Tenant is invalid", ZodPath.Root);

		// Act
		var complete = ZodMessageFormat.TryFormatComplete(
			"Tenant {tenantId} is invalid",
			error,
			out _,
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(complete).IsFalse();
	}
}

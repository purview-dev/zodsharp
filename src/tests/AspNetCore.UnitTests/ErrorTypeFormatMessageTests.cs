using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public class ErrorTypeFormatMessageTests
{
	[Test]
	public async Task GivenMessageFormatWithParameters_FormatsPlaceholders()
	{
		// Arrange
		ErrorType errorType = new(
			"aggregate_save_failed",
			Description: "Save failed.",
			HttpStatus: StatusCodes.Status409Conflict,
			MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save"
		)
		{
			Parameters =
			[
				new ErrorTypeParameter("OrderId", typeof(string)),
				new ErrorTypeParameter("AggregateType", typeof(string)),
			],
		};

		// Act
		var message = errorType.FormatMessage(
			new Dictionary<string, object?> { ["OrderId"] = "ord-1", ["AggregateType"] = "Order" }
		);

		// Assert
		await Assert.That(message).IsEqualTo("Order 'ord-1' (of type Order) failed to save");
	}

	[Test]
	public async Task GivenMessageFormat_TypedParametersOverload_FormatsPlaceholders()
	{
		// Arrange
		ErrorType errorType = new(
			"aggregate_save_failed",
			Description: "Save failed.",
			HttpStatus: StatusCodes.Status409Conflict,
			MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save"
		)
		{
			Parameters =
			[
				new ErrorTypeParameter("OrderId", typeof(string)),
				new ErrorTypeParameter("AggregateType", typeof(string)),
			],
		};
		var parameters = ErrorTypeParameters.Create(
			errorType.Parameters,
			new Dictionary<string, object?> { ["OrderId"] = "ord-1", ["AggregateType"] = "Order" }
		);

		// Act
		var message = errorType.FormatMessage(parameters);

		// Assert
		await Assert.That(message).IsEqualTo("Order 'ord-1' (of type Order) failed to save");
		await Assert.That(parameters.Get<string>("OrderId")).IsEqualTo("ord-1");
		await Assert.That(parameters.GetDeclaredType("AggregateType")).IsEqualTo(typeof(string));
	}

	[Test]
	public async Task GivenMessageFormat_MissingParameterLeavesPlaceholderAsIs()
	{
		// Arrange
		ErrorType errorType = new(
			"save_failed",
			MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"
		)
		{
			Parameters =
			[
				new ErrorTypeParameter("AggregateId", typeof(string)),
				new ErrorTypeParameter("AggregateType", typeof(string)),
			],
		};

		// Act
		var message = errorType.FormatMessage(new Dictionary<string, object?> { ["AggregateId"] = "agg-123" });

		// Assert
		await Assert.That(message).IsEqualTo("Aggregate 'agg-123' (of type {AggregateType}) failed to save");
	}

	[Test]
	public async Task GivenNoMessageFormat_FallsBackToDescription()
	{
		// Arrange
		ErrorType errorType = new(
			"locked",
			Description: "The resource is locked.",
			HttpStatus: StatusCodes.Status423Locked
		);

		// Act
		var message = errorType.FormatMessage(parameters: null);

		// Assert
		await Assert.That(message).IsEqualTo("The resource is locked.");
	}

	[Test]
	public async Task GivenNoMessageFormatOrDescription_FallsBackToCode()
	{
		// Arrange
		ErrorType errorType = new("conflict", HttpStatus: StatusCodes.Status409Conflict);

		// Act
		var message = errorType.FormatMessage(parameters: null);

		// Assert
		await Assert.That(message).IsEqualTo("conflict");
	}

	[Test]
	public async Task GivenMessageFormatWithNullParameters_ReturnsFormatUnchanged()
	{
		// Arrange
		ErrorType errorType = new("save_failed", MessageFormat: "Save of '{AggregateId}' failed.");

		// Act
		var message = errorType.FormatMessage(parameters: null);

		// Assert
		await Assert.That(message).IsEqualTo("Save of '{AggregateId}' failed.");
	}
}

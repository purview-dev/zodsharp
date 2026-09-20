using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public static partial class ConcurrentErrorType
{
	[ErrorType]
	public static readonly ErrorType SaveFailed = new(
		Code: "aggregate_save_failed",
		Description: "The order could not be saved because it was modified concurrently.",
		HttpStatus: StatusCodes.Status409Conflict,
		MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
		Parameters: new List<ErrorTypeParameter>
		{
			new("OrderId", typeof(string)),
			ErrorType.Param<string>("AggregateType"),
			//new ("AggregateType", typeof(string)),
		}
	);
}

public class ErrorTypeSourceGeneratorIntegrationTests
{
	[Test]
	public async Task GivenGeneratedCreate_PopulatesCodeParametersAndPath()
	{
		// Arrange/Act
		var error = ConcurrentErrorType.CreateSaveFailed("ord-42", "Order", path: ["orderId"]);

		// Assert
		await Assert.That(error.Code).IsEqualTo("aggregate_save_failed");
		await Assert.That(error.Path).IsEquivalentTo(["orderId"]);
		await Assert.That(error.Parameters).IsNotNull();
		await Assert.That(error.Parameters!["OrderId"]).IsEqualTo("ord-42");
		await Assert.That(error.Parameters!["AggregateType"]).IsEqualTo("Order");
		await Assert.That(error.Parameters!.Get<string>("OrderId")).IsEqualTo("ord-42");
		await Assert.That(error.Parameters!.GetDeclaredType("OrderId")).IsEqualTo(typeof(string));
		await Assert.That(error.Message).IsEqualTo("Order 'ord-42' (of type Order) failed to save");
	}

	[Test]
	public async Task GivenGeneratedCreate_MapsToConflictProblemDetails()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(ConcurrentErrorType.SaveFailed);

		// Act
		var error = ConcurrentErrorType.CreateSaveFailed("ord-42", "Order");
		var details = new ZodException([error]).ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert
			.That(details.Errors[string.Empty])
			.IsEquivalentTo(["Order 'ord-42' (of type Order) failed to save"]);
	}

	[Test]
	public async Task GivenGeneratedThrow_ThrowsZodExceptionWithTheError()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(ConcurrentErrorType.SaveFailed);

		// Act
		ZodException? thrown = null;
		try
		{
			ConcurrentErrorType.ThrowSaveFailed("ord-42", "Order");
		}
		catch (ZodException ex)
		{
			thrown = ex;
		}

		// Assert
		await Assert.That(thrown).IsNotNull();
		await Assert.That(thrown!.Errors).HasSingleItem();
		await Assert.That(thrown!.Errors[0].Parameters!["OrderId"]).IsEqualTo("ord-42");
	}
}

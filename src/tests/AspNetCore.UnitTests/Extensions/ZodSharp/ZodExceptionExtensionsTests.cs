using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp;

public class ZodExceptionExtensionsTests
{
	[Test]
	public async Task ToHttpValidationProblemDetails_GivenFailure_PreservesErrorsAndIssues()
	{
		// Arrange
		ZodException exception = new([
			ValidationError.Create(
				"too_small",
				"Field 'Items' must contain at least 2 elements.",
				["Items"],
				origin: "array",
				minimum: 2,
				inclusive: true
			),
			ValidationError.Create(
				"too_big",
				"Field 'Order.Lines[3].Quantity' must contain no more than 5 elements.",
				["Order", "Lines", "[3]", "Quantity"],
				origin: "collection",
				maximum: 5,
				inclusive: true
			),
		]);

		// Act
		var details = exception.ToHttpValidationProblemDetails();

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(details.Errors["Items"]).IsEquivalentTo(["Field 'Items' must contain at least 2 elements."]);
		await Assert
			.That(details.Errors["Order.Lines[3].Quantity"])
			.IsEquivalentTo(["Field 'Order.Lines[3].Quantity' must contain no more than 5 elements."]);
		await Assert.That(details.Extensions.ContainsKey("issues")).IsTrue();
	}

	[Test]
	public async Task ToHttpValidationProblemDetails_GivenEmptyErrors_ProducesEmptyPayload()
	{
		// Arrange
		ZodException exception = new([]);

		// Act
		var details = exception.ToHttpValidationProblemDetails();

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(details.Errors).IsEmpty();
	}

	[Test]
	public async Task ToValidationProblemDetails_GivenFailure_ProducesValidationProblemDetails()
	{
		// Arrange
		ZodException exception = new([
			ValidationError.Create("too_small", "Field 'Items' must contain at least 2 elements.", ["Items"]),
		]);

		// Act
		var details = exception.ToValidationProblemDetails();

		// Assert
		await Assert.That(details).IsTypeOf<ValidationProblemDetails>();
		await Assert.That(details.Errors["Items"]).HasSingleItem();
		await Assert.That(details.Extensions.ContainsKey("issues")).IsTrue();
	}
}

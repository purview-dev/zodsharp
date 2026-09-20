using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp;

public class ProblemDetailsExtensionsTests
{
	[Test]
	public async Task ToHttpValidationProblemDetails_GivenFailure_PreservesErrorsAndIssues()
	{
		var result = ValidationResult<string>.Failure([
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

		var details = result.ToHttpValidationProblemDetails();

		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(details.Errors["Items"]).IsEquivalentTo(["Field 'Items' must contain at least 2 elements."]);
		await Assert
			.That(details.Errors["Order.Lines[3].Quantity"])
			.IsEquivalentTo(["Field 'Order.Lines[3].Quantity' must contain no more than 5 elements."]);
		await Assert.That(details.Extensions.ContainsKey("issues")).IsTrue();
	}

	[Test]
	public async Task ToValidationProblemDetails_GivenFailure_ProducesValidationProblemDetails()
	{
		var result = ValidationResult<string>.Failure(
			ValidationError.Create(
				"too_small",
				"Field 'Items' must contain at least 2 elements.",
				["Items"],
				origin: "array",
				minimum: 2,
				inclusive: true
			)
		);

		var details = result.ToValidationProblemDetails();

		await Assert.That(details).IsTypeOf<ValidationProblemDetails>();
		await Assert.That(details.Errors["Items"]).HasSingleItem();
		await Assert.That(details.Extensions.ContainsKey("issues")).IsTrue();
	}
}

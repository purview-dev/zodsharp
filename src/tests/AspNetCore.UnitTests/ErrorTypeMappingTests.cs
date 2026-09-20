using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public class ErrorTypeMappingTests
{
	[Test]
	public async Task GivenMappedCode_ReturnsErrorTypeStatusAndFormattedMessage()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType(
				"aggregate_save_failed",
				"The aggregate could not be saved.",
				StatusCodes.Status409Conflict,
				MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"
			)
			{
				Parameters =
				[
					new ErrorTypeParameter("AggregateId", typeof(string)),
					new ErrorTypeParameter("AggregateType", typeof(string)),
				],
			}
		);
		ZodException exception = new([
			ValidationError.Create(
				"aggregate_save_failed",
				"Aggregate failed to save.",
				[],
				parameters: new Dictionary<string, object?>
				{
					["AggregateId"] = "agg-123",
					["AggregateType"] = "Invoice",
				}
			),
		]);

		// Act
		var details = exception.ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(details.Detail).IsEqualTo("The aggregate could not be saved.");
		await Assert
			.That(details.Errors[string.Empty])
			.IsEquivalentTo(["Aggregate 'agg-123' (of type Invoice) failed to save"]);
	}

	[Test]
	public async Task GivenMappedCode_ExposesTitleAndParametersInExtensionsAndIssues()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType("locked", Title: "Resource is locked", HttpStatus: StatusCodes.Status423Locked)
		);
		ZodException exception = new([
			ValidationError.Create(
				"locked",
				"Resource is locked.",
				[],
				parameters: new Dictionary<string, object?> { ["ResourceId"] = "res-42" }
			),
		]);

		// Act
		var details = exception.ToHttpValidationProblemDetails(registry);
		var issues = (ValidationIssue[])details.Extensions["issues"]!;

		// Assert
		await Assert.That(details.Title).IsEqualTo("Resource is locked");
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status423Locked);
		await Assert.That(details.Extensions["resourceId"]).IsEqualTo("res-42");
		await Assert.That(issues).HasSingleItem();
		await Assert.That(issues[0].Parameters).IsNotNull();
		await Assert.That(issues[0].Parameters!["ResourceId"]).IsEqualTo("res-42");
	}

	[Test]
	public async Task GivenUnmappedCode_ReturnsDefaultStatusAndDefaultTitle()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(new ErrorType("known"));
		ZodException exception = new([ValidationError.Create("unknown", "Something failed.", [])]);

		// Act
		var details = exception.ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(details.Title).IsEqualTo("One or more validation errors occurred.");
	}

	[Test]
	public async Task GivenMultipleCodesWithDifferentStatuses_HighestStatusWins()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(new ErrorType("conflict", HttpStatus: StatusCodes.Status409Conflict));
		registry.Register(new ErrorType("locked", HttpStatus: StatusCodes.Status423Locked));
		ZodException exception = new([
			ValidationError.Create("conflict", "Conflict.", []),
			ValidationError.Create("locked", "Locked.", []),
		]);

		// Act
		var details = exception.ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status423Locked);
		await Assert.That(details.Errors.Keys).IsEquivalentTo([string.Empty]);
	}

	[Test]
	public async Task GivenMessageFormat_MissingPlaceholderIsLeftAsIs()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType(
				"save_failed",
				HttpStatus: StatusCodes.Status409Conflict,
				MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"
			)
			{
				Parameters =
				[
					new ErrorTypeParameter("AggregateId", typeof(string)),
					new ErrorTypeParameter("AggregateType", typeof(string)),
				],
			}
		);
		ZodException exception = new([
			ValidationError.Create(
				"save_failed",
				"Save failed.",
				[],
				parameters: new Dictionary<string, object?> { ["AggregateId"] = "agg-123" }
			),
		]);

		// Act
		var details = exception.ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert
			.That(details.Errors[string.Empty])
			.IsEquivalentTo(["Aggregate 'agg-123' (of type {AggregateType}) failed to save"]);
	}

	[Test]
	public async Task GivenValidationResultWithRegistry_FormatsMessages()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType(
				"too_small",
				HttpStatus: StatusCodes.Status422UnprocessableEntity,
				MessageFormat: "'{Field}' must be at least {Minimum} characters."
			)
			{
				Parameters =
				[
					new ErrorTypeParameter("Field", typeof(string)),
					new ErrorTypeParameter("Minimum", typeof(int)),
				],
			}
		);
		var result = ValidationResult<string>.Failure(
			ValidationError.Create(
				"too_small",
				"Too short.",
				["name"],
				parameters: new Dictionary<string, object?> { ["Field"] = "Name", ["Minimum"] = 3 }
			)
		);

		// Act
		var details = result.ToHttpValidationProblemDetails(registry);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(details.Errors["name"]).IsEquivalentTo(["'Name' must be at least 3 characters."]);
	}

	[Test]
	public async Task GivenValidationResult_ExistingOverloadKeepsBehavior()
	{
		// Arrange
		var result = ValidationResult<string>.Failure(
			ValidationError.Create("too_small", "Field 'Items' must contain at least 2 elements.", ["Items"])
		);

		// Act
		var details = result.ToHttpValidationProblemDetails();

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(details.Title).IsEqualTo("One or more validation errors occurred.");
	}
}

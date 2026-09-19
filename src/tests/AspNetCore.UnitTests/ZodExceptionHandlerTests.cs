using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public class ZodExceptionHandlerTests
{
	[Test]
	public async Task TryHandleAsync_GivenZodException_ReturnsTrueAndWritesProblemDetails(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new ZodProblemDetailsOptions());
		ZodExceptionHandler handler = new(options);
		ZodException exception = new([ValidationError.Create("invalid", "Invalid value.", ["value"])]);
		var httpContext = NewContext();

		// Act
		var handled = await handler.TryHandleAsync(httpContext, exception, cancellationToken);

		// Assert
		await Assert.That(handled).IsTrue();
		await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(httpContext.Response.ContentType).IsEqualTo("application/problem+json");

		var body = await ReadBody(httpContext, cancellationToken);
		using var document = JsonDocument.Parse(body);
		var root = document.RootElement;
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(root.GetProperty("errors").GetProperty("value")[0].GetString()).IsEqualTo("Invalid value.");
		await Assert.That(root.GetProperty("traceId").GetString()).IsEqualTo("trace-1");
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(1);
	}

	[Test]
	public async Task TryHandleAsync_GivenMappedErrorType_UsesMappedStatusAndFormattedMessage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(
			new ErrorType(
				"aggregate_save_failed",
				HttpStatus: StatusCodes.Status409Conflict,
				MessageFormat: "Aggregate '{AggregateId}' failed to save"
			)
			{
				Parameters = ["AggregateId"],
			}
		);
		var options = Microsoft.Extensions.Options.Options.Create(new ZodProblemDetailsOptions { Registry = registry });
		ZodExceptionHandler handler = new(options);
		ZodException exception = new([
			ValidationError.Create(
				"aggregate_save_failed",
				"Save failed.",
				[],
				parameters: new Dictionary<string, object?> { ["AggregateId"] = "agg-123" }
			),
		]);
		var httpContext = NewContext();

		// Act
		var handled = await handler.TryHandleAsync(httpContext, exception, cancellationToken);

		// Assert
		await Assert.That(handled).IsTrue();
		await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);

		var body = await ReadBody(httpContext, cancellationToken);
		using var document = JsonDocument.Parse(body);
		var root = document.RootElement;
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert
			.That(root.GetProperty("errors").GetProperty("")[0].GetString())
			.IsEqualTo("Aggregate 'agg-123' failed to save");
		await Assert.That(root.GetProperty("aggregateId").GetString()).IsEqualTo("agg-123");
	}

	[Test]
	public async Task TryHandleAsync_GivenStatusCodeSelector_UsesSelectorStatus(CancellationToken cancellationToken)
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(
			new ZodProblemDetailsOptions { StatusCodeSelector = static _ => StatusCodes.Status503ServiceUnavailable }
		);
		ZodExceptionHandler handler = new(options);
		ZodException exception = new([ValidationError.Create("invalid", "Invalid value.", [])]);
		var httpContext = NewContext();

		// Act
		var handled = await handler.TryHandleAsync(httpContext, exception, cancellationToken);

		// Assert
		await Assert.That(handled).IsTrue();
		await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status503ServiceUnavailable);
	}

	[Test]
	public async Task TryHandleAsync_GivenOtherException_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		ZodExceptionHandler handler = new(Microsoft.Extensions.Options.Options.Create(new ZodProblemDetailsOptions()));
		var httpContext = NewContext();

		// Act
		var handled = await handler.TryHandleAsync(
			httpContext,
			new InvalidOperationException("boom"),
			cancellationToken
		);

		// Assert
		await Assert.That(handled).IsFalse();
	}

	static DefaultHttpContext NewContext()
	{
		DefaultHttpContext httpContext = new();
		httpContext.TraceIdentifier = "trace-1";
		httpContext.Response.Body = new MemoryStream();
		return httpContext;
	}

	static async Task<string> ReadBody(HttpContext httpContext, CancellationToken cancellationToken)
	{
		httpContext.Response.Body.Position = 0;
		using StreamReader reader = new(httpContext.Response.Body);
		return await reader.ReadToEndAsync(cancellationToken);
	}
}

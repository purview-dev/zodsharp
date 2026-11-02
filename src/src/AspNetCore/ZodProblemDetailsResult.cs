using Microsoft.AspNetCore.Http;

namespace ZodSharp.AspNetCore;

/// <summary>
/// An <see cref="IResult"/> that writes a ZodSharp ProblemDetails through <see cref="ZodProblemDetailsWriter"/>,
/// so it goes through the registered <see cref="IProblemDetailsService"/> like every other path.
/// </summary>
public sealed class ZodProblemDetailsResult
	: IResult,
		IStatusCodeHttpResult,
		IValueHttpResult,
		IValueHttpResult<HttpValidationProblemDetails>,
		IContentTypeHttpResult
{
	readonly ZodProblemDetailsWriter _writer;

	internal ZodProblemDetailsResult(ZodProblemDetailsWriter writer, HttpValidationProblemDetails problemDetails)
	{
		_writer = writer;
		ProblemDetails = problemDetails;
	}

	/// <summary>
	/// Gets the ProblemDetails that will be written.
	/// </summary>
	public HttpValidationProblemDetails ProblemDetails { get; }

	/// <inheritdoc/>
	public int? StatusCode => ProblemDetails.Status;

	/// <inheritdoc/>
	public string ContentType => "application/problem+json";

	object? IValueHttpResult.Value => ProblemDetails;

	HttpValidationProblemDetails? IValueHttpResult<HttpValidationProblemDetails>.Value => ProblemDetails;

	/// <inheritdoc/>
	public Task ExecuteAsync(HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		return _writer.WriteAsync(httpContext, ProblemDetails).AsTask();
	}
}

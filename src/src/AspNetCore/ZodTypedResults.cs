using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Typed results for handlers that validate manually (an injected <see cref="IZodSchemaFactory"/>, or a generated
/// <c>XxxSchema.Validate</c>). They produce the same ProblemDetails body as the endpoint filter and
/// <see cref="ZodExceptionHandler"/>, using the <see cref="ZodProblemDetailsWriter"/> from the request services.
/// </summary>
public static class ZodTypedResults
{
	/// <summary>
	/// Creates a <see cref="ValidationProblem"/> for a failed result, for union return types such as
	/// <c>Results&lt;Ok&lt;T&gt;, ValidationProblem&gt;</c>.
	/// </summary>
	/// <remarks>
	/// ASP.NET Core's <see cref="ValidationProblem"/> is always <c>400</c>. When a mapped <see cref="ErrorType"/> (or a
	/// status selector) yields another status, the body is still the full contract but the status is <c>400</c>;
	/// use <see cref="Problem{T}(in ValidationResult{T}, HttpContext)"/> to keep the computed status.
	/// </remarks>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="result">The failed result.</param>
	/// <param name="httpContext">The request.</param>
	/// <returns>The result.</returns>
	/// <exception cref="InvalidOperationException">The result is successful.</exception>
	public static ValidationProblem ValidationProblem<T>(in ValidationResult<T> result, HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		EnsureFailed(result);

		var details = ZodProblemDetailsWriter
			.GetOrCreate(httpContext.RequestServices)
			.Create(result.Errors, httpContext);

		return ToValidationProblem(details);
	}

	/// <summary>
	/// Creates a <see cref="ValidationProblem"/> for a thrown <see cref="ZodException"/>; see
	/// <see cref="ValidationProblem{T}(in ValidationResult{T}, HttpContext)"/>.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <param name="httpContext">The request.</param>
	/// <returns>The result.</returns>
	public static ValidationProblem ValidationProblem(ZodException exception, HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(httpContext);

		var details = ZodProblemDetailsWriter
			.GetOrCreate(httpContext.RequestServices)
			.Create(exception.Errors, httpContext);

		return ToValidationProblem(details);
	}

	/// <summary>
	/// Creates a result that writes the ZodSharp ProblemDetails with its computed status (for example a mapped
	/// <see cref="ErrorType.HttpStatus"/> of <c>409</c>) through the registered <see cref="IProblemDetailsService"/>.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="result">The failed result.</param>
	/// <param name="httpContext">The request.</param>
	/// <returns>The result.</returns>
	/// <exception cref="InvalidOperationException">The result is successful.</exception>
	public static ZodProblemDetailsResult Problem<T>(in ValidationResult<T> result, HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		EnsureFailed(result);

		var writer = ZodProblemDetailsWriter.GetOrCreate(httpContext.RequestServices);
		return new ZodProblemDetailsResult(writer, writer.Create(result.Errors, httpContext));
	}

	/// <summary>
	/// Creates a result that writes the ZodSharp ProblemDetails for a thrown <see cref="ZodException"/> with its
	/// computed status.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <param name="httpContext">The request.</param>
	/// <returns>The result.</returns>
	public static ZodProblemDetailsResult Problem(ZodException exception, HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(httpContext);

		var writer = ZodProblemDetailsWriter.GetOrCreate(httpContext.RequestServices);
		return new ZodProblemDetailsResult(writer, writer.Create(exception.Errors, httpContext));
	}

	static ValidationProblem ToValidationProblem(HttpValidationProblemDetails details) =>
		TypedResults.ValidationProblem(
			details.Errors,
			detail: details.Detail,
			instance: details.Instance,
			title: details.Title,
			type: details.Status == StatusCodes.Status400BadRequest
				? details.Type
				: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
			extensions: details.Extensions
		);

	static void EnsureFailed<T>(in ValidationResult<T> result)
	{
		if (result.IsSuccess)
			throw new InvalidOperationException("Cannot create ProblemDetails from a successful validation result.");
	}
}

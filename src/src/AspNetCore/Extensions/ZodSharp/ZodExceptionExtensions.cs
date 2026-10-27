using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Converts a thrown <see cref="ZodException"/> into ASP.NET Core ProblemDetails payloads. Every overload delegates
/// to <see cref="ZodProblemDetailsWriter"/>, so the body matches the endpoint filter and the exception handler.
/// </summary>
public static class ZodExceptionExtensions
{
	extension([NotNull] ZodException exception)
	{
		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />, without resolving
		/// <see cref="ErrorType"/>s.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			int statusCode = StatusCodes.Status400BadRequest
		) =>
			ProblemDetailsExtensions
				.CreateWriter(registry: null, lookup: null, formatMessages: false, statusCode)
				.Create(exception.Errors);

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			ArgumentNullException.ThrowIfNull(registry);
			return ProblemDetailsExtensions
				.CreateWriter(registry, lookup: null, formatMessages: true, statusCode)
				.Create(exception.Errors);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			ArgumentNullException.ThrowIfNull(lookup);
			return ProblemDetailsExtensions
				.CreateWriter(registry: null, lookup, formatMessages: true, statusCode)
				.Create(exception.Errors);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" /> with the supplied
		/// options.
		/// </summary>
		/// <param name="options">The ProblemDetails options.</param>
		/// <param name="httpContext">The request, for the trace id and JSON naming; optional.</param>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			ZodProblemDetailsOptions options,
			HttpContext? httpContext = null
		)
		{
			ArgumentNullException.ThrowIfNull(options);
			return new ZodProblemDetailsWriter(options).Create(exception.Errors, httpContext);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(int statusCode = StatusCodes.Status400BadRequest)
		{
			var details = exception.ToHttpValidationProblemDetails(statusCode);
			return ProblemDetailsExtensions.ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = exception.ToHttpValidationProblemDetails(registry, statusCode);
			return ProblemDetailsExtensions.ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = exception.ToHttpValidationProblemDetails(lookup, statusCode);
			return ProblemDetailsExtensions.ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into MVC <see cref="ValidationProblemDetails" /> with the supplied
		/// options. Every extension is copied.
		/// </summary>
		/// <param name="options">The ProblemDetails options.</param>
		/// <param name="httpContext">The request, for the trace id and JSON naming; optional.</param>
		public ValidationProblemDetails ToValidationProblemDetails(
			ZodProblemDetailsOptions options,
			HttpContext? httpContext = null
		)
		{
			var details = exception.ToHttpValidationProblemDetails(options, httpContext);
			return ProblemDetailsExtensions.ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a <see cref="ZodException"/> into a minimal-API <see cref="IResult"/> written by the
		/// <see cref="ZodProblemDetailsWriter"/> registered in the request services.
		/// </summary>
		/// <param name="httpContext">The request.</param>
		/// <returns>An <see cref="IResult"/>.</returns>
		public IResult ToValidationProblem(HttpContext httpContext) => ZodTypedResults.Problem(exception, httpContext);
	}
}

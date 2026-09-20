using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Converts a thrown <see cref="ZodException"/> into ASP.NET Core ProblemDetails payloads.
/// </summary>
public static class ZodExceptionExtensions
{
	extension([NotNull] ZodException exception)
	{
		/// <summary>
		/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			return ProblemDetailsMapper.Build(
				exception.Errors,
				statusCode,
				registry: null,
				lookup: null,
				formatMessages: false
			);
		}

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
			return ProblemDetailsMapper.Build(
				exception.Errors,
				statusCode,
				registry,
				lookup: null,
				formatMessages: true
			);
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
			return ProblemDetailsMapper.Build(
				exception.Errors,
				statusCode,
				registry: null,
				lookup,
				formatMessages: true
			);
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
	}
}

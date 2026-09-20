using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Converts ZodSharp validation results into ASP.NET Core ProblemDetails payloads.
/// </summary>
public static class ProblemDetailsExtensions
{
	extension<T>(ValidationResult<T> result)
	{
		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			EnsureFailed(result);
			return ProblemDetailsMapper.Build(
				result.Errors,
				statusCode,
				registry: null,
				lookup: null,
				formatMessages: false
			);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			EnsureFailed(result);
			return ProblemDetailsMapper.Build(result.Errors, statusCode, registry, lookup: null, formatMessages: true);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			EnsureFailed(result);
			return ProblemDetailsMapper.Build(result.Errors, statusCode, registry: null, lookup, formatMessages: true);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(int statusCode = StatusCodes.Status400BadRequest)
		{
			var details = result.ToHttpValidationProblemDetails(statusCode);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = result.ToHttpValidationProblemDetails(registry, statusCode);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = result.ToHttpValidationProblemDetails(lookup, statusCode);
			return ToValidationProblemDetails(details);
		}
	}

	static void EnsureFailed<T>(ValidationResult<T> result)
	{
		if (result.IsSuccess)
			throw new InvalidOperationException("Cannot create ProblemDetails from a successful validation result.");
	}

	internal static ValidationProblemDetails ToValidationProblemDetails(HttpValidationProblemDetails details) =>
		new(details.Errors)
		{
			Title = details.Title,
			Status = details.Status,
			Type = details.Type,
			Detail = details.Detail,
			Instance = details.Instance,
			Extensions = { ["issues"] = details.Extensions["issues"] },
		};
}

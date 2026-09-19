using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Converts a thrown <see cref="ZodException"/> into ASP.NET Core ProblemDetails payloads.
/// </summary>
public static class ZodExceptionExtensions
{
	/// <summary>
	/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />.
	/// </summary>
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails(
		this ZodException exception,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		ArgumentNullException.ThrowIfNull(exception);
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
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails(
		this ZodException exception,
		ErrorTypeRegistry registry,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(registry);
		return ProblemDetailsMapper.Build(exception.Errors, statusCode, registry, lookup: null, formatMessages: true);
	}

	/// <summary>
	/// Converts a <see cref="ZodException"/> into <see cref="HttpValidationProblemDetails" />, resolving
	/// <see cref="ErrorType"/>s through the supplied lookup to derive the status code, title, and messages.
	/// </summary>
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails(
		this ZodException exception,
		Func<string, ErrorType?> lookup,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(lookup);
		return ProblemDetailsMapper.Build(exception.Errors, statusCode, registry: null, lookup, formatMessages: true);
	}

	/// <summary>
	/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />.
	/// </summary>
	public static ValidationProblemDetails ToValidationProblemDetails(
		this ZodException exception,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		var details = exception.ToHttpValidationProblemDetails(statusCode);
		return ProblemDetailsExtensions.ToValidationProblemDetails(details);
	}

	/// <summary>
	/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />, resolving
	/// <see cref="ErrorType"/>s from the supplied registry.
	/// </summary>
	public static ValidationProblemDetails ToValidationProblemDetails(
		this ZodException exception,
		ErrorTypeRegistry registry,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		var details = exception.ToHttpValidationProblemDetails(registry, statusCode);
		return ProblemDetailsExtensions.ToValidationProblemDetails(details);
	}

	/// <summary>
	/// Converts a <see cref="ZodException"/> into <see cref="ValidationProblemDetails" />, resolving
	/// <see cref="ErrorType"/>s through the supplied lookup.
	/// </summary>
	public static ValidationProblemDetails ToValidationProblemDetails(
		this ZodException exception,
		Func<string, ErrorType?> lookup,
		int statusCode = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest
	)
	{
		var details = exception.ToHttpValidationProblemDetails(lookup, statusCode);
		return ProblemDetailsExtensions.ToValidationProblemDetails(details);
	}
}

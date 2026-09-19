using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Converts ZodSharp validation results into ASP.NET Core ProblemDetails payloads.
/// </summary>
#if !NETSTANDARD2_1_OR_GREATER
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public static class ProblemDetailsExtensions
{
	/// <summary>
	/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />.
	/// </summary>
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails<T>(
		this ValidationResult<T> result,
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
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails<T>(
		this ValidationResult<T> result,
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
	public static HttpValidationProblemDetails ToHttpValidationProblemDetails<T>(
		this ValidationResult<T> result,
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
	public static ValidationProblemDetails ToValidationProblemDetails<T>(
		this ValidationResult<T> result,
		int statusCode = StatusCodes.Status400BadRequest
	)
	{
		var details = result.ToHttpValidationProblemDetails(statusCode);
		return ToValidationProblemDetails(details);
	}

	/// <summary>
	/// Converts a failed validation result into <see cref="ValidationProblemDetails" />, resolving
	/// <see cref="ErrorType"/>s from the supplied registry.
	/// </summary>
	public static ValidationProblemDetails ToValidationProblemDetails<T>(
		this ValidationResult<T> result,
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
	public static ValidationProblemDetails ToValidationProblemDetails<T>(
		this ValidationResult<T> result,
		Func<string, ErrorType?> lookup,
		int statusCode = StatusCodes.Status400BadRequest
	)
	{
		var details = result.ToHttpValidationProblemDetails(lookup, statusCode);
		return ToValidationProblemDetails(details);
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

/// <summary>
/// Serializable structured validation issue metadata included in ProblemDetails extensions.
/// </summary>
public readonly record struct ValidationIssue
{
	/// <summary>
	/// The issue code.
	/// </summary>
	public required string Code { get; init; }

	/// <summary>
	/// The issue origin category.
	/// </summary>
	public string? Origin { get; init; }

	/// <summary>
	/// The inclusive minimum bound when present.
	/// </summary>
	public int? Minimum { get; init; }

	/// <summary>
	/// The inclusive maximum bound when present.
	/// </summary>
	public int? Maximum { get; init; }

	/// <summary>
	/// Whether the bound is inclusive.
	/// </summary>
	public bool? Inclusive { get; init; }

	/// <summary>
	/// The issue path segments.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public required string[] Path { get; init; }

	/// <summary>
	/// The human-readable issue message.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The additional error parameters carried by the validation error (for example aggregate ids).
	/// </summary>
	[System.Text.Json.Serialization.JsonIgnore(
		Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	)]
	public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
}

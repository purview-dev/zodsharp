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
		if (result.IsSuccess)
			throw new InvalidOperationException("Cannot create ProblemDetails from a successful validation result.");

		Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
		Dictionary<string, List<string>> groupedMessages = new(StringComparer.Ordinal);

		foreach (var error in result.Errors)
		{
			var key = ToProblemDetailsKey(error.Path);
			if (!groupedMessages.TryGetValue(key, out var messages))
			{
				messages = [];
				groupedMessages[key] = messages;
			}

			messages.Add(error.Message);
		}

		foreach (var pair in groupedMessages)
			errors[pair.Key] = [.. pair.Value];

		HttpValidationProblemDetails details = new(errors)
		{
			Title = "One or more validation errors occurred.",
			Status = statusCode,
		};
		details.Extensions["issues"] = result
			.Errors.Select(static error => new ValidationIssue
			{
				Code = error.Code,
				Origin = error.Origin,
				Minimum = error.Minimum,
				Maximum = error.Maximum,
				Inclusive = error.Inclusive,
				Path = [.. error.Path],
				Message = error.Message,
			})
			.ToArray();

		return details;
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
		return new(details.Errors)
		{
			Title = details.Title,
			Status = details.Status,
			Type = details.Type,
			Detail = details.Detail,
			Instance = details.Instance,
			Extensions = { ["issues"] = details.Extensions["issues"] },
		};
	}

	static string ToProblemDetailsKey(System.Collections.Immutable.ImmutableArray<string> path)
	{
		if (path.IsDefaultOrEmpty)
			return string.Empty;

		System.Text.StringBuilder builder = new();
		for (var i = 0; i < path.Length; i++)
		{
			var segment = path[i];
			if (i > 0 && !segment.StartsWith('['))
				builder = builder.Append('.');

			builder = builder.Append(segment);
		}

		return builder.ToString();
	}
}

/// <summary>
/// Serializable structured validation issue metadata included in ProblemDetails extensions.
/// </summary>
public sealed class ValidationIssue
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
}

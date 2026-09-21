using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Shared implementation that maps a set of <see cref="ValidationError"/>s onto an
/// <see cref="HttpValidationProblemDetails"/>, honouring registered <see cref="ErrorType"/>s.
/// </summary>
static class ProblemDetailsMapper
{
	public static HttpValidationProblemDetails Build(
		ImmutableArray<ValidationError> errors,
		int defaultStatusCode,
		ErrorTypeRegistry? registry,
		Func<string, ErrorType?>? lookup,
		bool formatMessages
	)
	{
		var errorCount = errors.IsDefault ? 0 : errors.Length;

		Dictionary<string, List<string>> groupedMessages = new(StringComparer.Ordinal);
		Dictionary<string, string[]> errorDictionary = new(errorCount, StringComparer.Ordinal);
		var issues = new ValidationIssue[errorCount];

		ErrorType? highest = null;
		var highestStatus = 0;
		var issueIndex = 0;

		foreach (var error in errors.IsDefault ? [] : errors)
		{
			var errorType = TryResolve(registry, lookup, error.Code);
			if (errorType is not null && errorType.HttpStatus > highestStatus)
			{
				highestStatus = errorType.HttpStatus;
				highest = errorType;
			}

			var key = ToProblemDetailsKey(error.Path);
			if (!groupedMessages.TryGetValue(key, out var messages))
			{
				messages = [];
				groupedMessages[key] = messages;
			}

			var message =
				formatMessages && errorType?.MessageFormat is not null
					? errorType.FormatMessage(error.Parameters)
					: error.Message;

			messages.Add(message);

			issues[issueIndex++] = new ValidationIssue
			{
				Code = error.Code,
				Category = error.Category,
				Origin = error.Origin,
				Minimum = error.Minimum,
				Maximum = error.Maximum,
				Inclusive = error.Inclusive,
				Path = error.Path.IsDefault ? [] : [.. error.Path],
				Message = message,
				Parameters = error.Parameters,
			};
		}

		foreach (var pair in groupedMessages)
			errorDictionary[pair.Key] = [.. pair.Value];

		var status = highestStatus != 0 ? highestStatus : defaultStatusCode;

		HttpValidationProblemDetails details = new(errorDictionary)
		{
			Title = highest?.Title ?? "One or more validation errors occurred.",
			Status = status,
		};

		if (highest?.Type is not null)
			details.Type = highest.Type;

		if (highest?.Description is not null)
			details.Detail = highest.Description;

		details.Extensions["issues"] = issues;

		if (lookup is not null || registry is not null)
			MergeParameters(details, errors);

		return details;
	}

	static ErrorType? TryResolve(ErrorTypeRegistry? registry, Func<string, ErrorType?>? lookup, string code) =>
		lookup is not null ? lookup(code)
		: registry is not null && registry.TryGet(code, out var errorType) ? errorType
		: null;

	static string ToProblemDetailsKey(ImmutableArray<string> path)
	{
		if (path.IsDefaultOrEmpty)
			return string.Empty;

		StringBuilder builder = new();
		for (var i = 0; i < path.Length; i++)
		{
			var segment = path[i];
			if (i > 0 && !segment.StartsWith('['))
				builder = builder.Append('.');

			builder = builder.Append(segment);
		}

		return builder.ToString();
	}

	static void MergeParameters(HttpValidationProblemDetails details, ImmutableArray<ValidationError> errors)
	{
		foreach (var error in errors.IsDefault ? [] : errors)
		{
			if (error.Parameters is null)
				continue;

			foreach (var pair in error.Parameters)
			{
				if (pair.Key is "issues" or "traceId")
					continue;

				details.Extensions[JsonNamingPolicy.CamelCase.ConvertName(pair.Key)] = pair.Value;
			}
		}
	}
}

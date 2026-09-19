using System.Collections.Immutable;
using System.Globalization;
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
					? FormatMessage(errorType.MessageFormat, error.Parameters)
					: error.Message;

			messages.Add(message);

			issues[issueIndex++] = new ValidationIssue
			{
				Code = error.Code,
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

	static string FormatMessage(string format, IReadOnlyDictionary<string, object?>? parameters)
	{
		if (parameters is null || parameters.Count == 0)
			return format;

		// Single pass over the format string; only allocate a builder when a placeholder
		// actually substitutes. Placeholders must be `{Identifier}`; `{{`/`{0}` never match,
		// and names without a matching parameter value are left as-is.
		StringBuilder? builder = null;
		var start = 0;

		for (var i = 0; i < format.Length; i++)
		{
			if (format[i] != '{' || i + 1 >= format.Length || !IsIdentifierStart(format[i + 1]))
				continue;

			var close = format.IndexOf('}', i + 1);
			if (close < 0)
				break;

			if (!IsIdentifierTail(format, i + 2, close))
			{
				i = close;
				continue;
			}

			var name = format.AsSpan(i + 1, close - i - 1);

			object? value = null;
			var found = false;
			foreach (var pair in parameters)
			{
				if (pair.Key.AsSpan().SequenceEqual(name))
				{
					value = pair.Value;
					found = true;
					break;
				}
			}

			if (!found || value is null)
			{
				i = close;
				continue;
			}

			builder ??= new StringBuilder(format.Length + 8);
			builder.Append(format, start, i - start);
			builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);

			start = close + 1;
			i = close;
		}

		if (builder is null)
			return format;

		builder.Append(format, start, format.Length - start);
		return builder.ToString();
	}

	static bool IsIdentifierStart(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';

	static bool IsIdentifierTail(string format, int start, int end)
	{
		for (var i = start; i < end; i++)
		{
			var c = format[i];
			if (c is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
				return false;
		}

		return true;
	}
}

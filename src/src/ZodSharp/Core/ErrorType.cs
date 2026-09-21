using System.Globalization;
using System.Text;

namespace ZodSharp.Core;

/// <summary>
/// Defines a user-facing error type that maps a validation error code to descriptive metadata and
/// an optional message template whose named placeholders are substituted from the error's
/// <see cref="ValidationError.Parameters"/>.
/// </summary>
/// <param name="Code">The validation error code this error type maps to.</param>
/// <param name="Category">
/// An optional category the error belongs to. A broad grouping that can span many specific codes
/// (for example code <c>invalid_tenant_id</c> with category <c>invalid_value</c>).
/// </param>
/// <param name="Description">An optional human-readable description of the error.</param>
/// <param name="HttpStatus">
/// An optional HTTP status code associated with the error. Kept here purely for convenience so the
/// ASP.NET Core integration (<c>Purview.ZodSharp.AspNetCore</c>) can derive ProblemDetails
/// responses; the core library itself does not use this value. Defaults to <c>400 Bad Request</c>.
/// </param>
/// <param name="Title">An optional title used on ProblemDetails payloads.</param>
/// <param name="Type">An optional RFC 7807 type URI used on ProblemDetails payloads.</param>
/// <param name="MessageFormat">
/// An optional message template with named placeholders (for example
/// <c>"Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"</c>). Placeholders are
/// substituted from <see cref="ValidationError.Parameters"/>; placeholders without a
/// matching value are left as-is so templating gaps stay visible.
/// </param>
/// <param name="Parameters">
/// The named placeholders expected by <see cref="MessageFormat"/>
/// together with their expected value types (for example <c>new("AggregateId", typeof(string))</c>).
/// </param>
/// <example>
/// <code>
/// public static partial class ConcurrentErrorType
/// {
///     [ErrorType]
///     public static readonly ErrorType SaveFailed = new(
///         Code: "aggregate_save_failed",
///         Category: "invalid_value",
///         Description: "The aggregate could not be saved.",
///         HttpStatus: 409,
///         MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save",
///         Parameters:
///         [
///             new("AggregateId", typeof(string)),
///             new("AggregateType", typeof(string))
///         ]);
/// }
/// </code>
/// Marking the field with <c>[ErrorType]</c> and declaring the containing class <c>partial</c>
/// lets the bundled source generator add <c>CreateSaveFailed</c>/<c>ThrowSaveFailed</c> helpers
/// with one strongly typed parameter per declared parameter.
/// </example>
public sealed record ErrorType(
	string Code,
	string? Category = null,
	string? Description = null,
	int HttpStatus = 400,
	string? Title = null,
	string? Type = null,
	string? MessageFormat = null,
	IReadOnlyList<ErrorTypeParameter>? Parameters = null
)
{
	/// <summary>
	/// Creates an error type parameter with the specified name and type.
	/// </summary>
	/// <typeparam name="T">The type represented by the parameter.</typeparam>
	/// <param name="name">The name of the parameter.</param>
	/// <returns>The created error type parameter.</returns>
	public static ErrorTypeParameter Param<T>(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		return new(name, typeof(T));
	}

	/// <summary>
	/// Formats <see cref="MessageFormat"/> using the supplied typed parameter values. Placeholders
	/// that have no matching value are left as-is so templating gaps stay visible. When
	/// <see cref="MessageFormat"/> is <c>null</c>, falls back to <see cref="Description"/> and then
	/// to <see cref="Code"/>.
	/// </summary>
	/// <param name="parameters">
	/// The typed parameter values (for example the error's <see cref="ValidationError.Parameters"/>).
	/// </param>
	public string FormatMessage(ErrorTypeParameters? parameters) =>
		FormatMessage((IReadOnlyDictionary<string, object?>?)parameters);

	/// <summary>
	/// Formats <see cref="MessageFormat"/> using the supplied named parameter values. Placeholders
	/// that have no matching value are left as-is so templating gaps stay visible. When
	/// <see cref="MessageFormat"/> is <c>null</c>, falls back to <see cref="Description"/> and then
	/// to <see cref="Code"/>.
	/// </summary>
	/// <param name="parameters">
	/// The named parameter values (for example the error's <see cref="ValidationError.Parameters"/>).
	/// </param>
	public string FormatMessage(IReadOnlyDictionary<string, object?>? parameters)
	{
		if (MessageFormat is null)
			return Description ?? Code;

		if (parameters is null || parameters.Count == 0)
			return MessageFormat;

		// Single pass over the format string; only allocate a builder when a placeholder
		// actually substitutes. Placeholders must be `{Identifier}`; `{{`/`{0}` never match,
		// and names without a matching parameter value are left as-is.
		StringBuilder? builder = null;
		var start = 0;

		for (var i = 0; i < MessageFormat.Length; i++)
		{
			if (MessageFormat[i] != '{' || i + 1 >= MessageFormat.Length || !IsIdentifierStart(MessageFormat[i + 1]))
				continue;

			var close = MessageFormat.IndexOf('}', i + 1);
			if (close < 0)
				break;

			if (!IsIdentifierTail(MessageFormat, i + 2, close))
			{
				i = close;
				continue;
			}

			var name = MessageFormat.AsSpan(i + 1, close - i - 1);

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

			builder ??= new StringBuilder(MessageFormat.Length + 8);
			builder.Append(MessageFormat, start, i - start);
			builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);

			start = close + 1;
			i = close;
		}

		if (builder is null)
			return MessageFormat;

		builder.Append(MessageFormat, start, MessageFormat.Length - start);
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

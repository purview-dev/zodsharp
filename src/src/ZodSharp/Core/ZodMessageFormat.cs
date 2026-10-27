using System.Globalization;
using System.Text;

namespace ZodSharp.Core;

/// <summary>
/// Formats validation messages. Unlike <see cref="string.Format(IFormatProvider, string, object[])"/> it never
/// throws: a message is user-facing text, and a stray brace in a custom message must not turn a validation
/// failure into an exception.
/// </summary>
/// <remarks>
/// <para>
/// Placeholders are <c>{name}</c>, <c>{name:format}</c> or positional <c>{0}</c>. Named placeholders resolve
/// from the supplied parameters first and then, when formatting an error, from its built-in fields:
/// <c>displayName</c>, <c>member</c>, <c>path</c>, <c>code</c>, <c>category</c>, <c>origin</c>, <c>minimum</c>,
/// <c>maximum</c> and <c>inclusive</c>. <c>{{</c> and <c>}}</c> are literal braces.
/// </para>
/// <para>
/// A placeholder that cannot be resolved, and any unbalanced brace, is written out literally.
/// </para>
/// </remarks>
public static class ZodMessageFormat
{
	/// <summary>
	/// Formats <paramref name="format"/> with named parameters.
	/// </summary>
	/// <param name="format">The message format.</param>
	/// <param name="parameters">The named values.</param>
	/// <param name="provider">The format provider; the invariant culture when <see langword="null"/>.</param>
	/// <returns>The formatted message.</returns>
	public static string Format(
		string format,
		IReadOnlyDictionary<string, object?>? parameters,
		IFormatProvider? provider = null
	) => FormatCore(format, default, parameters, null, provider, out _);

	/// <summary>
	/// Formats <paramref name="format"/> with positional and named values.
	/// </summary>
	/// <param name="format">The message format.</param>
	/// <param name="positional">The values for <c>{0}</c>, <c>{1}</c>, ….</param>
	/// <param name="parameters">The named values.</param>
	/// <param name="provider">The format provider; the invariant culture when <see langword="null"/>.</param>
	/// <returns>The formatted message.</returns>
	public static string Format(
		string format,
		ReadOnlySpan<object?> positional,
		IReadOnlyDictionary<string, object?>? parameters = null,
		IFormatProvider? provider = null
	) => FormatCore(format, positional, parameters, null, provider, out _);

	/// <summary>
	/// Formats <paramref name="format"/> against an error's parameters and built-in fields.
	/// </summary>
	/// <param name="format">The message format.</param>
	/// <param name="error">The error supplying values.</param>
	/// <param name="provider">The format provider; the invariant culture when <see langword="null"/>.</param>
	/// <returns>The formatted message.</returns>
	public static string Format(string format, in ValidationError error, IFormatProvider? provider = null)
	{
		var copy = error;
		return FormatCore(format, default, error.Parameters, name => TryGetBuiltIn(copy, name), provider, out _);
	}

	/// <summary>
	/// Formats <paramref name="format"/> against an error, succeeding only when every placeholder resolves. Use
	/// this when replacing an already-good message with a template, so a template is never shown with its
	/// placeholders unfilled.
	/// </summary>
	/// <param name="format">The message format.</param>
	/// <param name="error">The error supplying values.</param>
	/// <param name="message">The formatted message when every placeholder resolved.</param>
	/// <param name="provider">The format provider; the invariant culture when <see langword="null"/>.</param>
	/// <returns><see langword="true"/> when every placeholder resolved.</returns>
	public static bool TryFormatComplete(
		string format,
		in ValidationError error,
		out string message,
		IFormatProvider? provider = null
	)
	{
		var copy = error;
		message = FormatCore(
			format,
			default,
			error.Parameters,
			name => TryGetBuiltIn(copy, name),
			provider,
			out var allResolved
		);
		return allResolved;
	}

	static (bool Found, object? Value) TryGetBuiltIn(ValidationError error, string name) =>
		name switch
		{
			"path" => (true, error.FullPath.ToDottedString()),
			"code" => (true, error.Code),
			"category" => (error.Category is not null, error.Category),
			"origin" => (error.Origin is not null, error.Origin),
			"minimum" => (error.Lower is not null, error.Lower?.Value),
			"maximum" => (error.Upper is not null, error.Upper?.Value),
			"inclusive" => (error.Inclusive is not null, error.Inclusive),
			"member" => LastMember(error.FullPath),
			"displayName" => LastMember(error.FullPath),
			_ => (false, null),
		};

	static (bool Found, object? Value) LastMember(ZodPath path)
	{
		for (var i = path.Length - 1; i >= 0; i--)
		{
			if (path[i].Kind == ZodPathSegmentKind.Member)
				return (true, path[i].Name);
		}

		return (false, null);
	}

	static string FormatCore(
		string format,
		ReadOnlySpan<object?> positional,
		IReadOnlyDictionary<string, object?>? parameters,
		Func<string, (bool Found, object? Value)>? fallback,
		IFormatProvider? provider,
		out bool allResolved
	)
	{
		allResolved = true;
		if (string.IsNullOrEmpty(format) || (format.IndexOf('{') < 0 && format.IndexOf('}') < 0))
			return format ?? string.Empty;

		provider ??= CultureInfo.InvariantCulture;
		StringBuilder builder = new(format.Length + 16);
		var span = format.AsSpan();
		var i = 0;
		while (i < span.Length)
		{
			var c = span[i];
			if (c == '{')
			{
				if (i + 1 < span.Length && span[i + 1] == '{')
				{
					builder.Append('{');
					i += 2;
					continue;
				}

				var close = span[(i + 1)..].IndexOfAny('{', '}');
				if (close < 0 || span[i + 1 + close] == '{')
				{
					// Unbalanced: write the brace literally and carry on.
					builder.Append('{');
					i++;
					continue;
				}

				var token = span.Slice(i + 1, close);
				var placeholder = span.Slice(i, close + 2);
				i += close + 2;

				if (TryResolve(token, positional, parameters, fallback, provider, out var text))
				{
					builder.Append(text);
				}
				else
				{
					allResolved = false;
					builder.Append(placeholder);
				}

				continue;
			}

			if (c == '}')
			{
				builder.Append('}');
				i += i + 1 < span.Length && span[i + 1] == '}' ? 2 : 1;
				continue;
			}

			builder.Append(c);
			i++;
		}

		return builder.ToString();
	}

	static bool TryResolve(
		ReadOnlySpan<char> token,
		ReadOnlySpan<object?> positional,
		IReadOnlyDictionary<string, object?>? parameters,
		Func<string, (bool Found, object? Value)>? fallback,
		IFormatProvider provider,
		out string text
	)
	{
		text = string.Empty;

		var formatSeparator = token.IndexOf(':');
		var nameAndAlignment = formatSeparator < 0 ? token : token[..formatSeparator];
		var itemFormat = formatSeparator < 0 ? null : token[(formatSeparator + 1)..].ToString();

		var alignmentSeparator = nameAndAlignment.IndexOf(',');
		var nameSpan = (alignmentSeparator < 0 ? nameAndAlignment : nameAndAlignment[..alignmentSeparator]).Trim();
		var alignment = 0;
		if (
			alignmentSeparator >= 0
			&& !int.TryParse(
				nameAndAlignment[(alignmentSeparator + 1)..].Trim(),
				NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture,
				out alignment
			)
		)
		{
			return false;
		}

		if (nameSpan.IsEmpty)
			return false;

		object? value;
		if (char.IsAsciiDigit(nameSpan[0]))
		{
			if (
				!int.TryParse(nameSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
				|| index >= positional.Length
			)
			{
				return false;
			}

			value = positional[index];
		}
		else
		{
			var name = nameSpan.ToString();
			if (parameters is not null && parameters.TryGetValue(name, out var named))
			{
				value = named;
			}
			else if (fallback?.Invoke(name) is (true, var builtIn))
			{
				value = builtIn;
			}
			else
			{
				return false;
			}
		}

		text = value switch
		{
			null => string.Empty,
			IFormattable formattable => formattable.ToString(itemFormat, provider),
			_ => value.ToString() ?? string.Empty,
		};

		if (alignment != 0)
			text = alignment > 0 ? text.PadLeft(alignment) : text.PadRight(-alignment);

		return true;
	}
}

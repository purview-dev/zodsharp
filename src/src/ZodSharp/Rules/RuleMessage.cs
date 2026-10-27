using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using ZodSharp.Core;

namespace ZodSharp.Rules;

/// <summary>
/// Formats rule error messages from the public message-format constants exposed by each rule, caching the
/// parsed <see cref="CompositeFormat"/> so a format string is compiled once rather than on every failure.
/// </summary>
/// <remarks>
/// <para>
/// Messages are formatted with the invariant culture, so the same failure produces the same text on every
/// thread and platform; hosts localize at the boundary (see <see cref="ZodMessageFormat"/>).
/// </para>
/// <para>
/// Formatting never throws. A custom message is user text, so a format that is not a valid composite format
/// (a stray <c>{</c>, or a placeholder index the rule does not supply) is formatted leniently instead, with
/// anything unresolvable written out literally.
/// </para>
/// </remarks>
public static class RuleMessage
{
	static readonly ConcurrentDictionary<string, CompositeFormat?> Formats = new(StringComparer.Ordinal);

	/// <summary>Formats a rule message with a single argument.</summary>
	/// <typeparam name="TArg0">The argument type.</typeparam>
	/// <param name="format">The message format (a rule's <c>MessageFormat</c> constant).</param>
	/// <param name="arg0">The format argument.</param>
	/// <returns>The formatted message using <see cref="CultureInfo.InvariantCulture"/>.</returns>
	public static string Format<TArg0>(string format, TArg0 arg0) =>
		GetFormat(format, 1) is { } composite
			? string.Format(CultureInfo.InvariantCulture, composite, arg0)
			: ZodMessageFormat.Format(format, [arg0], provider: CultureInfo.InvariantCulture);

	/// <summary>Formats a rule message with two arguments.</summary>
	/// <typeparam name="TArg0">The first argument type.</typeparam>
	/// <typeparam name="TArg1">The second argument type.</typeparam>
	/// <param name="format">The message format (a rule's <c>MessageFormat</c> constant).</param>
	/// <param name="arg0">The first format argument.</param>
	/// <param name="arg1">The second format argument.</param>
	/// <returns>The formatted message using <see cref="CultureInfo.InvariantCulture"/>.</returns>
	public static string Format<TArg0, TArg1>(string format, TArg0 arg0, TArg1 arg1) =>
		GetFormat(format, 2) is { } composite
			? string.Format(CultureInfo.InvariantCulture, composite, arg0, arg1)
			: ZodMessageFormat.Format(format, [arg0, arg1], provider: CultureInfo.InvariantCulture);

	/// <summary>Formats a rule message with three arguments.</summary>
	/// <typeparam name="TArg0">The first argument type.</typeparam>
	/// <typeparam name="TArg1">The second argument type.</typeparam>
	/// <typeparam name="TArg2">The third argument type.</typeparam>
	/// <param name="format">The message format (a rule's <c>MessageFormat</c> constant).</param>
	/// <param name="arg0">The first format argument.</param>
	/// <param name="arg1">The second format argument.</param>
	/// <param name="arg2">The third format argument.</param>
	/// <returns>The formatted message using <see cref="CultureInfo.InvariantCulture"/>.</returns>
	public static string Format<TArg0, TArg1, TArg2>(string format, TArg0 arg0, TArg1 arg1, TArg2 arg2) =>
		GetFormat(format, 3) is { } composite
			? string.Format(CultureInfo.InvariantCulture, composite, arg0, arg1, arg2)
			: ZodMessageFormat.Format(format, [arg0, arg1, arg2], provider: CultureInfo.InvariantCulture);

	/// <summary>
	/// Builds the named template a rule reports as <see cref="IZodRule.MessageFormat"/> from its positional
	/// message format: <c>{0}</c> becomes <c>{names[0]}</c>, and so on. Alignment and format specifiers are kept,
	/// escaped braces and positions without a name are left as they are. Never throws.
	/// </summary>
	/// <param name="format">The positional format (the rule's <c>MessageFormat</c>, or a custom message).</param>
	/// <param name="names">The name each position binds to, in the rule's argument order.</param>
	/// <returns>The named template.</returns>
	internal static string Template(string format, params ReadOnlySpan<string?> names) =>
		Template(format, -1, null, names);

	/// <summary>
	/// Builds a named template like <see cref="Template(string, ReadOnlySpan{string?})"/>, writing the
	/// placeholder at <paramref name="literalPosition"/> as <paramref name="literal"/>. Used for a position that
	/// carries rule configuration rather than a reported parameter (for example the address family of an
	/// <see cref="IPAddressRule"/>).
	/// </summary>
	/// <param name="format">The positional format.</param>
	/// <param name="literalPosition">The position replaced by <paramref name="literal"/>.</param>
	/// <param name="literal">The text written for <paramref name="literalPosition"/>; braces are escaped.</param>
	/// <param name="names">The name each other position binds to.</param>
	/// <returns>The named template.</returns>
	internal static string Template(
		string format,
		int literalPosition,
		string? literal,
		params ReadOnlySpan<string?> names
	)
	{
		if (string.IsNullOrEmpty(format) || format.IndexOf('{', StringComparison.Ordinal) < 0)
			return format ?? string.Empty;

		StringBuilder builder = new(format.Length + 16);
		var i = 0;
		while (i < format.Length)
		{
			var c = format[i];
			if (c != '{')
			{
				builder.Append(c);
				i++;
				continue;
			}

			if (i + 1 < format.Length && format[i + 1] == '{')
			{
				builder.Append("{{");
				i += 2;
				continue;
			}

			var start = i + 1;
			var end = start;
			while (end < format.Length && char.IsAsciiDigit(format[end]))
				end++;

			if (
				end > start
				&& end < format.Length
				&& format[end] is '}' or ',' or ':'
				&& int.TryParse(
					format.AsSpan(start, end - start),
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out var position
				)
			)
			{
				if (position == literalPosition && literal is not null)
				{
					var close = format.IndexOf('}', end);
					if (close > 0)
					{
						builder.Append(
							literal
								.Replace("{", "{{", StringComparison.Ordinal)
								.Replace("}", "}}", StringComparison.Ordinal)
						);
						i = close + 1;
						continue;
					}
				}
				else if (position < names.Length && names[position] is { Length: > 0 } name)
				{
					builder.Append('{').Append(name);
					i = end;
					continue;
				}
			}

			builder.Append(c);
			i++;
		}

		return builder.ToString();
	}

	/// <summary>The template name of a lower bound, resolved from <see cref="ValidationError.Lower"/>.</summary>
	internal const string Minimum = "minimum";

	/// <summary>The template name of an upper bound, resolved from <see cref="ValidationError.Upper"/>.</summary>
	internal const string Maximum = "maximum";

	/// <summary>
	/// The template name of the failing value. It resolves only when
	/// <see cref="ZodErrorOptions.IncludeReceivedValues"/> is enabled; otherwise a host keeps the produced message.
	/// </summary>
	internal const string Received = ZodIssueParameters.Received;

	static CompositeFormat? GetFormat(string format, int argumentCount)
	{
		var composite = Formats.GetOrAdd(format, static f => TryParse(f));
		return composite is not null && composite.MinimumArgumentCount <= argumentCount ? composite : null;
	}

	static CompositeFormat? TryParse(string format)
	{
		try
		{
			return CompositeFormat.Parse(format);
		}
		catch (FormatException)
		{
			return null;
		}
	}
}

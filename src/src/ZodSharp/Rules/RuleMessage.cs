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

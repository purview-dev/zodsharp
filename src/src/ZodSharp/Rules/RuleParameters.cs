using System.Collections.Concurrent;
using ZodSharp.Core;

namespace ZodSharp.Rules;

/// <summary>
/// Builds the <see cref="IZodRule.Parameters"/> of the built-in rules. Parameter sets that do not depend on a
/// rule's configuration are cached, so reporting a failure does not allocate them again.
/// </summary>
static class RuleParameters
{
	static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, object?>> Formats = new(
		StringComparer.Ordinal
	);

	/// <summary>The cached <c>{ format: name }</c> parameters of a string format rule.</summary>
	public static IReadOnlyDictionary<string, object?> Format(string format) =>
		Formats.GetOrAdd(format, static name => One(ZodIssueParameters.Format, name));

	/// <summary>A single parameter.</summary>
	public static IReadOnlyDictionary<string, object?> One(string name, object? value) =>
		new Dictionary<string, object?>(1, StringComparer.Ordinal) { [name] = value };

	/// <summary>Two parameters.</summary>
	public static IReadOnlyDictionary<string, object?> Two(
		string name1,
		object? value1,
		string name2,
		object? value2
	) => new Dictionary<string, object?>(2, StringComparer.Ordinal) { [name1] = value1, [name2] = value2 };

	/// <summary>The <c>{ count: n }</c> parameters of a size failure.</summary>
	public static IReadOnlyDictionary<string, object?> Count(int count) => One(ZodIssueParameters.Count, count);
}

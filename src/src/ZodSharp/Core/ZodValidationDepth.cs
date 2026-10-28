using System.Diagnostics.CodeAnalysis;

namespace ZodSharp.Core;

/// <summary>
/// The nesting limit generated validators enforce while walking an object graph. Matches
/// Microsoft.Extensions.Validation's <c>ValidationOptions.MaxDepth</c> (default <c>32</c>): a graph nested
/// deeper than the limit — almost always a cycle — fails with <see cref="InvalidOperationException"/> instead
/// of overflowing the stack.
/// </summary>
/// <remarks>
/// The root value is validated at depth <c>0</c> and every nested schema call adds one, so validation throws
/// when a value at depth <see cref="MaxDepth"/> is reached.
/// </remarks>
public static class ZodValidationDepth
{
	/// <summary>
	/// The default limit, matching Microsoft.Extensions.Validation.
	/// </summary>
	public const int DefaultMaxDepth = 32;

	/// <summary>
	/// Gets or sets the maximum nesting depth. Process-wide; hosts that configure
	/// <c>ValidationOptions.MaxDepth</c> may mirror the value here.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
	public static int MaxDepth
	{
		get => Volatile.Read(ref field);
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
			Volatile.Write(ref field, value);
		}
	} = DefaultMaxDepth;

	/// <summary>
	/// Throws when <paramref name="depth"/> has reached <see cref="MaxDepth"/>.
	/// </summary>
	/// <param name="depth">The depth of the value being validated (the root is <c>0</c>).</param>
	/// <param name="type">The type being validated, named in the exception message.</param>
	/// <exception cref="InvalidOperationException">The depth limit was reached.</exception>
	public static void ThrowIfExceeded(int depth, Type type)
	{
		var maxDepth = MaxDepth;
		if (depth >= maxDepth)
			ThrowExceeded(maxDepth, type);
	}

	[DoesNotReturn]
	static void ThrowExceeded(int maxDepth, Type type) =>
		throw new InvalidOperationException(
			$"Maximum validation depth of {maxDepth} exceeded in '{type?.Name}'. This is likely caused by a circular reference in the object graph. Consider increasing ZodValidationDepth.MaxDepth if deeper validation is required."
		);
}

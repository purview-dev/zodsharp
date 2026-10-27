namespace ZodSharp.Core;

/// <summary>
/// A bound reported by a size or range issue. Unlike the integer <see cref="ValidationError.Minimum"/> and
/// <see cref="ValidationError.Maximum"/>, a bound can carry any comparable value (a <see cref="double"/>,
/// <see cref="decimal"/>, <see cref="DateTime"/> and so on) and records whether it is inclusive, so
/// <c>&gt;</c> and <c>&gt;=</c> can be told apart.
/// </summary>
/// <param name="Value">The bound value.</param>
/// <param name="Inclusive">Whether a value equal to the bound satisfies it.</param>
public readonly record struct ErrorBound(object Value, bool Inclusive)
{
	/// <summary>
	/// Creates an inclusive bound.
	/// </summary>
	/// <param name="value">The bound value.</param>
	/// <returns>The bound.</returns>
	public static ErrorBound Including(object value) => new(value, true);

	/// <summary>
	/// Creates an exclusive bound.
	/// </summary>
	/// <param name="value">The bound value.</param>
	/// <returns>The bound.</returns>
	public static ErrorBound Excluding(object value) => new(value, false);
}

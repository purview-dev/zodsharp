using System.Collections;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a collection's element (or entry) count to be within an inclusive range. The
/// trim- and Native AOT-safe counterpart of <c>[Length]</c> on a collection member.
/// </summary>
/// <remarks>
/// The generated <c>[CountRange]</c> attribute is what a <c>[ZodSchema]</c> model uses: the generator emits the
/// count check itself, with the same error identity as <c>[Length]</c> on the same member (<c>too_small</c> below
/// the range and <c>too_big</c> above it), without the BCL attribute's <c>[RequiresUnreferencedCode]</c>
/// constructor.
/// </remarks>
[Core.ZodRule]
public readonly record struct CountRangeRule : Core.IValidationRule<IEnumerable>, Core.IZodRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_count";

	/// <summary>Gets the message format; <c>{0}</c> is the minimum, <c>{1}</c> the maximum and <c>{2}</c> the actual count.</summary>
	public const string MessageFormat = "Collection must contain between {0} and {1} element(s), but got {2}";

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="CountRangeRule"/> struct.
	/// </summary>
	/// <param name="minimum">The inclusive minimum number of elements.</param>
	/// <param name="maximum">The inclusive maximum number of elements.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public CountRangeRule(int minimum, int maximum, string? message = null, string? code = null)
	{
		Minimum = minimum;
		Maximum = maximum;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>Gets the inclusive minimum number of elements.</summary>
	public int Minimum { get; }

	/// <summary>Gets the inclusive maximum number of elements.</summary>
	public int Maximum { get; }

	/// <summary>
	/// Validates that the collection's count is within the configured range. A <see langword="null"/> collection is
	/// not counted.
	/// </summary>
	/// <param name="value">The collection to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	public bool IsValid(in IEnumerable value)
	{
		if (value is null)
			return true;

		var count = CollectionCount.Of(value);
		return count >= Minimum && count <= Maximum;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The collection that failed validation.</param>
	/// <returns>The error message.</returns>
	public string GetErrorMessage(in IEnumerable value) =>
		RuleMessage.Format(_message ?? MessageFormat, Minimum, Maximum, CollectionCount.Of(value));

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Array;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(Minimum);

	Core.ErrorBound? Core.IZodRule.Upper => Core.ErrorBound.Including(Maximum);
}

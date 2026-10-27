namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a comparable value to fall within a minimum/maximum range, mirroring
/// <c>System.ComponentModel.DataAnnotations.RangeAttribute</c> with both bounds expressed in one rule.
/// </summary>
/// <typeparam name="T">The comparable type the rule validates.</typeparam>
/// <remarks>
/// Each bound is inclusive unless the matching <c>minimumIsExclusive</c>/<c>maximumIsExclusive</c> flag is
/// set. The generated <c>[RangeZod]</c> attribute surfaces the type-parameter bounds as <c>double</c> values,
/// so the same attribute applies to an <c>int</c> or a <c>double</c> member; the name is suffixed with
/// <c>Zod</c> because <c>RangeAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </remarks>
[Core.ZodRule]
public readonly record struct RangeRule<T> : Core.IValidationRule<T>, Core.IZodRule, Core.IZodFailureDescriber<T>
	where T : IComparable<T>
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_range";

	/// <summary>Gets the message format; <c>{0}</c> is the minimum, <c>{1}</c> the maximum and <c>{2}</c> the value.</summary>
	public const string MessageFormat = "Value must be between {0} and {1}, but got {2}";

	readonly T _minimum;
	readonly T _maximum;
	readonly bool _minimumIsExclusive;
	readonly bool _maximumIsExclusive;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="RangeRule{T}"/> struct.
	/// </summary>
	/// <param name="minimum">The lower bound.</param>
	/// <param name="maximum">The upper bound.</param>
	/// <param name="minimumIsExclusive">Whether the lower bound is exclusive. Defaults to <see langword="false"/>.</param>
	/// <param name="maximumIsExclusive">Whether the upper bound is exclusive. Defaults to <see langword="false"/>.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public RangeRule(
		T minimum,
		T maximum,
		bool minimumIsExclusive = false,
		bool maximumIsExclusive = false,
		string? message = null,
		string? code = null
	)
	{
		_minimum = minimum;
		_maximum = maximum;
		_minimumIsExclusive = minimumIsExclusive;
		_maximumIsExclusive = maximumIsExclusive;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value falls within the configured range.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value)
	{
		var aboveMinimum = _minimumIsExclusive ? value.CompareTo(_minimum) > 0 : value.CompareTo(_minimum) >= 0;

		if (!aboveMinimum)
			return false;

		// If the value is above the minimum, check if it is below the maximum.
		return _maximumIsExclusive ? value.CompareTo(_maximum) < 0 : value.CompareTo(_maximum) <= 0;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) =>
		RuleMessage.Format(_message ?? MessageFormat, _minimum, _maximum, value);

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Of(typeof(T));

	Core.ErrorBound? Core.IZodRule.Lower => new Core.ErrorBound(_minimum, !_minimumIsExclusive);

	Core.ErrorBound? Core.IZodRule.Upper => new Core.ErrorBound(_maximum, !_maximumIsExclusive);

	// The Zod issue code follows the bound that failed.
	Core.ZodRuleFailure Core.IZodFailureDescriber<T>.DescribeFailure(in T value)
	{
		var comparison = value.CompareTo(_minimum);
		var belowMinimum = _minimumIsExclusive ? comparison <= 0 : comparison < 0;
		return new(belowMinimum ? Core.ZodIssueCategories.TooSmall : Core.ZodIssueCategories.TooBig);
	}
}

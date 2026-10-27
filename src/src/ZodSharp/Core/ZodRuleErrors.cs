namespace ZodSharp.Core;

/// <summary>
/// Builds the <see cref="ValidationError"/> reported for a failed rule, copying the rule's structured identity
/// (<see cref="IZodRule"/>) so runtime schemas and generated validators report exactly the same error for the
/// same rule.
/// </summary>
public static class ZodRuleErrors
{
	/// <summary>
	/// Creates the error for a failed <see cref="IValidationRule{T}"/>.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="rule">The rule that failed.</param>
	/// <param name="value">The value that failed.</param>
	/// <param name="path">The error path.</param>
	/// <param name="fallbackOrigin">The origin used when the rule does not report one.</param>
	/// <param name="message">A message that replaces the rule's own message (for example an attribute's <c>ErrorMessage</c>).</param>
	/// <param name="code">A code that replaces the rule's own code.</param>
	/// <returns>The error.</returns>
	public static ValidationError Create<T>(
		IValidationRule<T> rule,
		in T value,
		ZodPath path,
		string? fallbackOrigin = null,
		string? message = null,
		string? code = null
	)
	{
		ArgumentNullException.ThrowIfNull(rule);
		var zodRule = rule as IZodRule;
		return Build(
			code ?? (zodRule?.Code is { } ruleCode ? ruleCode : rule.Code),
			message ?? rule.GetErrorMessage(value),
			path,
			zodRule,
			fallbackOrigin
		);
	}

	/// <summary>
	/// Creates the error for a failed <see cref="IStringValidationRule"/>.
	/// </summary>
	/// <param name="rule">The rule that failed.</param>
	/// <param name="value">The value that failed.</param>
	/// <param name="path">The error path.</param>
	/// <param name="fallbackOrigin">The origin used when the rule does not report one.</param>
	/// <param name="message">A message that replaces the rule's own message.</param>
	/// <param name="code">A code that replaces the rule's own code.</param>
	/// <returns>The error.</returns>
	public static ValidationError Create(
		IStringValidationRule rule,
		ReadOnlySpan<char> value,
		ZodPath path,
		string? fallbackOrigin = "string",
		string? message = null,
		string? code = null
	)
	{
		ArgumentNullException.ThrowIfNull(rule);
		var zodRule = rule as IZodRule;
		return Build(
			code ?? (zodRule?.Code is { } ruleCode ? ruleCode : rule.Code),
			message ?? rule.GetErrorMessage(value),
			path,
			zodRule,
			fallbackOrigin
		);
	}

	/// <summary>
	/// Creates an error from a rule's structured identity with an explicit code and message.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="path">The error path.</param>
	/// <param name="rule">The rule supplying the structured identity, if any.</param>
	/// <param name="fallbackOrigin">The origin used when the rule does not report one.</param>
	/// <returns>The error.</returns>
	public static ValidationError Build(
		string code,
		string message,
		ZodPath path,
		IZodRule? rule,
		string? fallbackOrigin = null
	)
	{
		var lower = rule?.Lower;
		var upper = rule?.Upper;
		var minimum = lower is { Value: int lowerValue } ? lowerValue : (int?)null;
		var maximum = upper is { Value: int upperValue } ? upperValue : (int?)null;
		var inclusive = lower?.Inclusive ?? upper?.Inclusive;

		return ValidationError.Create(
			code,
			message,
			path,
			rule?.Parameters,
			rule?.Origin ?? fallbackOrigin,
			minimum,
			maximum,
			inclusive,
			rule?.Category
		) with
		{
			Lower = lower,
			Upper = upper,
			MessageFormat = rule?.MessageFormat,
		};
	}
}

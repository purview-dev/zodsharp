namespace ZodSharp.Core;

/// <summary>
/// Builds the <see cref="ValidationError"/> reported for a failed rule, copying the rule's structured identity
/// (<see cref="IZodRule"/>) so runtime schemas and generated validators report exactly the same error for the
/// same rule.
/// </summary>
/// <remarks>
/// When the rule implements <see cref="IZodFailureDescriber{T}"/> (or <see cref="IZodStringFailureDescriber"/>
/// for spans) its value-dependent category and parameters are applied too. The raw input is added as the
/// <see cref="ZodIssueParameters.Received"/> parameter only when <see cref="ZodErrorOptions.IncludeReceivedValues"/>
/// is enabled.
/// </remarks>
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
		var failure = rule is IZodFailureDescriber<T> describer ? describer.DescribeFailure(value) : default;
		var includeReceived = ZodErrorOptions.IncludeReceivedValues;
		return BuildCore(
			ResolveCode(code, zodRule?.Code is { } ruleCode ? ruleCode : rule.Code, failure),
			message ?? rule.GetErrorMessage(value),
			path,
			zodRule,
			fallbackOrigin,
			failure,
			includeReceived,
			includeReceived ? value : null
		);
	}

	// A failure's own code (a direction-dependent code) replaces the rule's code, including when the caller
	// passes the rule's code back explicitly as generated validators do; a different explicit code wins.
	static string ResolveCode(string? code, string ruleCode, ZodRuleFailure failure) =>
		failure.Code is { } failureCode && (code is null || string.Equals(code, ruleCode, StringComparison.Ordinal))
			? failureCode
			: code ?? ruleCode;

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
		var failure = rule is IZodStringFailureDescriber describer ? describer.DescribeFailure(value) : default;
		var includeReceived = ZodErrorOptions.IncludeReceivedValues;
		return BuildCore(
			ResolveCode(code, zodRule?.Code is { } ruleCode ? ruleCode : rule.Code, failure),
			message ?? rule.GetErrorMessage(value),
			path,
			zodRule,
			fallbackOrigin,
			failure,
			includeReceived,
			includeReceived ? value.ToString() : null
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
	) => BuildCore(code, message, path, rule, fallbackOrigin, default, false, null);

	/// <summary>
	/// Creates an error from a rule's structured identity with an explicit code and message, applying a
	/// value-dependent <paramref name="failure"/> description.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="path">The error path.</param>
	/// <param name="rule">The rule supplying the structured identity, if any.</param>
	/// <param name="failure">The value-dependent category and parameters.</param>
	/// <param name="fallbackOrigin">The origin used when the rule does not report one.</param>
	/// <returns>The error.</returns>
	public static ValidationError Build(
		string code,
		string message,
		ZodPath path,
		IZodRule? rule,
		ZodRuleFailure failure,
		string? fallbackOrigin = null
	) =>
		BuildCore(
			rule?.Code is { } ruleCode ? ResolveCode(code, ruleCode, failure) : code,
			message,
			path,
			rule,
			fallbackOrigin,
			failure,
			false,
			null
		);

	static ValidationError BuildCore(
		string code,
		string message,
		ZodPath path,
		IZodRule? rule,
		string? fallbackOrigin,
		ZodRuleFailure failure,
		bool includeReceived,
		object? received
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
			MergeParameters(rule?.Parameters, failure.Parameters, includeReceived, received),
			rule?.Origin ?? fallbackOrigin,
			minimum,
			maximum,
			inclusive,
			failure.Category ?? rule?.Category
		) with
		{
			Lower = lower,
			Upper = upper,
			MessageFormat = rule?.MessageFormat,
		};
	}

	static IReadOnlyDictionary<string, object?>? MergeParameters(
		IReadOnlyDictionary<string, object?>? ruleParameters,
		IReadOnlyDictionary<string, object?>? failureParameters,
		bool includeReceived,
		object? received
	)
	{
		if (failureParameters is null && !includeReceived)
			return ruleParameters;

		if (ruleParameters is null && !includeReceived)
			return failureParameters;

		Dictionary<string, object?> merged = [with(StringComparer.Ordinal)];
		if (ruleParameters is not null)
		{
			foreach (var pair in ruleParameters)
				merged[pair.Key] = pair.Value;
		}

		if (failureParameters is not null)
		{
			foreach (var pair in failureParameters)
				merged[pair.Key] = pair.Value;
		}

		if (includeReceived)
			merged[ZodIssueParameters.Received] = received;

		return merged;
	}
}

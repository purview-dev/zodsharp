namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a string to have a length within an inclusive minimum/maximum range,
/// mirroring <c>System.ComponentModel.DataAnnotations.LengthAttribute</c>.
/// </summary>
/// <remarks>
/// The generated attribute is named <c>[LengthZod]</c> because <c>LengthAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </remarks>
[Core.ZodRule]
public readonly record struct LengthRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		Core.IZodFailureDescriber<string>,
		Core.IZodStringFailureDescriber
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_length";

	/// <summary>Gets the message format; <c>{0}</c> is the minimum, <c>{1}</c> the maximum and <c>{2}</c> the actual length.</summary>
	public const string MessageFormat = "String length must be between {0} and {1} characters, but got {2}";

	static readonly string?[] TemplateNames = [RuleMessage.Minimum, RuleMessage.Maximum, Core.ZodIssueParameters.Count];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly int _minimum;
	readonly int _maximum;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="LengthRule"/> struct.
	/// </summary>
	/// <param name="minimum">The inclusive minimum length.</param>
	/// <param name="maximum">The inclusive maximum length.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public LengthRule(int minimum, int maximum, string? message = null, string? code = null)
	{
		_minimum = minimum;
		_maximum = maximum;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value's length is within the configured range.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value)
	{
		var length = value.LengthOrDefault();
		return length >= _minimum && length <= _maximum;
	}

	/// <summary>
	/// Validates that the span's length is within the configured range without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Length >= _minimum && value.Length <= _maximum;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		RuleMessage.Format(_message ?? MessageFormat, _minimum, _maximum, value.LengthOrDefault());

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, _minimum, _maximum, value.Length);

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(_minimum);

	Core.ErrorBound? Core.IZodRule.Upper => Core.ErrorBound.Including(_maximum);

	Core.ZodRuleFailure Core.IZodFailureDescriber<string>.DescribeFailure(in string value) =>
		Describe(value.LengthOrDefault());

	Core.ZodRuleFailure Core.IZodStringFailureDescriber.DescribeFailure(ReadOnlySpan<char> value) =>
		Describe(value.Length);

	// The Zod issue code follows the bound that failed.
	Core.ZodRuleFailure Describe(int length) =>
		new(
			length < _minimum ? Core.ZodIssueCategories.TooSmall : Core.ZodIssueCategories.TooBig,
			RuleParameters.Count(length)
		);
}

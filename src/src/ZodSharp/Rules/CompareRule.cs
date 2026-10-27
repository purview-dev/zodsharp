namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a string to equal a fixed value, mirroring the common use of
/// <c>System.ComponentModel.DataAnnotations.CompareAttribute</c> for a confirmation field.
/// </summary>
/// <remarks>
/// A rule cannot reference another property, so the expected value is supplied directly. The generated
/// attribute is named <c>[CompareZod]</c> because <c>CompareAttribute</c> is already declared by
/// <c>System.ComponentModel.DataAnnotations</c>.
/// </remarks>
[Core.ZodRule]
public readonly record struct CompareRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "mismatch";

	/// <summary>Gets the default message format, which contains neither value; a custom message may use <c>{0}</c> for the expected value and <c>{1}</c> for the actual value.</summary>
	public const string MessageFormat = "Value does not match the expected value";

	static readonly string?[] TemplateNames = [null, RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly string _other;
	readonly StringComparison _comparison;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompareRule"/> struct.
	/// </summary>
	/// <param name="other">The value the validated string must equal.</param>
	/// <param name="comparison">The string comparison to use. Defaults to <see cref="StringComparison.Ordinal"/>.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public CompareRule(
		string other,
		StringComparison comparison = StringComparison.Ordinal,
		string? message = null,
		string? code = null
	)
	{
		_other = other.OrNull() ?? throw new ArgumentNullException(nameof(other));
		_comparison = comparison;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value equals the expected value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && value.Equals(_other, _comparison);

	/// <summary>
	/// Validates that the span equals the expected value without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Equals(_other.AsSpan(), _comparison);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, _other, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, _other, value.ToString());

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidValue;
}

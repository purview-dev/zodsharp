namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for credit card numbers using the Luhn algorithm.
/// Mirrors the behavior of System.ComponentModel.DataAnnotations.CreditCardAttribute.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct CreditCardRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid credit card number format";

	static readonly string?[] TemplateNames = [RuleMessage.Received];

	static readonly string DefaultTemplate = RuleMessage.Template(MessageFormat, TemplateNames);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the CreditCardRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public CreditCardRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a valid credit card number.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		// Use AsSpan to avoid allocations
		return IsValid(value.AsSpan());
	}

	/// <summary>
	/// Validates that the span is a valid credit card number without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsWhiteSpace())
			return false;

		var sum = 0;
		var digitCount = 0;

		for (var i = value.Length - 1; i >= 0; i--)
		{
			var c = value[i];
			if (c is ' ' or '-')
				continue;

			if (!char.IsDigit(c))
				return false;

			var digit = c - '0';
			if ((digitCount & 1) is 1)
			{
				digit *= 2;
				if (digit > 9)
					digit -= 9;
			}

			sum += digit;
			digitCount++;
		}

		return digitCount > 0 && sum % 10 == 0;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, value.ToString());

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.MessageFormat =>
		_message is null ? DefaultTemplate : RuleMessage.Template(_message, TemplateNames);

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("credit_card");
}

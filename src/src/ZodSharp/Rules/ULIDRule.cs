namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for ULID format: 26 Crockford base32 characters.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct ULIDRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid ULID";

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the ULIDRule struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public ULIDRule(string? message = null, string? code = null)
	{
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a ULID.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is a ULID without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.Length != 26)
			return false;

		// The first character encodes the 48-bit timestamp, so it is always 0-7 (Zod parity).
		if (value[0] is < '0' or > '7')
			return false;

		foreach (var c in value)
		{
			if (!IsCrockfordBase32(c))
				return false;
		}

		return true;
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

	static bool IsCrockfordBase32(char c) =>
		c switch
		{
			>= '0' and <= '9' => true,
			>= 'A' and <= 'H' => true,
			>= 'J' and <= 'K' => true,
			>= 'M' and <= 'N' => true,
			>= 'P' and <= 'T' => true,
			>= 'V' and <= 'Z' => true,
			>= 'a' and <= 'h' => true,
			>= 'j' and <= 'k' => true,
			>= 'm' and <= 'n' => true,
			>= 'p' and <= 't' => true,
			>= 'v' and <= 'z' => true,
			_ => false,
		};

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format("ulid");
}

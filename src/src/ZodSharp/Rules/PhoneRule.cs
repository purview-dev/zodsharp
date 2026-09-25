namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for phone number format.
/// Mirrors the behavior of System.ComponentModel.DataAnnotations.PhoneAttribute:
/// allows digits and the characters () . + -, and requires at least one digit.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct PhoneRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the PhoneRule struct.
	/// </summary>
	/// <param name="message">Optional error message</param>
	public PhoneRule(string? message = null)
	{
		_message = message.OrNull();
	}

	/// <summary>
	/// Validates that the value is a valid phone number.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is a valid phone number without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsWhiteSpace())
			return false;

		const string additionalChars = "() .+-";
		var hasDigit = false;

		foreach (var c in value)
		{
			if (char.IsDigit(c))
			{
				hasDigit = true;
				continue;
			}

			if (additionalChars.Contains(c, StringComparison.Ordinal))
				continue;

			return false;
		}

		return hasDigit;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => _message ?? $"Invalid phone number format: {value}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) => _message ?? $"Invalid phone number format: {value}";
}

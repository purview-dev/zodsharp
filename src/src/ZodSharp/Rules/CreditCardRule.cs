namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for credit card numbers using the Luhn algorithm.
/// Mirrors the behavior of System.ComponentModel.DataAnnotations.CreditCardAttribute.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct CreditCardRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the CreditCardRule struct.
	/// </summary>
	/// <param name="message">Optional error message</param>
	public CreditCardRule(string? message = null)
	{
		_message = message.OrNull();
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
	public string GetErrorMessage(in string value) => _message ?? $"Invalid credit card number format: {value}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		_message ?? $"Invalid credit card number format: {value}";
}

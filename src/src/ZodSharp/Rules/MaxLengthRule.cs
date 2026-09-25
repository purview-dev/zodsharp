namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for maximum string length.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct MaxLengthRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly int _maxLength;

	/// <summary>
	/// Initializes a new instance of the MaxLengthRule struct.
	/// </summary>
	/// <param name="maxLength">The maximum length</param>
	public MaxLengthRule(int maxLength)
	{
		_maxLength = maxLength;
	}

	/// <summary>
	/// Validates that the value meets the maximum length requirement.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value.LengthOrDefault() <= _maxLength;

	/// <summary>
	/// Validates that the span meets the maximum length requirement without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Length <= _maxLength;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		$"String must be at most {_maxLength} characters long, but got {value.LengthOrDefault()}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		$"String must be at most {_maxLength} characters long, but got {value.Length}";
}

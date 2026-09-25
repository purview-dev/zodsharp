namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for minimum string length.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct MinLengthRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly int _minLength;

	/// <summary>
	/// Initializes a new instance of the MinLengthRule struct.
	/// </summary>
	/// <param name="minLength">The minimum length</param>
	public MinLengthRule(int minLength)
	{
		_minLength = minLength;
	}

	/// <summary>
	/// Validates that the value meets the minimum length requirement.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value.LengthOrDefault() >= _minLength;

	/// <summary>
	/// Validates that the span meets the minimum length requirement without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.Length >= _minLength;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		$"String must be at least {_minLength} characters long, but got {value.LengthOrDefault()}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		$"String must be at least {_minLength} characters long, but got {value.Length}";
}

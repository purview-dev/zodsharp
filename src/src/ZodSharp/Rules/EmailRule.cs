using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for email format.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct EmailRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	/// <summary>
	/// Regular expression for validating email format.
	/// </summary>
	public static readonly Regex EmailRegex = new(
		@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase,
		TimeSpan.FromMilliseconds(100)
	);

	/// <summary>
	/// Validates that the value matches the email format.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => EmailRegex.IsMatch(value);

	/// <summary>
	/// Validates that the span matches the email format without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => EmailRegex.IsMatch(value);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => $"Invalid email format: {value}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) => $"Invalid email format: {value}";
}

using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for regex pattern matching.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct RegexRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly Regex _pattern;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the RegexRule struct.
	/// </summary>
	/// <param name="pattern">The regex pattern</param>
	/// <param name="message">Optional error message</param>
	public RegexRule(Regex pattern, string? message = null)
	{
		_pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
		_message = message.OrNull();
	}

	/// <summary>
	/// Initializes a new instance of the RegexRule struct.
	/// </summary>
	/// <param name="pattern">The regex pattern</param>
	/// <param name="message">Optional error message</param>
	public RegexRule(string pattern, string? message = null)
		: this(new Regex(pattern, RegexOptions.Compiled), message) { }

	/// <summary>
	/// Validates that the value matches the regex pattern.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => _pattern.IsMatch(value);

	/// <summary>
	/// Validates that the span matches the regex pattern without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => _pattern.IsMatch(value);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		_message ?? $"String does not match the required pattern: {_pattern}";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) => GetErrorMessage(value.ToString());
}

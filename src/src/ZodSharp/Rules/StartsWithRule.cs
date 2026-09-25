namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for string prefix.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct StartsWithRule : Core.IValidationRule<string>, Core.IStringValidationRule
{
	readonly string _prefix;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the StartsWithRule struct.
	/// </summary>
	/// <param name="prefix">The required prefix</param>
	/// <param name="message">Optional error message</param>
	public StartsWithRule(string prefix, string? message = null)
	{
		_prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
		_message = message.OrNull();
	}

	/// <summary>
	/// Validates that the value starts with the specified prefix.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value != null && value.StartsWith(_prefix, StringComparison.Ordinal);

	/// <summary>
	/// Validates that the span starts with the specified prefix without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value) => value.StartsWith(_prefix.AsSpan(), StringComparison.Ordinal);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		_message ?? $"String must start with '{_prefix}', but got '{value}'";

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		_message ?? $"String must start with '{_prefix}', but got '{value}'";
}

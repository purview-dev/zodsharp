using System.Text.RegularExpressions;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for UUID format.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct UUIDRule : Core.IValidationRule<string>
{
	static readonly Regex UUIDRegex = new(
		@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
		RegexOptions.Compiled | RegexOptions.IgnoreCase,
		TimeSpan.FromMilliseconds(100)
	);

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the UuidRule struct.
	/// </summary>
	/// <param name="message">Optional error message</param>
	public UUIDRule(string? message = null)
	{
		_message = message.OrNull();
	}

	/// <summary>
	/// Validates that the value is a valid UUID.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => !string.IsNullOrWhiteSpace(value) && UUIDRegex.IsMatch(value);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => _message ?? $"Invalid UUID format: {value}";
}

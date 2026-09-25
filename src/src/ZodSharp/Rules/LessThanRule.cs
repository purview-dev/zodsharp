namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for an exclusive maximum numeric value (strictly less than).
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The numeric type</typeparam>
public readonly record struct LessThanRule<T> : Core.IValidationRule<T>
	where T : IComparable<T>
{
	readonly T _exclusiveMaximum;

	/// <summary>
	/// Initializes a new instance of the LessThanRule struct.
	/// </summary>
	/// <param name="exclusiveMaximum">The value the input must be strictly less than</param>
	public LessThanRule(T exclusiveMaximum)
	{
		_exclusiveMaximum = exclusiveMaximum;
	}

	/// <summary>
	/// Validates that the value is strictly less than the configured bound.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => value.CompareTo(_exclusiveMaximum) < 0;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) => $"Value must be less than {_exclusiveMaximum}, but got {value}";
}

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for multiple-of check.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct MultipleOfRule : Core.IValidationRule<double>
{
	/// <summary>
	/// The relative tolerance applied when comparing the quotient to its nearest integer.
	/// </summary>
	const double RelativeTolerance = 1e-12;

	readonly double _divisor;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the MultipleOfRule struct.
	/// </summary>
	/// <param name="divisor">The divisor</param>
	/// <param name="message">Optional error message</param>
	public MultipleOfRule(double divisor, string? message = null)
	{
		if (divisor == 0)
			throw new ArgumentException("Divisor cannot be zero", nameof(divisor));

		_divisor = divisor;
		_message = message.OrNull();
	}

	/// <summary>
	/// Validates that the value is a multiple of the divisor.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
			return false;

		// Floating-point division is inexact (for example 0.3 / 0.1 == 2.9999999999999996),
		// so compare the quotient against its nearest integer using a relative tolerance
		// instead of testing the raw remainder.
		var quotient = value / _divisor;
		var nearestInteger = Math.Round(quotient);
		var tolerance = RelativeTolerance * Math.Max(1.0, Math.Abs(quotient));
		return Math.Abs(quotient - nearestInteger) <= tolerance;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in double value) =>
		_message ?? $"Number must be a multiple of {_divisor}, but got {value}";
}

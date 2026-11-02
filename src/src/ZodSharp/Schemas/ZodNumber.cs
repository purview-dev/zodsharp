using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for number validation.
/// Provides fluent API for common number validations.
/// </summary>
public class ZodNumber : ZodType<double>
{
	/// <summary>
	/// Parses and validates a number value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<double> ParseInternal(double value) =>
		double.IsNaN(value)
			? ValidationResult<double>.Failure(
				ZodIssues.InvalidType(ZodTypeNames.Number, value, origin: ZodTypeNames.Number)
			)
			: ValidationResult<double>.Success(value);

	/// <summary>
	/// Adds a minimum value validation.
	/// </summary>
	/// <param name="minValue">The minimum value</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Min(double minValue, string? message = null, string? code = null)
	{
		AddRule(new MinValueRule<double>(minValue, message, code));
		return this;
	}

	/// <summary>
	/// Adds a maximum value validation.
	/// </summary>
	/// <param name="maxValue">The maximum value</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Max(double maxValue, string? message = null, string? code = null)
	{
		AddRule(new MaxValueRule<double>(maxValue, message, code));
		return this;
	}

	/// <summary>
	/// Adds a range validation requiring the value to fall between a minimum and maximum.
	/// </summary>
	/// <param name="minimum">The lower bound</param>
	/// <param name="maximum">The upper bound</param>
	/// <param name="minimumIsExclusive">Whether the lower bound is exclusive</param>
	/// <param name="maximumIsExclusive">Whether the upper bound is exclusive</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Range(
		double minimum,
		double maximum,
		bool minimumIsExclusive = false,
		bool maximumIsExclusive = false,
		string? message = null,
		string? code = null
	)
	{
		AddRule(new RangeRule<double>(minimum, maximum, minimumIsExclusive, maximumIsExclusive, message, code));
		return this;
	}

	/// <summary>
	/// Adds a strictly-greater-than validation.
	/// Equivalent to Zod's <c>z.number().gt(value)</c>.
	/// </summary>
	/// <param name="value">The exclusive lower bound</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Gt(double value, string? message = null, string? code = null)
	{
		AddRule(new GreaterThanRule<double>(value, message, code));
		return this;
	}

	/// <summary>
	/// Adds a greater-than-or-equal validation.
	/// Equivalent to Zod's <c>z.number().gte(value)</c>.
	/// </summary>
	/// <param name="value">The inclusive lower bound</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Gte(double value, string? message = null, string? code = null)
	{
		AddRule(new GreaterThanOrEqualRule<double>(value, message, code));
		return this;
	}

	/// <summary>
	/// Adds a strictly-less-than validation.
	/// Equivalent to Zod's <c>z.number().lt(value)</c>.
	/// </summary>
	/// <param name="value">The exclusive upper bound</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Lt(double value, string? message = null, string? code = null)
	{
		AddRule(new LessThanRule<double>(value, message, code));
		return this;
	}

	/// <summary>
	/// Adds a less-than-or-equal validation.
	/// Equivalent to Zod's <c>z.number().lte(value)</c>.
	/// </summary>
	/// <param name="value">The inclusive upper bound</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Lte(double value, string? message = null, string? code = null)
	{
		AddRule(new LessThanOrEqualRule<double>(value, message, code));
		return this;
	}

	/// <summary>
	/// Adds an integer validation (must be a whole number).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name")]
	public ZodNumber Int(string? message = null, string? code = null)
	{
		AddRule(new IntRule<double>(message, code));
		return this;
	}

	/// <summary>
	/// Adds a strictly positive number validation (value must be greater than zero).
	/// Equivalent to Zod's <c>z.number().positive()</c>.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Positive(string? message = null, string? code = null)
	{
		AddRule(new GreaterThanRule<double>(0.0, message, code));
		return this;
	}

	/// <summary>
	/// Adds a strictly negative number validation (value must be less than zero).
	/// Equivalent to Zod's <c>z.number().negative()</c>.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Negative(string? message = null, string? code = null)
	{
		AddRule(new LessThanRule<double>(0.0, message, code));
		return this;
	}

	/// <summary>
	/// Adds a non-negative number validation (value must be greater than or equal to zero).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber NonNegative(string? message = null, string? code = null)
	{
		AddRule(new MinValueRule<double>(0.0, message, code));
		return this;
	}

	/// <summary>
	/// Adds a non-positive number validation (value must be less than or equal to zero).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber NonPositive(string? message = null, string? code = null)
	{
		AddRule(new MaxValueRule<double>(0.0, message, code));
		return this;
	}

	/// <summary>
	/// Adds a multiple-of validation.
	/// </summary>
	/// <param name="divisor">The divisor</param>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber MultipleOf(double divisor, string? message = null, string? code = null)
	{
		AddRule(new MultipleOfRule<double>(divisor, message, code));
		return this;
	}

	/// <summary>
	/// Adds a finite number validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Finite(string? message = null, string? code = null)
	{
		AddRule(new FiniteRule<double>(message, code));
		return this;
	}

	/// <summary>
	/// Adds a safe integer validation (within int.MinValue and int.MaxValue).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Safe(string? message = null, string? code = null)
	{
		AddRule(new SafeIntegerRule(message, code));
		return this;
	}

	/// <summary>
	/// Adds an even-number validation (the value must be a multiple of two).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Even(string? message = null, string? code = null)
	{
		AddRule(new EvenRule<double>(message, code));
		return this;
	}

	/// <summary>
	/// Adds an odd-number validation (the value must not be a multiple of two).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <param name="code">Optional error code override</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Odd(string? message = null, string? code = null)
	{
		AddRule(new OddRule<double>(message, code));
		return this;
	}
}

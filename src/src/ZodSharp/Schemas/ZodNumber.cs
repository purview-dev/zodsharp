using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for number validation.
/// Provides fluent API for common number validations.
/// </summary>
public class ZodNumber : ZodType<double>
{
	static readonly string[] EmptyPath = [];

	/// <summary>
	/// Parses and validates a number value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<double> ParseInternal(double value) =>
		double.IsNaN(value)
			? ValidationResult<double>.Failure(
				new ValidationError("invalid_type", "Expected number, but got NaN", EmptyPath)
			)
			: ValidationResult<double>.Success(value);

	/// <summary>
	/// Adds a minimum value validation.
	/// </summary>
	/// <param name="minValue">The minimum value</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Min(double minValue)
	{
		AddRule(new MinValueRule<double>(minValue));
		return this;
	}

	/// <summary>
	/// Adds a maximum value validation.
	/// </summary>
	/// <param name="maxValue">The maximum value</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Max(double maxValue)
	{
		AddRule(new MaxValueRule<double>(maxValue));
		return this;
	}

	/// <summary>
	/// Adds an integer validation (must be a whole number).
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name")]
	public ZodNumber Int()
	{
		AddRule(new IntRule());
		return this;
	}

	/// <summary>
	/// Adds a strictly positive number validation (value must be greater than zero).
	/// Equivalent to Zod's <c>z.number().positive()</c>.
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Positive()
	{
		AddRule(new GreaterThanRule<double>(0.0));
		return this;
	}

	/// <summary>
	/// Adds a strictly negative number validation (value must be less than zero).
	/// Equivalent to Zod's <c>z.number().negative()</c>.
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Negative()
	{
		AddRule(new LessThanRule<double>(0.0));
		return this;
	}

	/// <summary>
	/// Adds a non-negative number validation (value must be greater than or equal to zero).
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber NonNegative()
	{
		AddRule(new MinValueRule<double>(0.0));
		return this;
	}

	/// <summary>
	/// Adds a non-positive number validation (value must be less than or equal to zero).
	/// </summary>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber NonPositive()
	{
		AddRule(new MaxValueRule<double>(0.0));
		return this;
	}

	/// <summary>
	/// Adds a multiple-of validation.
	/// </summary>
	/// <param name="divisor">The divisor</param>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber MultipleOf(double divisor, string? message = null)
	{
		AddRule(new MultipleOfRule(divisor, message));
		return this;
	}

	/// <summary>
	/// Adds a finite number validation.
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Finite(string? message = null)
	{
		AddRule(new FiniteRule(message));
		return this;
	}

	/// <summary>
	/// Adds a safe integer validation (within int.MinValue and int.MaxValue).
	/// </summary>
	/// <param name="message">Optional error message</param>
	/// <returns>This schema for method chaining</returns>
	public ZodNumber Safe(string? message = null)
	{
		AddRule(new SafeIntegerRule(message));
		return this;
	}
}

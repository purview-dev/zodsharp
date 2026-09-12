using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that requires a value to pass both of two schemas (intersection).
/// Equivalent to Zod's <c>z.intersection(a, b)</c> / <c>.and(other)</c>.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodIntersection{T}"/> class.
/// </remarks>
/// <param name="left">The first schema.</param>
/// <param name="right">The second schema.</param>
public class ZodIntersection<T>(IZodSchema<T, T> left, IZodSchema<T, T> right) : ZodType<T>
{
	/// <summary>
	/// Validates the value against both schemas, merging errors from both.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<T> ParseInternal(T value)
	{
		var leftResult = left.Validate(value);
		var rightResult = right.Validate(value);

		if (leftResult.IsSuccess && rightResult.IsSuccess)
			return ValidationResult<T>.Success(value);

		List<ValidationError> allErrors = [.. leftResult.Errors, .. rightResult.Errors];
		return ValidationResult<T>.Failure(allErrors);
	}
}

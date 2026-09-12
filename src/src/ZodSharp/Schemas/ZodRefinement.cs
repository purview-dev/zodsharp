using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that adds custom validation logic (refinement).
/// Equivalent to Zod's refine method.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
/// <remarks>
/// Initializes a new instance of the ZodRefinement class.
/// </remarks>
/// <param name="baseSchema">The base schema</param>
/// <param name="refinement">The refinement function</param>
/// <param name="message">Optional error message</param>
public class ZodRefinement<T>(IZodSchema<T> baseSchema, Func<T, bool> refinement, string? message = null) : ZodType<T>
{
	/// <summary>
	/// Parses and validates the value with refinement.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<T> ParseInternal(T value)
	{
		var baseResult = baseSchema.Validate(value);
		if (!baseResult.IsSuccess)
			return baseResult;

		// On success...
		return refinement(baseResult.Value)
			? ValidationResult<T>.Success(baseResult.Value)
			: ValidationResult<T>.Failure(
				new ValidationError("refinement_failed", message ?? "Custom validation failed", [])
			);
	}
}

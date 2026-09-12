using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that transforms a value during validation.
/// Equivalent to Zod's transform method.
/// </summary>
/// <typeparam name="TInput">The input type (after validation by inner schema)</typeparam>
/// <typeparam name="TOutput">The output type after transformation</typeparam>
/// <remarks>
/// Initializes a new instance of the ZodTransform class.
/// </remarks>
/// <param name="inputSchema">The input schema</param>
/// <param name="transform">The transformation function</param>
public class ZodTransform<TInput, TOutput>(IZodSchema<TInput, TInput> inputSchema, Func<TInput, TOutput> transform)
	: ZodType<TOutput, TInput>
{
	/// <summary>
	/// Parses and transforms the input value.
	/// </summary>
	/// <param name="value">The value to validate and transform</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<TOutput> ParseInternal(TInput value)
	{
		var validationResult = inputSchema.Validate(value);
		if (!validationResult.IsSuccess)
			return ValidationResult<TOutput>.Failure(validationResult.Errors);

		try
		{
			var transformedValue = transform(validationResult.Value);
			return ValidationResult<TOutput>.Success(transformedValue);
		}
		catch (Exception ex)
		{
			return ValidationResult<TOutput>.Failure(
				new ValidationError("transform_error", $"Transform failed: {ex.Message}", [])
			);
		}
	}
}

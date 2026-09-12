using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that pipes the output of an inner schema through a second schema.
/// Equivalent to Zod's <c>.pipe(target)</c> method and the foundation of
/// validation pipelines: the source schema validates/coerces the input, then
/// the target schema validates the resulting value.
/// </summary>
/// <typeparam name="TSourceOutput">The output type of the source schema.</typeparam>
/// <typeparam name="TTargetOutput">The output type of the target schema.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodPipe{TSourceOutput,TTargetOutput}"/> class.
/// </remarks>
/// <param name="source">The schema that validates the raw input.</param>
/// <param name="target">The schema that validates the source's output.</param>
public class ZodPipe<TSourceOutput, TTargetOutput>(
	IZodSchema<TSourceOutput, TSourceOutput> source,
	IZodSchema<TTargetOutput, TSourceOutput> target
) : ZodType<TTargetOutput, TSourceOutput>
{
	/// <summary>
	/// Validates the input with the source schema, then pipes the result
	/// through the target schema.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<TTargetOutput> ParseInternal(TSourceOutput value)
	{
		var sourceResult = source.Validate(value);
		return sourceResult.IsSuccess
			? target.Validate(sourceResult.Value)
			: ValidationResult<TTargetOutput>.Failure(sourceResult.Errors);
	}
}

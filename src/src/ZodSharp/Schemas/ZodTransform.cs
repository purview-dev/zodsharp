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
		catch (Exception ex) when (IsTransformFailure(ex))
		{
			// The exception message is deliberately NOT part of the validation error. A transform is
			// arbitrary caller code — it may reach a database or a service — and a validation error flows
			// into ProblemDetails and out to the HTTP client, so putting the message there discloses
			// internal detail to whoever made the request. Only the fixed code and message are reported.
			_ = ex;

			return ValidationResult<TOutput>.Failure(
				ValidationError.Create(
					ZodIssueCodes.TransformError,
					ZodMessages.TransformFailed,
					ZodPath.Root,
					category: ZodIssueCategories.Custom
				)
			);
		}
	}

	/// <summary>
	/// Whether <paramref name="exception"/> is a transform failure rather than something that must not be
	/// turned into a validation error.
	/// </summary>
	/// <remarks>
	/// The previous blanket <c>catch (Exception)</c> also swallowed cancellation — breaking a caller's
	/// <see cref="OperationCanceledException"/> contract — and conditions the process cannot continue
	/// through, such as <see cref="OutOfMemoryException"/>. Those propagate.
	/// </remarks>
	static bool IsTransformFailure(Exception exception) =>
		exception is not (OperationCanceledException or OutOfMemoryException or StackOverflowException);
}

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

		// Both sides often report the same issue (a shared base schema); report it once.
		List<ValidationError> allErrors = [.. leftResult.Errors];
		foreach (var error in rightResult.Errors)
		{
			if (!allErrors.Exists(existing => IsSameIssue(existing, error)))
				allErrors.Add(error);
		}

		return ValidationResult<T>.Failure(allErrors);
	}

	static bool IsSameIssue(ValidationError left, ValidationError right) =>
		string.Equals(left.Code, right.Code, StringComparison.Ordinal)
		&& left.FullPath == right.FullPath
		&& string.Equals(left.Message, right.Message, StringComparison.Ordinal);
}

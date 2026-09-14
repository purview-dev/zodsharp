using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for union types (one of multiple schemas).
/// </summary>
/// <remarks>
/// Initializes a new instance of the ZodUnion class.
/// </remarks>
/// <param name="options">The union options</param>
public class ZodUnion(IReadOnlyList<IZodSchema<object, object>> options) : ZodType<object, object>
{
	/// <summary>
	/// Parses and validates the value against union options.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<object> ParseInternal(object value)
	{
		List<ValidationError>? allErrors = null;

		foreach (var option in options)
		{
			var result = option.Validate(value);
			if (result.IsSuccess)
			{
				return result;
			}

			allErrors ??= [];
			allErrors.AddRange(result.Errors);
		}

		return ValidationResult<object>.Failure(
			new ValidationError(
				"invalid_union",
				"Value does not match any of the union options",
				[],
				new Dictionary<string, object?> { { "errors", allErrors ?? [] } }
			)
		);
	}
}

using System.Collections.Immutable;
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
	// Exposed to the JSON Schema exporter, which previously reflected a field named "_options" and cast it
	// to an array. Neither matched: the options are a primary-constructor parameter typed IReadOnlyList, so
	// the lookup always failed and every union exported with an empty "anyOf".
	internal IReadOnlyList<IZodSchema<object, object>> Options => options;

	/// <summary>
	/// Parses and validates the value against union options.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<object> ParseInternal(object value)
	{
		var unionErrors = ImmutableArray.CreateBuilder<ImmutableArray<ValidationError>>(options.Count);

		foreach (var option in options)
		{
			var result = option.Validate(value);
			if (result.IsSuccess)
			{
				return result;
			}

			// Each option's issues stay grouped, in declaration order, with paths relative to the union.
			unionErrors.Add(result.Errors.IsDefault ? [] : result.Errors);
		}

		return ValidationResult<object>.Failure(ZodIssues.InvalidUnion(unionErrors.MoveToImmutable()));
	}
}

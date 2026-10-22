using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for record (dictionary) validation. Validates that all values in a
/// dictionary match the value schema. Equivalent to Zod's
/// <c>z.record(valueSchema)</c>.
/// </summary>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodRecord{TValue}"/> class.
/// </remarks>
/// <param name="valueSchema">The schema for all values.</param>
public class ZodRecord<TValue>(IZodSchema<TValue, TValue> valueSchema)
	: ZodType<Dictionary<string, TValue>, Dictionary<string, TValue>>,
		JsonSchema.IJsonSchemaRecordInfo
{
	static readonly string[] EmptyPath = [];

	// IJsonSchemaRecordInfo: the JSON Schema exporter's non-generic seam. A record exports as an object whose
	// additionalProperties is the value schema. See IJsonSchemaRecordInfo.
	object JsonSchema.IJsonSchemaRecordInfo.ValueSchema => valueSchema;

	/// <summary>
	/// Validates that all values in the dictionary match the value schema.
	/// </summary>
	/// <param name="value">The dictionary to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<Dictionary<string, TValue>> ParseInternal(Dictionary<string, TValue> value)
	{
		if (value == null)
		{
			return ValidationResult<Dictionary<string, TValue>>.Failure(
				new ValidationError("invalid_type", "Expected record, but got null", EmptyPath)
			);
		}

		List<ValidationError> errors = [];
		Dictionary<string, TValue> validated = [];

		foreach (var (key, itemValue) in value)
		{
			// A null value is the value schema's decision, not the record's: a record of a non-nullable schema
			// rejects it (matching z.record(z.string(), T)), while a nullable value schema accepts it.
			var result = valueSchema.Validate(itemValue);
			if (!result.IsSuccess)
			{
				foreach (var error in result.Errors)
				{
					var path = new string[error.Path.Length + 1];
					path[0] = key;
					error.Path.CopyTo(0, path, 1, error.Path.Length);
					errors.Add(
						ValidationError.Create(
							error.Code,
							error.Message,
							ImmutableArray.Create(path),
							error.Parameters,
							error.Origin,
							error.Minimum,
							error.Maximum,
							error.Inclusive,
							error.Category
						)
					);
				}
			}
			else
			{
				validated[key] = result.Value;
			}
		}

		return errors.Count > 0
			? ValidationResult<Dictionary<string, TValue>>.Failure(errors)
			: ValidationResult<Dictionary<string, TValue>>.Success(validated);
	}
}

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
			return ValidationResult<Dictionary<string, TValue>>.Failure(ZodIssues.NullValue(ZodTypeNames.Record));
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
				var segment = ZodPathSegment.Key(key);
				foreach (var error in result.Errors)
					errors.Add(error.WithPathPrefix(segment));
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

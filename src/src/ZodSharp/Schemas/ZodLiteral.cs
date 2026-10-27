using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for literal value validation.
/// </summary>
/// <typeparam name="T">The literal type</typeparam>
/// <remarks>
/// Initializes a new instance of the ZodLiteral class.
/// </remarks>
/// <param name="value">The literal value</param>
public class ZodLiteral<T>(T value) : ZodType<T, T>, JsonSchema.IJsonSchemaLiteralInfo
	where T : IEquatable<T>
{
	readonly T _value = value;

	// Read by the JSON Schema exporter, which previously matched this type by name and read the value by
	// private field name. See IJsonSchemaLiteralInfo.
	object? JsonSchema.IJsonSchemaLiteralInfo.LiteralValue => _value;

	/// <summary>
	/// Parses and validates the value against the literal.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<T> ParseInternal(T value)
	{
		if (value is null)
		{
			return ValidationResult<T>.Failure(LiteralError(ZodTypeNames.Null));
		}

		// Check if the value is equal to the literal value
		return value.Equals(_value)
			? ValidationResult<T>.Success(value)
			: ValidationResult<T>.Failure(LiteralError(ZodTypeNames.OfValue(value)));
	}

	// The expected literal is schema configuration, so it is safe to report; the received value is caller
	// input, so only its type name is.
	ValidationError LiteralError(string receivedType) =>
		ZodIssues.Formatted(
			ZodIssueCodes.InvalidLiteral,
			ZodIssueCategories.InvalidValue,
			ZodMessages.InvalidLiteral,
			ZodPath.Root,
			null,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[ZodIssueParameters.Expected] = _value,
				[ZodIssueParameters.Received] = receivedType,
			}
		);
}

using System.Diagnostics.CodeAnalysis;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for string enum validation. Validates that a string is one of the
/// allowed values. Equivalent to Zod's <c>z.enum(["a", "b"])</c>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodEnum"/> class.
/// </remarks>
/// <param name="allowedValues">The allowed string values.</param>
[SuppressMessage("Naming", "CA1711", Justification = "Name mirrors Zod's z.enum API.")]
public class ZodEnum(HashSet<string> allowedValues) : ZodType<string>, JsonSchema.IJsonSchemaEnumInfo
{
	static readonly string[] EmptyPath = [];

	/// <summary>
	/// Gets the set of allowed values.
	/// </summary>
	public new IReadOnlyCollection<string> AllowedValues => allowedValues;

	IReadOnlyList<object?> JsonSchema.IJsonSchemaEnumInfo.EnumValues => [.. allowedValues.Cast<object?>()];

	/// <summary>
	/// Validates that the string is one of the allowed values.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<string> ParseInternal(string value)
	{
		if (value == null)
		{
			return ValidationResult<string>.Failure(
				new ValidationError("invalid_type", "Expected string, but got null", EmptyPath)
			);
		}

		// Check if the value is in the allowed values set
		return allowedValues.Contains(value)
			? ValidationResult<string>.Success(value)
			: ValidationResult<string>.Failure(
				new ValidationError(
					"invalid_enum_value",
					$"Expected one of: {string.Join(", ", allowedValues)}, but got '{value}'",
					EmptyPath
				)
			);
	}
}

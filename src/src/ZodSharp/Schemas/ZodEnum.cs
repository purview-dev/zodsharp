using System.Collections.Immutable;
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
	// Ordinal order: the set's own enumeration order follows string hash codes, which differ per process.
	readonly ImmutableArray<string> _options = [.. allowedValues.Order(StringComparer.Ordinal)];

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
			return ValidationResult<string>.Failure(ZodIssues.NullValue(ZodTypeNames.String));
		}

		// Check if the value is in the allowed values set
		return allowedValues.Contains(value)
			? ValidationResult<string>.Success(value)
			: ValidationResult<string>.Failure(
				ZodIssues.Formatted(
					ZodIssueCodes.InvalidEnumValue,
					ZodIssueCategories.InvalidValue,
					ZodMessages.InvalidEnumValue,
					ZodPath.Root,
					ZodTypeNames.Enum,
					new Dictionary<string, object?>(StringComparer.Ordinal) { [ZodIssueParameters.Options] = _options }
				)
			);
	}
}

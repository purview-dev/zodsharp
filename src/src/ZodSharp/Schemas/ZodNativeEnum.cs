using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for native C# enum validation. Validates that a value is a defined
/// member of <typeparamref name="TEnum"/>. Equivalent to Zod's
/// <c>z.nativeEnum(Enum)</c>.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodNativeEnum{TEnum}"/> class.
/// </remarks>
[SuppressMessage("Naming", "CA1711", Justification = "Name mirrors Zod's z.nativeEnum API.")]
public class ZodNativeEnum<TEnum>() : ZodType<TEnum>, JsonSchema.IJsonSchemaEnumInfo
	where TEnum : struct, Enum
{
	// The schema's own membership check reports exactly what the [Enum] rule reports.
	static readonly Rules.EnumRule<TEnum> MembershipRule = new(message: null);

	// Resolving the defined members once per closed generic type avoids the per-validation
	// Enum.IsDefined reflection path.
	static readonly HashSet<TEnum> DefinedValues = [.. System.Enum.GetValues<TEnum>()];

	// The JSON Schema wire form of each member. System.Text.Json serialises a native enum as its numeric
	// value by default, so the exported enum lists the underlying numbers rather than the member names.
	static readonly List<object?> JsonEnumValues =
	[
		.. System
			.Enum.GetValues<TEnum>()
			.Select(static value =>
				(object?)
					Convert.ChangeType(
						value,
						System.Enum.GetUnderlyingType(typeof(TEnum)),
						CultureInfo.InvariantCulture
					)
			),
	];

	IReadOnlyList<object?> JsonSchema.IJsonSchemaEnumInfo.EnumValues => JsonEnumValues;

	/// <summary>
	/// Validates that the value is a defined enum member.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<TEnum> ParseInternal(TEnum value) =>
		DefinedValues.Contains(value)
			? ValidationResult<TEnum>.Success(value)
			: ValidationResult<TEnum>.Failure(ZodRuleErrors.Create(MembershipRule, value, ZodPath.Root));

	/// <summary>
	/// Adds an <see cref="Rules.EnumRule{TEnum}"/> that validates the value is a defined member of
	/// <typeparamref name="TEnum"/>, rejecting any member the rule excludes.
	/// </summary>
	/// <param name="message">Optional error message.</param>
	/// <param name="code">Optional error code override.</param>
	/// <returns>This schema for method chaining.</returns>
	/// <remarks>
	/// The schema already rejects undefined members; this method mirrors the <c>[Enum]</c> attribute the
	/// source generator emits for enum properties and lets the error message and code be customised.
	/// </remarks>
	public ZodNativeEnum<TEnum> Enum(string? message = null, string? code = null)
	{
		AddRule(new Rules.EnumRule<TEnum>(message, code));
		return this;
	}
}

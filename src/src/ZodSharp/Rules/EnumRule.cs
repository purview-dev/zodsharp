using System.Globalization;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that rejects values that are not a defined member of the enum type, or that resolve to a
/// member excluded from the allowed set. Equivalent to Zod's <c>z.nativeEnum(Enum)</c> semantics.
/// </summary>
/// <typeparam name="TEnum">The enum type the rule validates.</typeparam>
/// <remarks>
/// <para>
/// The source generator emits this rule automatically for every non-flags enum property of a
/// <c>[ZodSchema]</c> type, supplying the members excluded from the allowed set: enum members marked with
/// <see cref="ZodIgnoreAttribute"/> and the values a property's <c>[DeniedValues]</c> attribute lists. Apply
/// <c>[ZodSchema(ValidateEnumValues = false)]</c> to opt out.
/// </para>
/// <para>
/// The defined members are resolved once per closed generic type, so the validation path performs no
/// reflection.
/// </para>
/// </remarks>
[Core.ZodRule]
public readonly record struct EnumRule<TEnum> : Core.IValidationRule<TEnum>, Core.IZodRule, JsonSchema.IJsonSchemaRule
	where TEnum : struct, Enum
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_enum_value";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value and <c>{1}</c> the enum name.</summary>
	public const string MessageFormat = "'{0}' is not a defined member of {1}";

	// Resolving the defined members once per closed generic type avoids the per-validation
	// Enum.IsDefined reflection path.
	static readonly HashSet<TEnum> DefinedValues = [.. Enum.GetValues<TEnum>()];

	static readonly System.Collections.Immutable.ImmutableArray<string> DefinedNames = [.. Enum.GetNames<TEnum>()];

	readonly TEnum[]? _disallowed;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumRule{TEnum}"/> struct that accepts every defined
	/// member of <typeparamref name="TEnum"/>.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public EnumRule(string? message = null, string? code = null)
		: this(disallowed: null, message, code) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="EnumRule{TEnum}"/> struct that accepts every defined
	/// member of <typeparamref name="TEnum"/> except the disallowed values.
	/// </summary>
	/// <param name="disallowed">The defined members the rule rejects; <see langword="null"/> or empty accepts every member.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public EnumRule(TEnum[]? disallowed, string? message = null, string? code = null)
	{
		_disallowed = disallowed is { Length: > 0 } ? disallowed : null;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a defined member of the enum type and is not disallowed.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in TEnum value) =>
		DefinedValues.Contains(value) && (_disallowed is null || Array.IndexOf(_disallowed, value) < 0);

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in TEnum value) =>
		RuleMessage.Format(_message ?? MessageFormat, value, typeof(TEnum).Name);

	/// <summary>
	/// Applies the JSON Schema <c>enum</c> constraint listing the defined members that are not disallowed,
	/// in their numeric wire form (System.Text.Json's default enum representation).
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);

		// A lambda inside a struct cannot capture instance members, so copy the field to a local first.
		var disallowed = _disallowed;
		schema.Enum =
		[
			.. Enum.GetValues<TEnum>()
				.Where(value => disallowed is null || Array.IndexOf(disallowed, value) < 0)
				.Select(static value =>
					Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TEnum)), CultureInfo.InvariantCulture)
				),
		];
	}

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Enum;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidValue;

	// The allowed member names, in declaration order (enum members are configuration, not caller input).
	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters
	{
		get
		{
			var disallowed = _disallowed;
			return RuleParameters.One(
				Core.ZodIssueParameters.Options,
				disallowed is null
					? DefinedNames
					:
					[
						.. Enum.GetValues<TEnum>()
							.Where(value => Array.IndexOf(disallowed, value) < 0)
							.Select(static value => value.ToString()),
					]
			);
		}
	}
}

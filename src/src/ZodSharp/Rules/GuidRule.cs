namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for GUID format (any 8-4-4-4-12 hexadecimal identifier), equivalent to Zod's
/// <c>z.string().guid()</c>. Unlike <see cref="UUIDRule"/> it accepts every version nibble.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct GuidRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Invalid GUID: {0}";

	/// <summary>
	/// The JSON Schema <c>pattern</c> for a canonical 8-4-4-4-12 hexadecimal identifier (any version or
	/// variant nibble). The source generator emits this for a <see cref="System.Guid"/>, so the exported
	/// constraint matches <see cref="IsValid(ReadOnlySpan{char})"/> rather than Zod's RFC UUID validator.
	/// </summary>
	public const string JsonSchemaPattern =
		"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="GuidRule"/> struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public GuidRule(string? message = null, string? code = null)
	{
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an 8-4-4-4-12 hexadecimal identifier.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is an 8-4-4-4-12 hexadecimal identifier without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.Length != 36)
			return false;

		for (var index = 0; index < value.Length; index++)
		{
			if (index is 8 or 13 or 18 or 23)
			{
				if (value[index] != '-')
					return false;

				continue;
			}

			if (!IsHex(value[index]))
				return false;
		}

		return true;
	}

	static bool IsHex(char character) => character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, value.ToString());

	/// <summary>Applies a JSON Schema pattern for an 8-4-4-4-12 hexadecimal identifier.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		JsonSchema.JsonSchemaRules.ConstrainPattern(schema, JsonSchemaPattern);
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for UUID format.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct UUIDRule
	: Core.IValidationRule<string>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the message format; <c>{0}</c> is the offending value.</summary>
	public const string MessageFormat = "Invalid UUID format: {0}";

	/// <summary>Gets the version-specific message format; <c>{0}</c> is the version and <c>{1}</c> the value.</summary>
	public const string VersionedMessageFormat = "Invalid UUID v{0} format: {1}";

	const string NilUuid = "00000000-0000-0000-0000-000000000000";
	const string MaxUuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

	readonly UuidVersion? _version;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the UuidRule struct with Zod-parity semantics
	/// (version 1-8, variant 8-9/a-b, plus the nil and max UUIDs).
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public UUIDRule(string? message = null, string? code = null)
	{
		_version = null;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Initializes a new instance of the UuidRule struct that requires a specific RFC 9562 version.
	/// </summary>
	/// <param name="version">The required UUID version</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public UUIDRule(UuidVersion version, string? message = null, string? code = null)
	{
		if (version == UuidVersion.None)
			throw new ArgumentOutOfRangeException(nameof(version), version, "UUID version must be between V1 and V8.");

		_version = version;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is a valid UUID.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is a valid UUID without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsWhiteSpace() || value.Length != 36)
			return false;

		if (!HasValidStructure(value))
			return false;

		if (_version is UuidVersion version)
			return value[14] == (char)('0' + (int)version) && IsValidVariant(value[19]);

		// No specific version required; just check that the version and variant are valid.
		return IsValidVersionless(value);
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => GetErrorMessageCore(value);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) => GetErrorMessageCore(value.ToString());

	string GetErrorMessageCore(string value)
	{
		if (_message is not null)
			return RuleMessage.Format(_message, value, _version);

		// If a specific version is required, include it in the message; otherwise, use the generic message.
		return _version is UuidVersion version
			? RuleMessage.Format(VersionedMessageFormat, (int)version, value)
			: RuleMessage.Format(MessageFormat, value);
	}

	static bool IsValidVersionless(ReadOnlySpan<char> value)
	{
		// nil and max are allowed regardless of version/variant (Zod parity).
		if (value.SequenceEqual(NilUuid))
			return true;
		if (value.SequenceEqual(MaxUuid))
			return true;

		var version = value[14];
		if (version is < '1' or > '8')
			return false;

		// Check that the variant is valid (8, 9, a, b).
		return IsValidVariant(value[19]);
	}

	static bool HasValidStructure(ReadOnlySpan<char> value)
	{
		if (value[8] != '-' || value[13] != '-' || value[18] != '-' || value[23] != '-')
			return false;

		for (var i = 0; i < 36; i++)
		{
			if (i is 8 or 13 or 18 or 23)
				continue;

			if (!char.IsAsciiHexDigit(value[i]))
				return false;
		}

		return true;
	}

	static bool IsValidVariant(char c) => c is '8' or '9' or 'a' or 'b' or 'A' or 'B';

	/// <summary>Applies the JSON Schema <c>uuid</c> string format.</summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.Format = "uuid";
	}

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}

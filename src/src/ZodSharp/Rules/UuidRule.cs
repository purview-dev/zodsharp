namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for UUID format.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct UUIDRule
	: Core.IValidationRule<string>,
		Core.IValidationRule<Guid>,
		Core.IStringValidationRule,
		Core.IZodRule,
		JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>
	/// Gets the default message format. It never contains the input; a custom message may use <c>{0}</c> for the
	/// offending value and <c>{1}</c> for the required version (empty when no version is required).
	/// </summary>
	public const string MessageFormat = "Invalid UUID format";

	/// <summary>
	/// Gets the version-specific default message format; <c>{1}</c> is the required version as an integer. The
	/// arguments are the same as for <see cref="MessageFormat"/>: <c>{0}</c> is the offending value.
	/// </summary>
	public const string VersionedMessageFormat = "Invalid UUID v{1} format";

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
	/// Validates that the <see cref="Guid"/>'s bytes form a UUID with the required version and a valid RFC
	/// variant. This lets the same rule (and the generated <c>[UUID]</c> attribute) validate a
	/// <c>[Scalar&lt;Guid&gt;]</c> value object or a plain <see cref="Guid"/> member, not just a string.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	public bool IsValid(in Guid value)
	{
		Span<byte> bytes = stackalloc byte[16];
		value.TryWriteBytes(bytes, bigEndian: true, out _);

		if (_version is UuidVersion version)
			return (bytes[6] >> 4) == (int)version && IsValidVariantNibble(bytes[8]);

		// nil and max are allowed regardless of version/variant (Zod parity).
		if (IsNilOrMax(bytes))
			return true;

		var versionNibble = bytes[6] >> 4;
		return versionNibble is >= 1 and <= 8 && IsValidVariantNibble(bytes[8]);
	}

	static bool IsValidVariantNibble(byte b) => (b >> 4) is 8 or 9 or 10 or 11;

	static bool IsNilOrMax(ReadOnlySpan<byte> bytes)
	{
		var first = bytes[0];
		if (first is not (0x00 or 0xFF))
			return false;

		foreach (var b in bytes)
		{
			if (b != first)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in Guid value) => GetErrorMessageCore(value.ToString("D"));

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
		// Custom and default messages receive the same arguments: the value, then the version as an integer.
		var format = _message ?? (_version is null ? MessageFormat : VersionedMessageFormat);
		return RuleMessage.Format(format, value, _version is UuidVersion version ? (int)version : (int?)null);
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

	/// <summary>
	/// Applies the JSON Schema <c>uuid</c> string format, and a version-pinning <c>pattern</c> when the rule
	/// requires a specific RFC 9562 version.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	/// <remarks>
	/// JSON Schema has no standard keyword for the RFC version nibble, and a consumer maps
	/// <c>format: "uuid"</c> to a versionless RFC validator (for example Zod's <c>fromJSONSchema</c>). The
	/// extra pattern carries the version and variant, so a versioned rule is not silently widened on export.
	/// </remarks>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		schema.Format = "uuid";

		if (_version is UuidVersion version)
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, JsonSchemaVersionPattern(version));
	}

	/// <summary>
	/// Builds the JSON Schema <c>pattern</c> that pins an RFC 9562 version and the RFC variant nibble.
	/// </summary>
	/// <param name="version">The required version (V1-V8).</param>
	/// <returns>An anchored pattern for a canonical UUID of the requested version.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is <see cref="UuidVersion.None"/>.</exception>
	public static string JsonSchemaVersionPattern(UuidVersion version) =>
		version == UuidVersion.None
			? throw new ArgumentOutOfRangeException(nameof(version), version, "UUID version must be between V1 and V8.")
			: $"^[0-9a-fA-F]{{8}}-[0-9a-fA-F]{{4}}-{(int)version}[0-9a-fA-F]{{3}}-[89abAB][0-9a-fA-F]{{3}}-[0-9a-fA-F]{{12}}$";

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters =>
		_version is UuidVersion version
			? RuleParameters.Two(Core.ZodIssueParameters.Format, "uuid", Core.ZodIssueParameters.Version, (int)version)
			: RuleParameters.Format("uuid");
}

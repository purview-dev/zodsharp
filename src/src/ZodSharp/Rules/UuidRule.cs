namespace ZodSharp.Rules;

/// <summary>
/// Validation rule for UUID format.
/// Uses struct to avoid allocations.
/// </summary>
public readonly record struct UUIDRule : Core.IValidationRule<string>
{
	readonly string? _message;
	readonly UuidVersion? _version;

	/// <summary>
	/// Initializes a new instance of the UuidRule struct with Zod-parity semantics
	/// (version 1-8, variant 8-9/a-b, plus the nil and max UUIDs).
	/// </summary>
	/// <param name="message">Optional error message</param>
	public UUIDRule(string? message = null)
	{
		_version = null;
		_message = message.OrNull();
	}

	/// <summary>
	/// Initializes a new instance of the UuidRule struct that requires a specific RFC 9562 version.
	/// </summary>
	/// <param name="version">The required UUID version</param>
	/// <param name="message">Optional error message</param>
	public UUIDRule(UuidVersion version, string? message = null)
	{
		if (version == UuidVersion.None)
			throw new ArgumentOutOfRangeException(nameof(version), version, "UUID version must be between V1 and V8.");

		_version = version;
		_message = message.OrNull();
	}

	/// <summary>
	/// Validates that the value is a valid UUID.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length != 36)
			return false;

		if (!HasValidStructure(value))
			return false;

		if (_version is UuidVersion version)
			return value[14] == (char)('0' + (int)version) && IsValidVariant(value[19]);

		return IsValidVersionless(value);
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) =>
		_message
		?? (
			_version is UuidVersion version
				? $"Invalid UUID v{(int)version} format: {value}"
				: $"Invalid UUID format: {value}"
		);

	static bool IsValidVersionless(string value)
	{
		// nil and max are allowed regardless of version/variant (Zod parity).
		if (value == "00000000-0000-0000-0000-000000000000")
			return true;
		if (value == "ffffffff-ffff-ffff-ffff-ffffffffffff")
			return true;

		var version = value[14];
		if (version is < '1' or > '8')
			return false;

		return IsValidVariant(value[19]);
	}

	static bool HasValidStructure(string value)
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
}

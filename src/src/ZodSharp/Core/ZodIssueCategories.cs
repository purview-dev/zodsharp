namespace ZodSharp.Core;

/// <summary>
/// The Zod (v4) issue codes, used as the <see cref="ValidationError.Category"/> of every built-in error so a
/// client can handle ZodSharp and Zod issues the same way, while <see cref="ValidationError.Code"/> keeps
/// ZodSharp's more specific identity.
/// </summary>
public static class ZodIssueCategories
{
	/// <summary>The value has the wrong type (including a missing or null value).</summary>
	public const string InvalidType = "invalid_type";

	/// <summary>The value is below a size or range bound.</summary>
	public const string TooSmall = "too_small";

	/// <summary>The value is above a size or range bound.</summary>
	public const string TooBig = "too_big";

	/// <summary>A string does not match a format (email, uuid, regex, prefix, …).</summary>
	public const string InvalidFormat = "invalid_format";

	/// <summary>A number is not a multiple of the required step.</summary>
	public const string NotMultipleOf = "not_multiple_of";

	/// <summary>An object contains keys its schema does not declare.</summary>
	public const string UnrecognizedKeys = "unrecognized_keys";

	/// <summary>No option of a union matched.</summary>
	public const string InvalidUnion = "invalid_union";

	/// <summary>A record key is invalid.</summary>
	public const string InvalidKey = "invalid_key";

	/// <summary>A map or set element is invalid.</summary>
	public const string InvalidElement = "invalid_element";

	/// <summary>The value is not one of the allowed values (enums, literals, allowed/denied lists).</summary>
	public const string InvalidValue = "invalid_value";

	/// <summary>A refinement or custom rule failed.</summary>
	public const string Custom = "custom";
}

/// <summary>
/// The well-known <see cref="ValidationError.Parameters"/> names used by built-in errors.
/// </summary>
public static class ZodIssueParameters
{
	/// <summary>
	/// The per-option issues of a failed union, as
	/// <c>ImmutableArray&lt;ImmutableArray&lt;ValidationError&gt;&gt;</c>, with paths relative to the union.
	/// </summary>
	public const string UnionErrors = "unionErrors";

	/// <summary>The allowed values or union options.</summary>
	public const string Options = "options";

	/// <summary>The received value or, by default, only its type name.</summary>
	public const string Received = "received";

	/// <summary>The expected type name.</summary>
	public const string Expected = "expected";

	/// <summary>The unrecognized keys of an object.</summary>
	public const string Keys = "keys";

	/// <summary>The discriminator property of a discriminated union.</summary>
	public const string Discriminator = "discriminator";

	/// <summary>The display name of the member that failed.</summary>
	public const string DisplayName = "displayName";

	/// <summary>The full name of the type that declares the member that failed.</summary>
	public const string DeclaringType = "declaringType";

	/// <summary>The member name that failed.</summary>
	public const string Member = "member";

	/// <summary>The count (length, number of elements) that failed a size bound.</summary>
	public const string Count = "count";
}

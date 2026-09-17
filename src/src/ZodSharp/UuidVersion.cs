namespace ZodSharp;

/// <summary>
/// RFC 9562 UUID versions supported by <see cref="Schemas.ZodString.UUID(UuidVersion, string?)"/>.
/// </summary>
public enum UuidVersion
{
	/// <summary>No version (only valid for the versionless <c>UUID()</c> overload).</summary>
	None = 0,

	/// <summary>Version 1 — time-based UUID.</summary>
	V1 = 1,

	/// <summary>Version 2 — DCE security UUID.</summary>
	V2 = 2,

	/// <summary>Version 3 — name-based (MD5) UUID.</summary>
	V3 = 3,

	/// <summary>Version 4 — random UUID.</summary>
	V4 = 4,

	/// <summary>Version 5 — name-based (SHA-1) UUID.</summary>
	V5 = 5,

	/// <summary>Version 6 — reordered time-based UUID.</summary>
	V6 = 6,

	/// <summary>Version 7 — Unix time-based UUID.</summary>
	V7 = 7,

	/// <summary>Version 8 — custom UUID.</summary>
	V8 = 8,
}

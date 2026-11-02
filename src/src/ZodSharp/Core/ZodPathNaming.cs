namespace ZodSharp.Core;

/// <summary>
/// Selects how the member segments of a <see cref="ZodPath"/> are named when a host renders error keys.
/// </summary>
public enum ZodPathNaming
{
	/// <summary>
	/// The C# member name (<c>Items[0].Name</c>). This is the default everywhere and matches
	/// Microsoft.Extensions.Validation, MVC model state and Blazor's <c>FieldIdentifier</c>, so ZodSharp and
	/// DataAnnotations types in the same API produce the same key shape.
	/// </summary>
	MemberName,

	/// <summary>
	/// The JSON name produced by the host's configured naming policy (<c>items[0].name</c>).
	/// </summary>
	Json,
}

namespace ZodSharp.Core;

/// <summary>
/// Implemented by validators that can rename the member segments of an error path to their JSON names without
/// reflection, using the names recorded at compile time (<c>[JsonPropertyName]</c>) and, for members without
/// one, a naming policy. Generated validators implement it, so a host that renders JSON-named paths
/// (<c>PathNaming = Json</c>) can do so under trimming.
/// </summary>
public interface IZodJsonPathMapper
{
	/// <summary>
	/// Returns <paramref name="path"/> with every member segment renamed to its JSON name. Index and key
	/// segments are unchanged.
	/// </summary>
	/// <param name="path">The error path, relative to the validated type.</param>
	/// <param name="namingPolicy">
	/// The naming policy applied to members without an explicit JSON name (for example
	/// <c>JsonNamingPolicy.CamelCase.ConvertName</c>), or <see langword="null"/> to keep their C# names.
	/// </param>
	/// <returns>The renamed path.</returns>
	ZodPath ToJsonNamedPath(ZodPath path, Func<string, string>? namingPolicy = null);
}

namespace ZodSharp;

/// <summary>
/// The trimming and Native AOT requirement shared by every public API in this package that reaches Newtonsoft.Json
/// serialization.
/// </summary>
/// <remarks>
/// Newtonsoft.Json discovers members and constructors by reflection and emits code at runtime, and it carries no
/// trimming annotations of its own, so the analyzers cannot see the hazard. These APIs declare it explicitly instead,
/// so a trimmed or Native AOT application is warned at the call site rather than failing at runtime.
/// </remarks>
static class NewtonsoftJsonTrimming
{
	public const string Message =
		"Newtonsoft.Json is not trim/AOT compatible; use Purview.ZodSharp.SystemTextJson "
		+ "with a source-generated JsonSerializerContext instead.";
}

using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Serializable structured validation issue metadata included in ProblemDetails extensions.
/// </summary>
public readonly record struct ValidationIssue
{
	/// <summary>
	/// The issue code.
	/// </summary>
	public required string Code { get; init; }

	/// <summary>
	/// The optional category the issue belongs to.
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	/// The issue origin category.
	/// </summary>
	public string? Origin { get; init; }

	/// <summary>
	/// The inclusive minimum bound when present.
	/// </summary>
	public int? Minimum { get; init; }

	/// <summary>
	/// The inclusive maximum bound when present.
	/// </summary>
	public int? Maximum { get; init; }

	/// <summary>
	/// Whether the bound is inclusive.
	/// </summary>
	public bool? Inclusive { get; init; }

	/// <summary>
	/// The issue path segments.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public required string[] Path { get; init; }

	/// <summary>
	/// The human-readable issue message.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The additional error parameters carried by the validation error (for example aggregate ids).
	/// </summary>
	[System.Text.Json.Serialization.JsonIgnore(
		Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	)]
	public ErrorTypeParameters? Parameters { get; init; }
}

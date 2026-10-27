using System.Text.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The serialized shape of one validation issue in the ProblemDetails <c>issues</c> extension. It is a safe
/// projection of a <see cref="ValidationError"/>: only invariant scalar values are serialized, never raw
/// objects, so nothing internal leaks through a parameter.
/// </summary>
/// <remarks>
/// The JSON shape mirrors Zod's issue format:
/// <c>{ "path": "Items[0].Quantity", "segments": ["Items", 0, "Quantity"], "code": "too_small",
/// "category": "too_small", "origin": "number", "minimum": 1, "inclusive": true, "message": "…" }</c>.
/// The CLR-only members (<see cref="Path"/>, <see cref="Minimum"/>, <see cref="Maximum"/> and
/// <see cref="Parameters"/>) keep their 2.x meaning for code that reads the issues in-process, and are not
/// serialized.
/// </remarks>
public readonly record struct ValidationIssue
{
	/// <summary>
	/// The issue path rendered as the <c>errors</c> key (<c>Items[0].Quantity</c>); empty for the root.
	/// </summary>
	[JsonPropertyName("path")]
	[JsonPropertyOrder(0)]
	public string DottedPath { get; init; }

	/// <summary>
	/// The issue path in Zod's shape: strings for members and keys, integers for indexes.
	/// </summary>
	[JsonPropertyName("segments")]
	[JsonPropertyOrder(1)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public object[]? Segments { get; init; }

	/// <summary>
	/// The issue code.
	/// </summary>
	[JsonPropertyName("code")]
	[JsonPropertyOrder(2)]
	public required string Code { get; init; }

	/// <summary>
	/// The optional category the issue belongs to (the Zod issue code for built-in errors).
	/// </summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyName("category")]
	[JsonPropertyOrder(3)]
	public string? Category { get; init; }

	/// <summary>
	/// The issue origin category.
	/// </summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyName("origin")]
	[JsonPropertyOrder(4)]
	public string? Origin { get; init; }

	/// <summary>
	/// The lower bound, serialized as <c>minimum</c>: a JSON number for numeric bounds, an ISO-8601 string for
	/// dates and times.
	/// </summary>
	[JsonPropertyName("minimum")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyOrder(5)]
	public object? Lower { get; init; }

	/// <summary>
	/// The upper bound, serialized as <c>maximum</c>: a JSON number for numeric bounds, an ISO-8601 string for
	/// dates and times.
	/// </summary>
	[JsonPropertyName("maximum")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyOrder(6)]
	public object? Upper { get; init; }

	/// <summary>
	/// Whether the reported bound is inclusive.
	/// </summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyName("inclusive")]
	[JsonPropertyOrder(7)]
	public bool? Inclusive { get; init; }

	/// <summary>
	/// The human-readable issue message.
	/// </summary>
	[JsonPropertyName("message")]
	[JsonPropertyOrder(8)]
	public required string Message { get; init; }

	/// <summary>
	/// The issue's parameters, camel-cased and restricted to invariant scalar values (strings, numbers, booleans,
	/// ISO-8601 dates) and arrays of them.
	/// </summary>
	[JsonPropertyName("parameters")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyOrder(9)]
	public IReadOnlyDictionary<string, object?>? SafeParameters { get; init; }

	/// <summary>
	/// For a failed union, the issues reported by each option, with paths relative to the union.
	/// </summary>
	[JsonPropertyName("unionErrors")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	[JsonPropertyOrder(10)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public ValidationIssue[][]? UnionErrors { get; init; }

	/// <summary>
	/// The legacy issue path segments (indexes as digits). Not serialized; see <see cref="DottedPath"/> and
	/// <see cref="Segments"/>.
	/// </summary>
	[JsonIgnore]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays")]
	public required string[] Path { get; init; }

	/// <summary>
	/// The inclusive integer minimum bound when present. Not serialized; see <see cref="Lower"/>.
	/// </summary>
	[JsonIgnore]
	public int? Minimum { get; init; }

	/// <summary>
	/// The inclusive integer maximum bound when present. Not serialized; see <see cref="Upper"/>.
	/// </summary>
	[JsonIgnore]
	public int? Maximum { get; init; }

	/// <summary>
	/// The raw error parameters carried by the validation error. Not serialized; see <see cref="SafeParameters"/>.
	/// </summary>
	[JsonIgnore]
	public ErrorTypeParameters? Parameters { get; init; }
}

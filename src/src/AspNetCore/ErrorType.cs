namespace ZodSharp.AspNetCore;

/// <summary>
/// Defines a user-facing error type that maps a validation error code to an HTTP status code,
/// optional title/type metadata, and an optional message template whose named placeholders are
/// substituted from the error's <see cref="ZodSharp.Core.ValidationError.Parameters"/>.
/// </summary>
/// <param name="Code">The validation error code this error type maps to.</param>
/// <param name="Description">An optional human-readable description of the error.</param>
/// <param name="HttpStatus">
/// The HTTP status code returned when an error with this code is surfaced. Defaults to
/// <c>400 Bad Request</c>.
/// </param>
/// <param name="Title">An optional title used on the ProblemDetails payload.</param>
/// <param name="Type">An optional RFC 7807 type URI used on the ProblemDetails payload.</param>
/// <param name="MessageFormat">
/// An optional message template with named placeholders (for example
/// <c>"Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"</c>). Placeholders are
/// substituted from <see cref="ZodSharp.Core.ValidationError.Parameters"/>; placeholders without a
/// matching value are left as-is so templating gaps stay visible.
/// </param>
/// <example>
/// <code>
/// public static class ConcurrentErrorType
/// {
///     public static readonly ErrorType SaveFailed = new(
///         Code: "aggregate_save_failed",
///         Description: "The aggregate could not be saved.",
///         HttpStatus: StatusCodes.Status409Conflict,
///         MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save")
///     {
///         Parameters = ["AggregateId", "AggregateType"]
///     };
/// }
/// </code>
/// </example>
public sealed record ErrorType(
	string Code,
	string? Description = null,
	int HttpStatus = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
	string? Title = null,
	string? Type = null,
	string? MessageFormat = null
)
{
	/// <summary>
	/// The named placeholders expected by <see cref="MessageFormat"/> (for example "AggregateId").
	/// </summary>
	public IReadOnlyList<string> Parameters { get; init; } = [];
}

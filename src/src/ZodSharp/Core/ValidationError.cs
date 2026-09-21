using System.Collections.Immutable;

namespace ZodSharp.Core;

/// <summary>
/// Represents a validation error.
/// Uses struct to minimize allocations.
/// </summary>
public readonly record struct ValidationError
{
	/// <summary>
	/// The error code (e.g., "invalid_type", "too_small", "too_big")
	/// </summary>
	public string Code { get; }

	/// <summary>
	/// The optional category the error belongs to. A broad grouping that can span many specific
	/// codes (e.g. code "invalid_tenant_id" and category "invalid_value").
	/// </summary>
	public string? Category { get; }

	/// <summary>
	/// The error message
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// The path to the field that failed validation (e.g., ["user", "email"])
	/// </summary>
	public ImmutableArray<string> Path { get; }

	/// <summary>
	/// The origin category for structured issues (e.g. "string", "array", "collection").
	/// </summary>
	public string? Origin { get; }

	/// <summary>
	/// The inclusive minimum bound for size-based validation issues.
	/// </summary>
	public int? Minimum { get; }

	/// <summary>
	/// The inclusive maximum bound for size-based validation issues.
	/// </summary>
	public int? Maximum { get; }

	/// <summary>
	/// Indicates whether the bound is inclusive when supplied.
	/// </summary>
	public bool? Inclusive { get; }

	/// <summary>
	/// Additional error parameters. Typed container that also exposes the raw values through
	/// <see cref="ErrorTypeParameters.Get{T}"/> and <c>IReadOnlyDictionary&lt;string, object?&gt;</c>.
	/// </summary>
	public ErrorTypeParameters? Parameters { get; }

	/// <summary>
	/// Initializes a new instance of the ValidationError struct.
	/// </summary>
	/// <param name="code">The error code</param>
	/// <param name="message">The error message</param>
	/// <param name="path">The path to the field that failed validation</param>
	/// <param name="parameters">Additional error parameters</param>
	/// <param name="origin">The origin category for structured issues.</param>
	/// <param name="minimum">The inclusive minimum bound for structured size issues.</param>
	/// <param name="maximum">The inclusive maximum bound for structured size issues.</param>
	/// <param name="inclusive">Whether the structured bound is inclusive.</param>
	/// <param name="category">The optional category the error belongs to.</param>
	public ValidationError(
		string code,
		string message,
		string[]? path = null,
		IReadOnlyDictionary<string, object?>? parameters = null,
		string? origin = null,
		int? minimum = null,
		int? maximum = null,
		bool? inclusive = null,
		string? category = null
	)
	{
		Code = code;
		Category = category;
		Message = message;
		Path = path is null ? [] : ImmutableArray.Create(path);
		Parameters = Wrap(parameters);
		Origin = origin;
		Minimum = minimum;
		Maximum = maximum;
		Inclusive = inclusive;
	}

	/// <summary>
	/// Creates a validation error using a precomputed immutable path.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="path">The precomputed immutable path.</param>
	/// <param name="parameters">Additional error parameters.</param>
	/// <param name="origin">The origin category for structured issues.</param>
	/// <param name="minimum">The inclusive minimum bound for structured size issues.</param>
	/// <param name="maximum">The inclusive maximum bound for structured size issues.</param>
	/// <param name="inclusive">Whether the structured bound is inclusive.</param>
	/// <param name="category">The optional category the error belongs to.</param>
	public static ValidationError Create(
		string code,
		string message,
		ImmutableArray<string> path,
		IReadOnlyDictionary<string, object?>? parameters = null,
		string? origin = null,
		int? minimum = null,
		int? maximum = null,
		bool? inclusive = null,
		string? category = null
	) => new(code, message, path.IsDefault ? [] : path, parameters, origin, minimum, maximum, inclusive, category);

	ValidationError(
		string code,
		string message,
		ImmutableArray<string> path,
		IReadOnlyDictionary<string, object?>? parameters,
		string? origin,
		int? minimum,
		int? maximum,
		bool? inclusive,
		string? category
	)
	{
		Code = code;
		Category = category;
		Message = message;
		Path = path;
		Parameters = Wrap(parameters);
		Origin = origin;
		Minimum = minimum;
		Maximum = maximum;
		Inclusive = inclusive;
	}

	static ErrorTypeParameters? Wrap(IReadOnlyDictionary<string, object?>? parameters) =>
		parameters is ErrorTypeParameters typed ? typed
		: parameters is null ? null
		: ErrorTypeParameters.Create(parameters);
}

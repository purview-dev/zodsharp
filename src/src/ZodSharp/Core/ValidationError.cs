using System.Collections.Immutable;

namespace ZodSharp.Core;

/// <summary>
/// Represents a validation error.
/// Uses struct to minimize allocations.
/// </summary>
public readonly record struct ValidationError
{
	readonly ZodPath _fullPath;
	readonly ErrorBound? _lower;
	readonly ErrorBound? _upper;

	/// <summary>
	/// The error code (e.g., "invalid_type", "too_small", "too_big")
	/// </summary>
	public string Code { get; }

	/// <summary>
	/// The optional category the error belongs to. A broad grouping that can span many specific
	/// codes (e.g. code "invalid_tenant_id" and category "invalid_value"). Built-in errors use the Zod issue
	/// code here (see <see cref="ZodIssueCategories"/>), so a client can treat ZodSharp and Zod issues alike.
	/// </summary>
	public string? Category { get; init; }

	/// <summary>
	/// The error message
	/// </summary>
	public string Message { get; init; }

	/// <summary>
	/// The unformatted template that produced <see cref="Message"/>, using named placeholders resolved from
	/// <see cref="Parameters"/> and the built-in fields (see <see cref="ZodMessageFormat"/>). Hosts use it to
	/// re-format the message in the request culture; <see langword="null"/> when the message was supplied
	/// literally.
	/// </summary>
	public string? MessageFormat { get; init; }

	/// <summary>
	/// The path to the field that failed validation (e.g., ["user", "email"]). Index segments are rendered as
	/// their digits (["items", "0", "name"]), matching Zod. Prefer <see cref="FullPath"/>, which keeps the
	/// segment kinds.
	/// </summary>
	public ImmutableArray<string> Path => _fullPath.LegacySegments;

	/// <summary>
	/// The typed path to the field that failed validation. This is the authoritative location; render it with
	/// <see cref="ZodPath.ToDottedString"/> rather than joining <see cref="Path"/>.
	/// </summary>
	public ZodPath FullPath
	{
		get => _fullPath;
		init => _fullPath = value;
	}

	/// <summary>
	/// The origin category for structured issues: the Zod type of the failing value (e.g. "string", "number", "array", "record").
	/// </summary>
	public string? Origin { get; init; }

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
	/// The lower bound of a size or range issue, of any comparable type and with its own inclusivity. Falls
	/// back to <see cref="Minimum"/> when only an integer bound was supplied.
	/// </summary>
	public ErrorBound? Lower
	{
		get => _lower ?? (Minimum is { } minimum ? new ErrorBound(minimum, Inclusive ?? true) : null);
		init => _lower = value;
	}

	/// <summary>
	/// The upper bound of a size or range issue, of any comparable type and with its own inclusivity. Falls
	/// back to <see cref="Maximum"/> when only an integer bound was supplied.
	/// </summary>
	public ErrorBound? Upper
	{
		get => _upper ?? (Maximum is { } maximum ? new ErrorBound(maximum, Inclusive ?? true) : null);
		init => _upper = value;
	}

	/// <summary>
	/// Additional error parameters. Typed container that also exposes the raw values through
	/// <see cref="ErrorTypeParameters.Get{T}"/> and <c>IReadOnlyDictionary&lt;string, object?&gt;</c>.
	/// </summary>
	public ErrorTypeParameters? Parameters { get; init; }

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
		: this(code, message, ZodPath.FromLegacy(path), parameters, origin, minimum, maximum, inclusive, category) { }

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
	) => new(code, message, ZodPath.FromLegacy(path), parameters, origin, minimum, maximum, inclusive, category);

	/// <summary>
	/// Creates a validation error at a typed path.
	/// </summary>
	/// <param name="code">The error code.</param>
	/// <param name="message">The error message.</param>
	/// <param name="path">The typed path.</param>
	/// <param name="parameters">Additional error parameters.</param>
	/// <param name="origin">The origin category for structured issues.</param>
	/// <param name="minimum">The inclusive minimum bound for structured size issues.</param>
	/// <param name="maximum">The inclusive maximum bound for structured size issues.</param>
	/// <param name="inclusive">Whether the structured bound is inclusive.</param>
	/// <param name="category">The optional category the error belongs to.</param>
	public static ValidationError Create(
		string code,
		string message,
		ZodPath path,
		IReadOnlyDictionary<string, object?>? parameters = null,
		string? origin = null,
		int? minimum = null,
		int? maximum = null,
		bool? inclusive = null,
		string? category = null
	) => new(code, message, path, parameters, origin, minimum, maximum, inclusive, category);

	ValidationError(
		string code,
		string message,
		ZodPath path,
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
		_fullPath = path;
		Parameters = Wrap(parameters);
		Origin = origin;
		Minimum = minimum;
		Maximum = maximum;
		Inclusive = inclusive;
	}

	/// <summary>
	/// Returns a copy of this error located under <paramref name="segment"/>, keeping every other field. This
	/// is the only supported way to re-path an error.
	/// </summary>
	/// <param name="segment">The outer segment.</param>
	/// <returns>The re-pathed error.</returns>
	public ValidationError WithPathPrefix(ZodPathSegment segment) =>
		this with
		{
			FullPath = _fullPath.Prepend(segment),
		};

	/// <summary>
	/// Returns a copy of this error located under <paramref name="prefix"/>, keeping every other field.
	/// </summary>
	/// <param name="prefix">The outer path.</param>
	/// <returns>The re-pathed error.</returns>
	public ValidationError WithPathPrefix(ZodPath prefix) =>
		prefix.IsRoot ? this : this with { FullPath = _fullPath.Prepend(prefix) };

	/// <summary>
	/// Returns a copy of this error with the supplied parameters, keeping every other field.
	/// </summary>
	/// <param name="parameters">The parameters.</param>
	/// <returns>The updated error.</returns>
	public ValidationError WithParameters(IReadOnlyDictionary<string, object?>? parameters) =>
		this with
		{
			Parameters = Wrap(parameters),
		};

	/// <inheritdoc/>
	public bool Equals(ValidationError other) =>
		string.Equals(Code, other.Code, StringComparison.Ordinal)
		&& string.Equals(Category, other.Category, StringComparison.Ordinal)
		&& string.Equals(Message, other.Message, StringComparison.Ordinal)
		&& string.Equals(MessageFormat, other.MessageFormat, StringComparison.Ordinal)
		&& string.Equals(Origin, other.Origin, StringComparison.Ordinal)
		&& _fullPath.Equals(other._fullPath)
		&& Minimum == other.Minimum
		&& Maximum == other.Maximum
		&& Inclusive == other.Inclusive
		&& Nullable.Equals(_lower, other._lower)
		&& Nullable.Equals(_upper, other._upper)
		&& ParametersEqual(Parameters, other.Parameters);

	/// <inheritdoc/>
	public override int GetHashCode() =>
		HashCode.Combine(
			Code,
			Category,
			Message,
			_fullPath,
			Origin,
			HashCode.Combine(Minimum, Maximum, Inclusive, _lower, _upper),
			Parameters?.Count ?? 0
		);

	static bool ParametersEqual(ErrorTypeParameters? left, ErrorTypeParameters? right)
	{
		if (ReferenceEquals(left, right))
			return true;

		var leftCount = left?.Count ?? 0;
		var rightCount = right?.Count ?? 0;
		if (leftCount != rightCount)
			return false;

		if (leftCount == 0)
			return true;

		foreach (var pair in left!)
		{
			if (!right!.TryGetValue(pair.Key, out var value) || !Equals(pair.Value, value))
				return false;
		}

		return true;
	}

	static ErrorTypeParameters? Wrap(IReadOnlyDictionary<string, object?>? parameters) =>
		parameters is ErrorTypeParameters typed ? typed
		: parameters is null ? null
		: ErrorTypeParameters.Create(parameters);
}

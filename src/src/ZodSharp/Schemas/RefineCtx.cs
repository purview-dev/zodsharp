using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Context passed to a <see cref="ZodType{TOutput,TInput}.SuperRefine"/> callback.
/// Mirrors Zod's <c>ctx</c> argument to <c>superRefine</c>, allowing refinements
/// to emit multiple, path-located issues rather than a single boolean verdict.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <remarks>
/// Issue paths are relative to the refined value; schemas that contain the refined schema (objects, arrays,
/// records, tuples) prefix them with the value's own location, so a refinement never needs to know where it is
/// used. Issues default to the <see cref="ZodIssueCategories.Custom"/> category, as Zod's custom issues do.
/// </remarks>
public sealed class RefineCtx<T>
{
	// The issue list is allocated on the first issue rather than up front, so a refinement that passes
	// costs a single context allocation and a refinement that never runs costs none.
	List<ValidationError>? _issues;

	/// <summary>
	/// Initializes a new instance of the <see cref="RefineCtx{T}"/> class with the value under validation and an
	/// optional base path.
	/// </summary>
	/// <param name="value">The validated value.</param>
	/// <param name="path">The base path prepended to any added issues.</param>
	public RefineCtx(T value, ImmutableArray<string> path)
		: this(value, ZodPath.FromLegacy(path)) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="RefineCtx{T}"/> class with the value under validation and a
	/// typed base path.
	/// </summary>
	/// <param name="value">The validated value.</param>
	/// <param name="path">The base path prepended to any added issues.</param>
	public RefineCtx(T value, ZodPath path)
	{
		Value = value;
		FullPath = path;
	}

	/// <summary>
	/// The value being validated.
	/// </summary>
	public T Value { get; }

	/// <summary>
	/// The base path under which issues are reported, in the legacy string form. Prefer <see cref="FullPath"/>.
	/// </summary>
	public ImmutableArray<string> Path => FullPath.LegacySegments;

	/// <summary>
	/// The typed base path under which issues are reported.
	/// </summary>
	public ZodPath FullPath { get; }

	/// <summary>
	/// The issues added by the refinement so far.
	/// </summary>
	public IReadOnlyList<ValidationError> Issues => _issues ?? (IReadOnlyList<ValidationError>)[];

	/// <summary>
	/// True when at least one issue has been added.
	/// </summary>
	public bool HasIssues => _issues is { Count: > 0 };

	/// <summary>
	/// Adds a validation issue with the given code and message, prefixed by the
	/// context's base path.
	/// </summary>
	/// <param name="code">The issue code (e.g. <c>refinement_failed</c>).</param>
	/// <param name="message">The human-readable issue message.</param>
	/// <param name="path">
	/// An optional relative path appended to <see cref="Path"/>. Segments of digits (or <c>"[0]"</c>) are
	/// treated as indexes.
	/// </param>
	public void AddIssue(string code, string message, string[]? path = null) =>
		AddIssue(code, message, ZodPath.FromLegacy(path));

	/// <summary>
	/// Adds a validation issue at a typed relative path, prefixed by the context's base path.
	/// </summary>
	/// <param name="code">The issue code (e.g. <c>refinement_failed</c>).</param>
	/// <param name="message">The human-readable issue message.</param>
	/// <param name="path">The path relative to <see cref="FullPath"/>.</param>
	/// <param name="parameters">Optional issue parameters.</param>
	/// <param name="category">The issue category; <see cref="ZodIssueCategories.Custom"/> when <see langword="null"/>.</param>
	public void AddIssue(
		string code,
		string message,
		ZodPath path,
		IReadOnlyDictionary<string, object?>? parameters = null,
		string? category = null
	)
	{
		if (string.IsNullOrWhiteSpace(code))
			throw new ArgumentException("Issue code must not be null or whitespace.", nameof(code));
		if (string.IsNullOrWhiteSpace(message))
			throw new ArgumentException("Issue message must not be null or whitespace.", nameof(message));

		Add(
			ValidationError.Create(
				code,
				message,
				FullPath.Append(path),
				parameters,
				category: category ?? ZodIssueCategories.Custom
			)
		);
	}

	/// <summary>
	/// Adds a fully specified issue (bounds, origin, parameters, message format), prefixed by the context's base
	/// path. Every field of <paramref name="issue"/> is kept.
	/// </summary>
	/// <param name="issue">The issue, with a path relative to <see cref="FullPath"/>.</param>
	public void AddIssue(ValidationError issue)
	{
		if (string.IsNullOrWhiteSpace(issue.Code))
			throw new ArgumentException("Issue code must not be null or whitespace.", nameof(issue));

		Add(issue.WithPathPrefix(FullPath));
	}

	/// <summary>
	/// Adds a generic <c>refinement_failed</c> issue with the given message.
	/// Equivalent to calling <see cref="AddIssue(string, string, string[])"/> with code <c>refinement_failed</c>.
	/// </summary>
	/// <param name="message">The human-readable issue message.</param>
	/// <param name="path">An optional relative path appended to <see cref="Path"/>.</param>
	public void AddIssue(string message, string[]? path = null) =>
		AddIssue(ZodIssueCodes.RefinementFailed, message, path);

	void Add(ValidationError issue) => (_issues ??= []).Add(issue);

	internal ImmutableArray<ValidationError> ToImmutable() => _issues is { Count: > 0 } ? [.. _issues] : [];
}

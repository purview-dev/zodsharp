using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that adds context-aware custom validation logic (<c>superRefine</c>).
/// Unlike <see cref="ZodRefinement{T}"/>, the refinement callback receives a
/// <see cref="RefineCtx{T}"/> and may emit multiple, path-located issues.
/// Equivalent to Zod's <c>superRefine</c> method.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodSuperRefinement{T}"/> class.
/// </remarks>
/// <param name="baseSchema">The base schema.</param>
/// <param name="refinement">The context-aware refinement callback.</param>
public class ZodSuperRefinement<T>(IZodSchema<T> baseSchema, Action<RefineCtx<T>> refinement) : ZodType<T>
{
	static readonly ImmutableArray<string> EmptyPath = [];

	/// <summary>
	/// Parses and validates the value with the context-aware refinement.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	protected override ValidationResult<T> ParseInternal(T value)
	{
		var baseResult = baseSchema.Validate(value);
		if (!baseResult.IsSuccess)
			return baseResult;

		RefineCtx<T> ctx = new(baseResult.Value!, EmptyPath);
		refinement(ctx);

		return ctx.HasIssues
			? ValidationResult<T>.Failure(ctx.ToImmutable())
			: ValidationResult<T>.Success(baseResult.Value!);
	}
}

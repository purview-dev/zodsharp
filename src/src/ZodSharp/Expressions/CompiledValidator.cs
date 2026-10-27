using System.Runtime.CompilerServices;
using ZodSharp.Core;

namespace ZodSharp.Expressions;

/// <summary>
/// Provides cached validator delegates for a schema.
/// </summary>
/// <remarks>
/// <para>
/// This type previously built its delegate with <c>System.Linq.Expressions</c>. That was removed: the
/// expression tree it produced was a single call to <see cref="IZodSchema{TInput, TOutput}.Validate"/>,
/// so it performed no inlining and removed no virtual dispatch, while costing a
/// <c>LambdaExpression.Compile</c> per call. Each compile emitted a <c>DynamicMethod</c> that is never
/// reclaimed in a non-collectible load context, so calling it per request — which the documented usage
/// invited — leaked both managed and native memory for the lifetime of the process. It also made the
/// package incompatible with Native AOT, because <c>Compile</c> is annotated
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute"/>.
/// </para>
/// <para>
/// The delegate returned now binds <see cref="IZodSchema{TInput, TOutput}.Validate"/> directly, which is
/// behaviourally identical and strictly cheaper. Results are cached per schema instance with weak keys,
/// so repeated calls for the same schema return the same delegate and a discarded schema is still
/// collectable.
/// </para>
/// </remarks>
public static class CompiledValidator
{
	/// <summary>
	/// Returns a validator delegate for <paramref name="schema"/>.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="schema">The schema to validate with.</param>
	/// <returns>
	/// A delegate equivalent to <see cref="IZodSchema{TInput, TOutput}.Validate"/>. The same delegate is
	/// returned for repeated calls with the same schema instance.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
	public static Func<T, ValidationResult<T>> Compile<T>(IZodSchema<T, T> schema)
	{
		ArgumentNullException.ThrowIfNull(schema);

		return ValidatorCache<T>.Validators.GetValue(schema, static key => key.Validate);
	}

	/// <summary>
	/// Returns a validator delegate that returns the validated value and throws on failure.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="schema">The schema to validate with.</param>
	/// <returns>
	/// A delegate equivalent to <c>Parse</c>. The same delegate is returned for repeated calls with the
	/// same schema instance.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
	/// <exception cref="ZodException">Thrown by the returned delegate when validation fails.</exception>
	public static Func<T, T> CompileParser<T>(IZodSchema<T, T> schema)
	{
		ArgumentNullException.ThrowIfNull(schema);

		return ValidatorCache<T>.Parsers.GetValue(schema, static key =>
		{
			var validate = Compile(key);

			return input =>
			{
				var result = validate(input);

				return result.IsSuccess ? result.Value : throw new ZodException(result.Errors);
			};
		});
	}

	// One table per T. ConditionalWeakTable is thread-safe and holds its keys weakly, so caching here
	// cannot keep a schema (or its closure over user state) alive.
	static class ValidatorCache<T>
	{
		internal static readonly ConditionalWeakTable<IZodSchema<T, T>, Func<T, ValidationResult<T>>> Validators = [];

		internal static readonly ConditionalWeakTable<IZodSchema<T, T>, Func<T, T>> Parsers = [];
	}
}

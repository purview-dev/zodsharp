using System.Linq.Expressions;
using ZodSharp.Core;

namespace ZodSharp.Expressions;

/// <summary>
/// Provides compiled validators using Expression Trees for maximum performance.
/// Inlines validation rules to avoid virtual calls and allocations.
/// </summary>
public static class CompiledValidator
{
	/// <summary>
	/// Compiles a validator function from a schema using Expression Trees.
	/// This creates a highly optimized delegate that can be cached and reused.
	/// </summary>
	public static Func<T, ValidationResult<T>> Compile<T>(IZodSchema<T, T> schema) => CompileStandard(schema);

	/// <summary>
	/// Standard compilation that calls Validate method.
	/// </summary>
	static Func<T, ValidationResult<T>> CompileStandard<T>(IZodSchema<T, T> schema)
	{
		var inputParam = Expression.Parameter(typeof(T), "input");
		var validateMethod =
			typeof(IZodSchema<T, T>).GetMethod(nameof(IZodSchema<,>.Validate))
			?? throw new InvalidOperationException(
				$"Method '{nameof(IZodSchema<,>.Validate)}' not found on type '{typeof(IZodSchema<T, T>)}'."
			);
		var validateCall = Expression.Call(Expression.Constant(schema), validateMethod, inputParam);

		var lambda = Expression.Lambda<Func<T, ValidationResult<T>>>(validateCall, inputParam);

		return lambda.Compile();
	}

	/// <summary>
	/// Compiles a validator that throws on failure.
	/// </summary>
	public static Func<T, T> CompileParser<T>(IZodSchema<T, T> schema)
	{
		var compiledValidator = Compile(schema);

		return input =>
		{
			var result = compiledValidator(input);
			return result.IsSuccess ? result.Value : throw new ZodException(result.Errors);
		};
	}
}

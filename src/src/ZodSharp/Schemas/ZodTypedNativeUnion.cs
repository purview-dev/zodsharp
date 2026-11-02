#if NET11_0_OR_GREATER
using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.Unions;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for a typed union of two schemas that yields a native C# 15
/// <see cref="NativeUnion{T1,T2}"/> on success.
/// </summary>
/// <typeparam name="T1">The first option's type.</typeparam>
/// <typeparam name="T2">The second option's type.</typeparam>
/// <remarks>
/// Only available when targeting <c>net11.0</c> or later. Prefer this over
/// <see cref="ZodTypedUnion{T1,T2}"/> when both option types are reference types: the native union
/// is allocation-free and supports exhaustive pattern matching, whereas value-type cases box.
/// </remarks>
/// <param name="option1">The first option schema.</param>
/// <param name="option2">The second option schema.</param>
public class ZodTypedNativeUnion<T1, T2>(IZodSchema<T1, T1> option1, IZodSchema<T2, T2> option2)
	: ZodType<NativeUnion<T1, T2>, object>
{
	static readonly string Option1Name = ZodTypeNames.Of(typeof(T1));
	static readonly string Option2Name = ZodTypeNames.Of(typeof(T2));
	static readonly ImmutableArray<string> OptionNames = [Option1Name, Option2Name];

	/// <summary>
	/// Validates the value against each option, returning the first match as a
	/// <see cref="NativeUnion{T1,T2}"/>.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result containing a native union or errors.</returns>
	protected override ValidationResult<NativeUnion<T1, T2>> ParseInternal(object value)
	{
		ImmutableArray<ValidationError> errors1;
		if (value is T1 typed1)
		{
			var result = option1.Validate(typed1);
			if (result.IsSuccess)
				return ValidationResult<NativeUnion<T1, T2>>.Success(result.Value);

			errors1 = result.Errors;
		}
		else
		{
			errors1 = [ZodIssues.InvalidType(Option1Name, value)];
		}

		ImmutableArray<ValidationError> errors2;
		if (value is T2 typed2)
		{
			var result = option2.Validate(typed2);
			if (result.IsSuccess)
				return ValidationResult<NativeUnion<T1, T2>>.Success(result.Value);

			errors2 = result.Errors;
		}
		else
		{
			errors2 = [ZodIssues.InvalidType(Option2Name, value)];
		}

		// Each option's issues are kept, in declaration order, with paths relative to the union.
		return ValidationResult<NativeUnion<T1, T2>>.Failure(ZodIssues.InvalidUnion([errors1, errors2], OptionNames));
	}
}
#endif

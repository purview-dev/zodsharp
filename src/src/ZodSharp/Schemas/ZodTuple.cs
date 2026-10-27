using System.Globalization;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for typed tuple validation. Validates a two-element array where each
/// position is validated against its own schema, returning a
/// <see cref="ValueTuple{T1,T2}"/>. Equivalent to Zod's
/// <c>z.tuple([a, b])</c>.
/// </summary>
/// <typeparam name="T1">The first element type.</typeparam>
/// <typeparam name="T2">The second element type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodTuple{T1,T2}"/> class.
/// </remarks>
/// <param name="schema1">The schema for the first element.</param>
/// <param name="schema2">The schema for the second element.</param>
public class ZodTuple<T1, T2>(IZodSchema<T1, T1> schema1, IZodSchema<T2, T2> schema2) : ZodType<(T1, T2), object?[]>
{
	/// <summary>
	/// Validates a two-element array, returning a typed tuple.
	/// </summary>
	/// <param name="value">The array to validate.</param>
	/// <returns>A validation result containing a typed tuple.</returns>
	protected override ValidationResult<(T1, T2)> ParseInternal(object?[] value)
	{
		if (value is null)
		{
			return ValidationResult<(T1, T2)>.Failure(
				ZodIssues.NullValue(ZodTypeNames.Tuple, origin: ZodTypeNames.Array)
			);
		}

		if (ZodTupleElements.LengthError(value.Length, 2) is { } lengthError)
			return ValidationResult<(T1, T2)>.Failure(lengthError);

		List<ValidationError> errors = [];
		T1 v1;
		T2 v2;

		ZodTupleElements.Validate(schema1, value[0], 0, errors, out v1);

		ZodTupleElements.Validate(schema2, value[1], 1, errors, out v2);

		return errors.Count > 0
			? ValidationResult<(T1, T2)>.Failure(errors)
			: ValidationResult<(T1, T2)>.Success((v1, v2));
	}
}

/// <summary>
/// Schema for typed tuple validation of three elements. Equivalent to Zod's
/// <c>z.tuple([a, b, c])</c>.
/// </summary>
/// <typeparam name="T1">The first element type.</typeparam>
/// <typeparam name="T2">The second element type.</typeparam>
/// <typeparam name="T3">The third element type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1005:Avoid excessive parameters on generic types")]
public class ZodTuple<T1, T2, T3>(IZodSchema<T1, T1> schema1, IZodSchema<T2, T2> schema2, IZodSchema<T3, T3> schema3)
	: ZodType<(T1, T2, T3), object?[]>
{
	/// <summary>
	/// Validates a three-element array, returning a typed tuple.
	/// </summary>
	/// <param name="value">The array to validate.</param>
	/// <returns>A validation result containing a typed tuple.</returns>
	protected override ValidationResult<(T1, T2, T3)> ParseInternal(object?[] value)
	{
		if (value is null)
		{
			return ValidationResult<(T1, T2, T3)>.Failure(
				ZodIssues.NullValue(ZodTypeNames.Tuple, origin: ZodTypeNames.Array)
			);
		}

		if (ZodTupleElements.LengthError(value.Length, 3) is { } lengthError)
			return ValidationResult<(T1, T2, T3)>.Failure(lengthError);

		List<ValidationError> errors = [];
		T1 v1;
		T2 v2;
		T3 v3;

		ZodTupleElements.Validate(schema1, value[0], 0, errors, out v1);

		ZodTupleElements.Validate(schema2, value[1], 1, errors, out v2);

		ZodTupleElements.Validate(schema3, value[2], 2, errors, out v3);

		return errors.Count > 0
			? ValidationResult<(T1, T2, T3)>.Failure(errors)
			: ValidationResult<(T1, T2, T3)>.Success((v1, v2, v3));
	}
}

/// <summary>
/// The element and length handling shared by the tuple schemas.
/// </summary>
static class ZodTupleElements
{
	/// <summary>
	/// The length error for a tuple of <paramref name="count"/> elements that must have exactly
	/// <paramref name="arity"/>, or <see langword="null"/> when the length matches.
	/// </summary>
	public static ValidationError? LengthError(int count, int arity)
	{
		if (count == arity)
			return null;

		var tooSmall = count < arity;
		var error = ValidationError.Create(
			ZodIssueCodes.InvalidTupleLength,
			string.Empty,
			ZodPath.Root,
			new Dictionary<string, object?>(StringComparer.Ordinal) { [ZodIssueParameters.Count] = count },
			ZodTypeNames.Array,
			arity,
			arity,
			inclusive: true,
			category: tooSmall ? ZodIssueCategories.TooSmall : ZodIssueCategories.TooBig
		) with
		{
			MessageFormat = ZodMessages.TupleLength,
		};

		return error with
		{
			Message = ZodMessageFormat.Format(ZodMessages.TupleLength, error, CultureInfo.InvariantCulture),
		};
	}

	/// <summary>
	/// Validates the element at <paramref name="index"/> against <paramref name="schema"/>, adding its errors
	/// (prefixed with the index) to <paramref name="errors"/>. A <see langword="null"/> element is handed to the
	/// schema when <typeparamref name="T"/> can hold it, so a nullable element schema decides for itself.
	/// </summary>
	/// <returns><see langword="true"/> when the element is valid.</returns>
	public static bool Validate<T>(
		IZodSchema<T, T> schema,
		object? element,
		int index,
		List<ValidationError> errors,
		out T result
	)
	{
		result = default!;

		T typed;
		if (element is T matched)
		{
			typed = matched;
		}
		else if (element is null && default(T) is null)
		{
			typed = default!;
		}
		else
		{
			errors.Add(
				ZodIssues.InvalidType(ZodTypeNames.Of(typeof(T)), element, ZodPath.From(ZodPathSegment.At(index)))
			);
			return false;
		}

		var validation = schema.Validate(typed);
		if (!validation.IsSuccess)
		{
			var segment = ZodPathSegment.At(index);
			foreach (var error in validation.Errors)
				errors.Add(error.WithPathPrefix(segment));

			return false;
		}

		result = validation.Value!;
		return true;
	}
}

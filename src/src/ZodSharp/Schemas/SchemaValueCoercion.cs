using System.Globalization;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Shared coercion logic used by <c>SchemaWrapper&lt;T&gt;</c> in the object and
/// discriminated-union builders, where untyped <see cref="object"/> values from a
/// dictionary (e.g. deserialized JSON) are handed to typed schemas.
/// </summary>
static class SchemaValueCoercion
{
	static readonly Type DoubleType = typeof(double);

	/// <summary>
	/// Validates a value coming through a typed-to-untyped schema wrapper, reusing the
	/// original boxed <paramref name="value"/> when the inner schema produced an equal
	/// value so value-typed fields do not allocate a fresh box per validation.
	/// </summary>
	public static ValidationResult<object> ValidateWrapped<T>(IZodSchema<T, T> inner, object? value)
	{
		if (value is null && inner is IAcceptsNull acceptsNull)
			return acceptsNull.ValidateNull();

		if (value is T typedValue)
		{
			var result = inner.Validate(typedValue);
			if (!result.IsSuccess)
				return ValidationResult<object>.Failure(result.Errors);

			// Reuse the original boxed value when the schema did not transform it.
			return SameValue(typedValue, result.Value)
				? ValidationResult<object>.Success(value)
				: ValidationResult<object>.Success(result.Value);
		}

		if (TryCoerce<T>(value, out var coerced))
		{
			var result = inner.Validate(coerced);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		return ValidationResult<object>.Failure(
			new ValidationError(
				"invalid_type",
				$"Expected {GetTypeDisplayName(typeof(T))}, but got {value?.GetType().Name ?? "null"}",
				[]
			)
		);
	}

	/// <summary>
	/// Returns <see langword="true"/> when two values are interchangeable: reference
	/// types compare by reference, value types by value.
	/// </summary>
	static bool SameValue<T>(T left, T right)
	{
		if (typeof(T).IsValueType)
			return EqualityComparer<T>.Default.Equals(left, right);

		// Reference types are interchangeable only when they are the same instance.
		return ReferenceEquals(left, right);
	}

	/// <summary>
	/// Attempts to coerce <paramref name="value"/> to <typeparamref name="T"/>.
	/// Returns <see langword="true"/> and sets <paramref name="result"/> on success;
	/// returns <see langword="false"/> when no safe coercion exists (caller should
	/// emit an <c>invalid_type</c> error).
	/// </summary>
	/// <remarks>
	/// Direct assignment is tried first. <see langword="null"/> passes through to the
	/// inner schema for reference types and <see cref="Nullable{T}"/> (so optional and
	/// defaulting schemas can handle a missing or null value), while failing for
	/// non-nullable value types. Numeric coercion (e.g. boxed <see cref="long"/>
	/// to <see cref="double"/>) is applied only when both the source and target are
	/// numeric <see cref="IConvertible"/> types, including numeric coercion into a
	/// <see cref="Nullable{T}"/> target (e.g. boxed <see cref="int"/> to
	/// <see cref="double"/>?), so non-numeric mismatches (e.g.
	/// <c>"not-a-number"</c> into <see cref="double"/>) still fail as before.
	/// </remarks>
	public static bool TryCoerce<T>(object? value, out T result)
	{
		if (value is null)
		{
			if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
			{
				result = default!;
				return false;
			}

			result = default!;
			return true;
		}

		if (value is T typedValue)
		{
			result = typedValue;
			return true;
		}

		var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
		if (value is IConvertible convertible && IsNumericType(targetType))
		{
			var sourceType = value.GetType();
			if (IsNumericType(sourceType))
			{
				try
				{
					result = (T)convertible.ToType(targetType, CultureInfo.InvariantCulture);
					return true;
				}
				catch (InvalidCastException)
				{
					// Overflow / unsupported numeric conversion - fall through to failure.
				}
				catch (OverflowException)
				{
					// Value out of target range - fall through to failure.
				}
			}
		}

		result = default!;
		return false;
	}

	static bool IsNumericType(Type type) =>
		type == DoubleType
		|| type == typeof(float)
		|| type == typeof(decimal)
		|| type == typeof(int)
		|| type == typeof(long)
		|| type == typeof(short)
		|| type == typeof(byte)
		|| type == typeof(uint)
		|| type == typeof(ulong)
		|| type == typeof(ushort)
		|| type == typeof(sbyte);

	/// <summary>
	/// Returns a human-readable name for <paramref name="type"/>, unwrapping
	/// <see cref="Nullable{T}"/> to its underlying type for display purposes.
	/// </summary>
	public static string GetTypeDisplayName(Type type) =>
		Nullable.GetUnderlyingType(type) is Type underlying ? underlying.Name : type.Name;
}

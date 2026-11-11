#if NET11_0_OR_GREATER
#pragma warning disable CA1815 // Native unions do not synthesize equality; the schema result does not need it.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ZodSharp.Unions;

/// <summary>
/// A non-boxing native C# 15 union of two types, produced by <c>Z.NativeUnion&lt;T1, T2&gt;</c>.
/// </summary>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <remarks>
/// <para>
/// Only available when targeting <c>net11.0</c> or later, where the C# 15
/// <see cref="UnionAttribute"/> and <see cref="System.Runtime.CompilerServices.IUnion"/> exist. The union is
/// declared by hand rather than with the <c>union</c> keyword so it can implement the language's
/// <em>non-boxing access pattern</em>: <see cref="HasValue"/> and the <see cref="TryGetValue(out T1)"/>
/// overloads let the compiler test a type pattern without boxing a value-type case, and the value is stored in
/// a typed field rather than a single <see cref="object"/> field.
/// </para>
/// <para>
/// The compiler still treats this as a union type: it synthesizes the implicit conversions from
/// <typeparamref name="T1"/> and <typeparamref name="T2"/>, and an exhaustive <c>switch</c> over the case types
/// needs no discard arm. <see cref="Value"/> is only the boxing view the union contract requires; pattern
/// matching and <see cref="TryGetValue(out T1)"/> never call it.
/// </para>
/// </remarks>
[Union]
[JsonUnion(TypeClassifier = typeof(NativeUnionTypeClassifierFactory))]
public readonly struct NativeUnion<T1, T2> : IUnion, System.Runtime.CompilerServices.IUnion
{
	/// <summary>No case is active (the default value, or a null reference/nullable case value).</summary>
	const byte None = 0;

	/// <summary>The first case type is active.</summary>
	const byte First = 1;

	/// <summary>The second case type is active.</summary>
	const byte Second = 2;

	readonly T1? _value1;
	readonly T2? _value2;
	readonly byte _tag;

	/// <summary>
	/// Whether a <typeparamref name="T"/> case can hold <see langword="null"/>. Read before the null test so a
	/// non-nullable value-type case never evaluates (and therefore never boxes) <c>value is null</c>.
	/// </summary>
	static class CaseTraits<T>
	{
		public static readonly bool CanBeNull =
			!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
	}

	/// <summary>
	/// Creates a union holding a <typeparamref name="T1"/> value.
	/// </summary>
	/// <param name="value">The value. A null reference or nullable value leaves the union empty.</param>
	public NativeUnion(T1 value)
	{
		if (CaseTraits<T1>.CanBeNull && value is null)
			return;

		_value1 = value;
		_tag = First;
	}

	/// <summary>
	/// Creates a union holding a <typeparamref name="T2"/> value.
	/// </summary>
	/// <param name="value">The value. A null reference or nullable value leaves the union empty.</param>
	public NativeUnion(T2 value)
	{
		if (CaseTraits<T2>.CanBeNull && value is null)
			return;

		_value2 = value;
		_tag = Second;
	}

	/// <summary>
	/// Gets the zero-based tag identifying which case is active: <c>0</c> for an empty union, <c>1</c> for
	/// <typeparamref name="T1"/> and <c>2</c> for <typeparamref name="T2"/>.
	/// </summary>
	public int Tag => _tag;

	/// <summary>
	/// Gets the boxed value of the active case, or <see langword="null"/> when the union is empty. Pattern
	/// matching and <see cref="TryGetValue(out T1)"/> are allocation-free; prefer them on hot paths.
	/// </summary>
	public object? Value =>
		_tag switch
		{
			First => _value1,
			Second => _value2,
			_ => null,
		};

	/// <summary>
	/// Gets whether the union holds a non-null value. The compiler uses this for a <c>null</c> pattern, so the
	/// union never has to touch <see cref="Value"/> to answer it.
	/// </summary>
	public bool HasValue => _tag != None;

	/// <summary>
	/// Gets the <typeparamref name="T1"/> value without boxing.
	/// </summary>
	/// <param name="value">The value when the first case is active.</param>
	/// <returns><see langword="true"/> when the union holds a <typeparamref name="T1"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T1? value)
	{
		if (_tag == First)
		{
			value = _value1!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Gets the <typeparamref name="T2"/> value without boxing.
	/// </summary>
	/// <param name="value">The value when the second case is active.</param>
	/// <returns><see langword="true"/> when the union holds a <typeparamref name="T2"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T2? value)
	{
		if (_tag == Second)
		{
			value = _value2!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>Returns a string representation of the active value.</summary>
	public override string ToString() => Value?.ToString() ?? "<empty>";
}
#pragma warning restore CA1815
#endif

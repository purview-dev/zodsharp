using System.Diagnostics.CodeAnalysis;

namespace ZodSharp.Unions;

/// <summary>
/// A non-boxing discriminated union of two types, equivalent to the .NET 11
/// <c>union(T1, T2)</c> declaration but usable on .NET 10 and .NET Standard 2.1.
/// </summary>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <remarks>
/// <para>
/// Construct via the implicit conversion operators from <typeparamref name="T1"/> or
/// <typeparamref name="T2"/>, or via <see cref="Create(T1)"/>. Deconstruct via
/// <see cref="TryGetValue(out T1)"/> / <see cref="TryGetValue(out T2)"/> (non-boxing),
/// <see cref="Match{TResult}"/>, or <see cref="Switch"/>.
/// </para>
/// <para>
/// <see cref="Tag"/> uses the same numbering as the net11.0-only <c>NativeUnion&lt;T1, T2&gt;</c>:
/// <c>0</c> is an empty union, <c>1</c> is <typeparamref name="T1"/> and <c>2</c> is
/// <typeparamref name="T2"/>. A default value (or an implicit conversion from a <see langword="null"/>
/// reference) is therefore empty, and <see cref="Value"/> returns <see langword="null"/> rather than throwing.
/// </para>
/// </remarks>
public readonly struct Union<T1, T2> : IUnion, IEquatable<Union<T1, T2>>
{
	readonly T1? _value1;
	readonly T2? _value2;

	Union(T1? value1, T2? value2, int tag)
	{
		_value1 = value1;
		_value2 = value2;
		Tag = tag;
	}

	/// <summary>Creates a union holding a <typeparamref name="T1"/> value.</summary>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2> Create(T1 value) => new(value, default, 1);

	/// <summary>Creates a union holding a <typeparamref name="T2"/> value.</summary>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2> Create(T2 value) => new(default, value, 2);

	/// <summary>Implicitly converts a <typeparamref name="T1"/> to the union.</summary>
	[return: NotNullIfNotNull(nameof(value))]
	public static implicit operator Union<T1, T2>(T1? value) =>
		value is null ? default : new Union<T1, T2>(value, default, 1);

	/// <summary>Implicitly converts a <typeparamref name="T2"/> to the union.</summary>
	[return: NotNullIfNotNull(nameof(value))]
	public static implicit operator Union<T1, T2>(T2? value) =>
		value is null ? default : new Union<T1, T2>(default, value, 2);

	/// <inheritdoc/>
	public int Tag { get; }

	/// <inheritdoc/>
	public object? Value =>
		Tag switch
		{
			1 => _value1,
			2 => _value2,
			_ => null,
		};

	/// <summary>
	/// Non-boxing attempt to retrieve the <typeparamref name="T1"/> value.
	/// </summary>
	/// <returns><see langword="true"/> if the union holds a <typeparamref name="T1"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T1? value)
	{
		if (Tag == 1)
		{
			value = _value1!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Non-boxing attempt to retrieve the <typeparamref name="T2"/> value.
	/// </summary>
	/// <returns><see langword="true"/> if the union holds a <typeparamref name="T2"/>.</returns>
	public bool TryGetValue([NotNullWhen(true)] out T2? value)
	{
		if (Tag == 2)
		{
			value = _value2!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Exhaustively maps the union to a result value.
	/// </summary>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public TResult Match<TResult>(Func<T1, TResult> case1, Func<T2, TResult> case2)
	{
		if (case1 is null)
			throw new ArgumentNullException(nameof(case1));
		if (case2 is null)
			throw new ArgumentNullException(nameof(case2));

		return Tag switch
		{
			1 => case1(_value1!),
			2 => case2(_value2!),
			_ => throw new InvalidOperationException("Union is empty."),
		};
	}

	/// <summary>
	/// Exhaustively executes an action for the active case.
	/// </summary>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public void Switch(Action<T1> case1, Action<T2> case2)
	{
		if (case1 is null)
			throw new ArgumentNullException(nameof(case1));
		if (case2 is null)
			throw new ArgumentNullException(nameof(case2));

		switch (Tag)
		{
			case 1:
				case1(_value1!);
				break;
			case 2:
				case2(_value2!);
				break;
			default:
				throw new InvalidOperationException("Union is empty.");
		}
	}

	/// <inheritdoc/>
	public bool Equals(Union<T1, T2> other) =>
		Tag == other.Tag
		&& Tag switch
		{
			1 => EqualityComparer<T1>.Default.Equals(_value1, other._value1),
			2 => EqualityComparer<T2>.Default.Equals(_value2, other._value2),
			_ => true,
		};

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is Union<T1, T2> other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() =>
		Tag switch
		{
			1 => _value1?.GetHashCode() ?? 0,
			2 => _value2?.GetHashCode() ?? 0,
			_ => 0,
		};

	/// <summary>Returns a string representation of the active value.</summary>
	public override string ToString() => Value?.ToString() ?? "<empty>";

	/// <summary>Equality operator.</summary>
	public static bool operator ==(Union<T1, T2> left, Union<T1, T2> right) => left.Equals(right);

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(Union<T1, T2> left, Union<T1, T2> right) => !left.Equals(right);
}

/// <summary>
/// A non-boxing discriminated union of three types.
/// </summary>
/// <typeparam name="T1">The first case type.</typeparam>
/// <typeparam name="T2">The second case type.</typeparam>
/// <typeparam name="T3">The third case type.</typeparam>
[SuppressMessage("Design", "CA1005:Avoid excessive parameters on generic types")]
public readonly struct Union<T1, T2, T3> : IUnion, IEquatable<Union<T1, T2, T3>>
{
	readonly T1? _value1;
	readonly T2? _value2;
	readonly T3? _value3;

	Union(T1? v1, T2? v2, T3? v3, int tag)
	{
		_value1 = v1;
		_value2 = v2;
		_value3 = v3;
		Tag = tag;
	}

	/// <summary>Creates a union holding a <typeparamref name="T1"/> value.</summary>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2, T3> Create(T1 value) => new(value, default, default, 1);

	/// <summary>Creates a union holding a <typeparamref name="T2"/> value.</summary>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2, T3> Create(T2 value) => new(default, value, default, 2);

	/// <summary>Creates a union holding a <typeparamref name="T3"/> value.</summary>
	[SuppressMessage(
		"Design",
		"CA1000:Do not declare static members on generic types",
		Justification = "Factory methods are intentionally placed on the generic union type."
	)]
	public static Union<T1, T2, T3> Create(T3 value) => new(default, default, value, 3);

	/// <summary>Implicitly converts a <typeparamref name="T1"/> to the union.</summary>
	[return: NotNullIfNotNull(nameof(value))]
	public static implicit operator Union<T1, T2, T3>(T1? value) =>
		value is null ? default : new Union<T1, T2, T3>(value, default, default, 1);

	/// <summary>Implicitly converts a <typeparamref name="T2"/> to the union.</summary>
	[return: NotNullIfNotNull(nameof(value))]
	public static implicit operator Union<T1, T2, T3>(T2? value) =>
		value is null ? default : new Union<T1, T2, T3>(default, value, default, 2);

	/// <summary>Implicitly converts a <typeparamref name="T3"/> to the union.</summary>
	[return: NotNullIfNotNull(nameof(value))]
	public static implicit operator Union<T1, T2, T3>(T3? value) =>
		value is null ? default : new Union<T1, T2, T3>(default, default, value, 3);

	/// <inheritdoc/>
	public int Tag { get; }

	/// <inheritdoc/>
	public object? Value =>
		Tag switch
		{
			1 => _value1,
			2 => _value2,
			3 => _value3,
			_ => null,
		};

	/// <summary>Non-boxing attempt to retrieve the <typeparamref name="T1"/> value.</summary>
	public bool TryGetValue([NotNullWhen(true)] out T1? value)
	{
		if (Tag == 1)
		{
			value = _value1!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>Non-boxing attempt to retrieve the <typeparamref name="T2"/> value.</summary>
	public bool TryGetValue([NotNullWhen(true)] out T2? value)
	{
		if (Tag == 2)
		{
			value = _value2!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>Non-boxing attempt to retrieve the <typeparamref name="T3"/> value.</summary>
	public bool TryGetValue([NotNullWhen(true)] out T3? value)
	{
		if (Tag == 3)
		{
			value = _value3!;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>Exhaustively maps the union to a result value.</summary>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public TResult Match<TResult>(Func<T1, TResult> case1, Func<T2, TResult> case2, Func<T3, TResult> case3)
	{
		if (case1 is null)
			throw new ArgumentNullException(nameof(case1));
		if (case2 is null)
			throw new ArgumentNullException(nameof(case2));
		if (case3 is null)
			throw new ArgumentNullException(nameof(case3));

		return Tag switch
		{
			1 => case1(_value1!),
			2 => case2(_value2!),
			3 => case3(_value3!),
			_ => throw new InvalidOperationException("Union is empty."),
		};
	}

	/// <summary>Exhaustively executes an action for the active case.</summary>
	/// <exception cref="InvalidOperationException">The union is empty.</exception>
	public void Switch(Action<T1> case1, Action<T2> case2, Action<T3> case3)
	{
		if (case1 is null)
			throw new ArgumentNullException(nameof(case1));
		if (case2 is null)
			throw new ArgumentNullException(nameof(case2));
		if (case3 is null)
			throw new ArgumentNullException(nameof(case3));

		switch (Tag)
		{
			case 1:
				case1(_value1!);
				break;
			case 2:
				case2(_value2!);
				break;
			case 3:
				case3(_value3!);
				break;
			default:
				throw new InvalidOperationException("Union is empty.");
		}
	}

	/// <inheritdoc/>
	public bool Equals(Union<T1, T2, T3> other) =>
		Tag == other.Tag
		&& Tag switch
		{
			1 => EqualityComparer<T1>.Default.Equals(_value1, other._value1),
			2 => EqualityComparer<T2>.Default.Equals(_value2, other._value2),
			3 => EqualityComparer<T3>.Default.Equals(_value3, other._value3),
			_ => true,
		};

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is Union<T1, T2, T3> other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() =>
		Tag switch
		{
			1 => _value1?.GetHashCode() ?? 0,
			2 => _value2?.GetHashCode() ?? 0,
			3 => _value3?.GetHashCode() ?? 0,
			_ => 0,
		};

	/// <summary>Returns a string representation of the active value.</summary>
	public override string ToString() => Value?.ToString() ?? "<empty>";

	/// <summary>Equality operator.</summary>
	public static bool operator ==(Union<T1, T2, T3> left, Union<T1, T2, T3> right) => left.Equals(right);

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(Union<T1, T2, T3> left, Union<T1, T2, T3> right) => !left.Equals(right);
}

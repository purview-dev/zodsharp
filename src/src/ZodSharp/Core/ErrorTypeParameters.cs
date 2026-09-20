using System.Collections;

namespace ZodSharp.Core;

/// <summary>
/// A typed, read-only set of error parameters. Carries the declared
/// <see cref="ErrorTypeParameter"/> metadata alongside the values so consumers can read values
/// through the strongly typed <see cref="Get{T}"/> accessor and format messages with knowledge of
/// each parameter's type. Implements <see cref="IReadOnlyDictionary{TKey, TValue}"/> so existing
/// dictionary-based consumers keep working unchanged.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1710:Identifiers should have correct suffix",
	Justification = "Real name"
)]
public sealed class ErrorTypeParameters : IReadOnlyDictionary<string, object?>
{
	static readonly IReadOnlyDictionary<string, ErrorTypeParameter> s_emptyDeclarations =
		new Dictionary<string, ErrorTypeParameter>();

	readonly IReadOnlyDictionary<string, object?> _values;
	readonly IReadOnlyDictionary<string, ErrorTypeParameter> _declarations;

	ErrorTypeParameters(
		IReadOnlyDictionary<string, object?> values,
		IReadOnlyDictionary<string, ErrorTypeParameter> declarations
	)
	{
		_values = values;
		_declarations = declarations;
	}

	/// <summary>
	/// Creates a typed parameter set from the declared parameters and their values. Each value's
	/// runtime type is validated against its declaration.
	/// </summary>
	/// <param name="declarations">The declared parameters (names and expected types).</param>
	/// <param name="values">The parameter values keyed by name.</param>
	/// <exception cref="ArgumentException">A value's type does not match its declared type.</exception>
	public static ErrorTypeParameters Create(
		IReadOnlyList<ErrorTypeParameter>? declarations,
		IEnumerable<KeyValuePair<string, object?>> values
	)
	{
		ArgumentNullException.ThrowIfNull(values);

		Dictionary<string, ErrorTypeParameter> declarationMap = new(StringComparer.Ordinal);
		if (declarations is not null)
		{
			foreach (var declaration in declarations)
				declarationMap[declaration.Name] = declaration;
		}

		Dictionary<string, object?> valueMap = new(StringComparer.Ordinal);
		foreach (var pair in values)
		{
			if (
				declarationMap.TryGetValue(pair.Key, out var declaration) && !IsCompatible(declaration.Type, pair.Value)
			)
			{
				throw new ArgumentException(
					$"Parameter '{pair.Key}' has value of type '{pair.Value?.GetType().FullName ?? "null"}' but was declared as '{declaration.Type.FullName}'.",
					nameof(values)
				);
			}

			valueMap[pair.Key] = pair.Value;
		}

		return new ErrorTypeParameters(valueMap, declarationMap);
	}

	/// <summary>
	/// Creates a typed parameter set from values only, without declared types.
	/// </summary>
	/// <param name="values">The parameter values keyed by name.</param>
	public static ErrorTypeParameters Create(IEnumerable<KeyValuePair<string, object?>> values)
	{
		ArgumentNullException.ThrowIfNull(values);

		Dictionary<string, object?> valueMap = new(StringComparer.Ordinal);
		foreach (var pair in values)
			valueMap[pair.Key] = pair.Value;

		return new ErrorTypeParameters(valueMap, s_emptyDeclarations);
	}

	/// <summary>
	/// Returns the value for <paramref name="name"/> cast to <typeparamref name="T"/>. Returns the
	/// default value for <typeparamref name="T"/> when the value is absent, is <c>null</c>, does not
	/// match the declared type, or is not assignable to <typeparamref name="T"/>.
	/// </summary>
	/// <param name="name">The parameter name.</param>
	public T? Get<T>(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (!_values.TryGetValue(name, out var value))
			return default;

		if (value is null)
			return default;

		if (_declarations.TryGetValue(name, out var declaration) && !IsCompatible(declaration.Type, value))
			return default;

		// The value is compatible with the declared type (if any), but may not be assignable to T.
		return value is T typed ? typed : default;
	}

	/// <summary>
	/// Returns the declared type for <paramref name="name"/>, or <c>null</c> when the value was
	/// created without declarations.
	/// </summary>
	/// <param name="name">The parameter name.</param>
	public Type? GetDeclaredType(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		return _declarations.TryGetValue(name, out var declaration) ? declaration.Type : null;
	}

	static bool IsCompatible(Type type, object? value)
	{
		if (value is null)
			return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

		// The value is non-null, so check if it is assignable to the declared type.
		return type.IsInstanceOfType(value);
	}

	/// <inheritdoc />
	public object? this[string key] => _values[key];

	/// <inheritdoc />
	public IEnumerable<string> Keys => _values.Keys;

	/// <inheritdoc />
	public IEnumerable<object?> Values => _values.Values;

	/// <inheritdoc />
	public int Count => _values.Count;

	/// <inheritdoc />
	public bool ContainsKey(string key) => _values.ContainsKey(key);

	/// <inheritdoc />
	public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

	/// <inheritdoc />
	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
}

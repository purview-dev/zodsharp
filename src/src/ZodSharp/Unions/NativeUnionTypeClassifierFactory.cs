#if NET11_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ZodSharp.Unions;

/// <summary>
/// Chooses the active case of a <see cref="NativeUnion{T1,T2}"/> when System.Text.Json reads a JSON value
/// that more than one case could accept.
/// </summary>
/// <remarks>
/// <para>
/// The .NET 11 serializer classifies a union case by the JSON token's kind. That is enough for an
/// <c>int | string</c> union with the default serializer settings, but not for the two shapes an HTTP API
/// produces: a string token under <see cref="JsonSerializerDefaults.Web"/> (where a number can also be read
/// from a string) and two object-shaped cases. Applying this factory through
/// <see cref="JsonUnionAttribute"/> lets the serializer read <see cref="NativeUnion{T1,T2}"/> payloads in
/// minimal APIs and other web hosts.
/// </para>
/// <para>
/// Classification is deterministic: the case whose natural JSON shape matches the token wins (a
/// <see cref="string"/> case for a string token before a number read from a string); object-shaped cases use
/// the platform's structural classifier (root property names); otherwise each candidate's own contract is
/// tried. A payload that no case, or more than one object-shaped case, can claim is left to the serializer,
/// which reports it as a <see cref="JsonException"/> - the same behaviour as a compiler-declared union.
/// </para>
/// </remarks>
public sealed class NativeUnionTypeClassifierFactory : JsonTypeClassifierFactory
{
	/// <inheritdoc/>
	public override bool CanClassify(JsonTypeClassifierContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.Kind == JsonTypeClassifierKind.Union;
	}

	/// <inheritdoc/>
	public override JsonTypeClassifier CreateJsonClassifier(
		JsonTypeClassifierContext context,
		JsonSerializerOptions options
	)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(options);

		var unionCases = context.UnionCases;
		var caseTypes = new Type[unionCases.Count];
		for (var i = 0; i < unionCases.Count; i++)
			caseTypes[i] = unionCases[i].CaseType;

		var structural = TryCreateStructuralClassifier(context, options);

		return (ref reader) => Classify(ref reader, caseTypes, options, structural);
	}

	/// <summary>
	/// Creates the platform's structural classifier for object-shaped cases, or <see langword="null"/> when
	/// the union has no object-shaped case (or the mix is one it cannot describe).
	/// </summary>
	static JsonTypeClassifier? TryCreateStructuralClassifier(
		JsonTypeClassifierContext context,
		JsonSerializerOptions options
	)
	{
		try
		{
			JsonUnionTypeStructuralClassifier factory = new();
			return factory.CanClassify(context) ? factory.CreateJsonClassifier(context, options) : null;
		}
		catch (Exception exception)
			when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
		{
			// The structural classifier rejects mixes it cannot describe (for example an object beside a
			// dictionary). Fall back to the token and contract checks below rather than failing the union.
			return null;
		}
	}

	static Type? Classify(
		ref Utf8JsonReader reader,
		Type[] caseTypes,
		JsonSerializerOptions options,
		JsonTypeClassifier? structural
	)
	{
		var token = reader.TokenType;

		// A null payload never reaches the classifier: the serializer selects the first nullable case, or
		// the default union value when no case accepts null.

		// Object-shaped cases are the one shape the platform can distinguish by property names. Let its
		// classifier decide first; it reports zero or multiple matches with a precise JsonException.
		if (token == JsonTokenType.StartObject && structural is not null)
			return structural(ref reader);

		List<Type> natural = [with(caseTypes.Length)];
		foreach (var caseType in caseTypes)
		{
			if (IsNaturalType(caseType, token, options))
				natural.Add(caseType);
		}

		if (natural.Count == 1)
			return natural[0];

		IReadOnlyList<Type> candidates = natural.Count > 0 ? natural : caseTypes;
		List<Type> readable = [with(candidates.Count)];
		foreach (var caseType in candidates)
		{
			if (CanRead(ref reader, caseType, options))
				readable.Add(caseType);
		}

		if (readable.Count == 1)
			return readable[0];

		// A scalar token has no structure left to disambiguate by, so declaration order is the stable
		// tie-break. An object or array token that several cases can read is genuinely ambiguous.
		if (readable.Count > 1 && token is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
			return readable[0];

		// Let the serializer report the ambiguity or lack of a match as a JsonException, which is the same
		// behaviour as a compiler-declared union.
		return null;
	}

	static bool IsNaturalType(Type caseType, JsonTokenType token, JsonSerializerOptions options)
	{
		if (token is JsonTokenType.StartObject or JsonTokenType.StartArray)
		{
			var kind = KindOf(caseType, options);
			return token == JsonTokenType.StartObject
				? kind is JsonTypeInfoKind.Object or JsonTypeInfoKind.Dictionary
				: kind == JsonTypeInfoKind.Enumerable;
		}

		return IsNaturalScalar(caseType, token, options);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static bool IsNaturalScalar(Type caseType, JsonTokenType token, JsonSerializerOptions options) =>
		token switch
		{
			JsonTokenType.Number => IsNumeric(caseType) || (caseType.IsEnum && !EnumReadsStrings(caseType, options)),
			JsonTokenType.String => caseType == typeof(string)
				|| caseType == typeof(char)
				|| caseType == typeof(Guid)
				|| caseType == typeof(DateTime)
				|| caseType == typeof(DateTimeOffset)
				|| caseType == typeof(DateOnly)
				|| caseType == typeof(TimeOnly)
				|| caseType == typeof(TimeSpan)
				|| caseType == typeof(Uri)
				|| caseType == typeof(byte[])
				|| (caseType.IsEnum && EnumReadsStrings(caseType, options)),
			JsonTokenType.True or JsonTokenType.False => caseType == typeof(bool),
			_ => false,
		};

	static bool IsNumeric(Type type) =>
		Type.GetTypeCode(type)
			is TypeCode.Byte
				or TypeCode.SByte
				or TypeCode.Int16
				or TypeCode.UInt16
				or TypeCode.Int32
				or TypeCode.UInt32
				or TypeCode.Int64
				or TypeCode.UInt64
				or TypeCode.Single
				or TypeCode.Double
				or TypeCode.Decimal
		|| type == typeof(Half)
		|| type == typeof(Int128)
		|| type == typeof(UInt128)
		|| type == typeof(System.Numerics.BFloat16)
		|| type == typeof(System.Numerics.Decimal32)
		|| type == typeof(System.Numerics.Decimal64)
		|| type == typeof(System.Numerics.Decimal128);

	static bool EnumReadsStrings(Type type, JsonSerializerOptions options)
	{
		try
		{
			return options.GetTypeInfo(type).Converter is JsonStringEnumConverter;
		}
		catch (Exception exception)
			when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
		{
			return false;
		}
	}

	static JsonTypeInfoKind KindOf(Type type, JsonSerializerOptions options)
	{
		try
		{
			return options.GetTypeInfo(type).Kind;
		}
		catch (Exception exception)
			when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
		{
			return JsonTypeInfoKind.None;
		}
	}

	static bool CanRead(ref Utf8JsonReader reader, Type caseType, JsonSerializerOptions options)
	{
		try
		{
			var typeInfo = options.GetTypeInfo(caseType);
			var probe = reader;
			JsonSerializer.Deserialize(ref probe, typeInfo);
			return true;
		}
		catch (Exception exception)
			when (exception is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
		{
			return false;
		}
	}
}
#endif

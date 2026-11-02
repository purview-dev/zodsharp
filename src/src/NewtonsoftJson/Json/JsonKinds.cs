using System.Collections;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ZodSharp.Json;

/// <summary>
/// Names JSON values and CLR types with Zod's type names (<c>string</c>, <c>number</c>, <c>boolean</c>,
/// <c>object</c>, <c>array</c>, <c>null</c>), so messages describe the JSON a client sent rather than the
/// CLR or <see cref="JToken"/> type that happened to hold it. Only the kind is ever reported, never the
/// value, so no input is echoed into a message.
/// </summary>
static class JsonKinds
{
	/// <summary>The name used when the expected type cannot be determined.</summary>
	public const string Unknown = "the expected type";

	static readonly Dictionary<Type, string> ScalarNames = new()
	{
		[typeof(bool)] = "boolean",
		[typeof(byte)] = "number",
		[typeof(sbyte)] = "number",
		[typeof(short)] = "number",
		[typeof(ushort)] = "number",
		[typeof(int)] = "number",
		[typeof(uint)] = "number",
		[typeof(long)] = "number",
		[typeof(ulong)] = "number",
		[typeof(float)] = "number",
		[typeof(double)] = "number",
		[typeof(decimal)] = "number",
		[typeof(Half)] = "number",
		[typeof(Int128)] = "number",
		[typeof(UInt128)] = "number",
		[typeof(System.Numerics.BigInteger)] = "number",
		[typeof(string)] = "string",
		[typeof(char)] = "string",
		[typeof(Guid)] = "string",
		[typeof(DateTime)] = "string",
		[typeof(DateTimeOffset)] = "string",
		[typeof(DateOnly)] = "string",
		[typeof(TimeOnly)] = "string",
		[typeof(TimeSpan)] = "string",
		[typeof(Uri)] = "string",
		[typeof(Version)] = "string",
		[typeof(byte[])] = "string",
	};

	/// <summary>
	/// The Zod name of a value held as a <see cref="JToken"/> or as a plain CLR value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfValue(object? value) =>
		value switch
		{
			null => "null",
			JToken token => OfTokenType(token.Type),
			string or char or Guid or DateTime or DateTimeOffset => "string",
			bool => "boolean",
			byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
			IDictionary => "object",
			IEnumerable => "array",
			_ => "object",
		};

	/// <summary>
	/// The Zod name of a <see cref="JTokenType"/>.
	/// </summary>
	/// <param name="type">The token type.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfTokenType(JTokenType type) =>
		type switch
		{
			JTokenType.Object => "object",
			JTokenType.Array => "array",
			JTokenType.Integer => "number",
			JTokenType.Float => "number",
			JTokenType.String => "string",
			JTokenType.Guid => "string",
			JTokenType.Uri => "string",
			JTokenType.TimeSpan => "string",
			JTokenType.Date => "string",
			JTokenType.Bytes => "string",
			JTokenType.Boolean => "boolean",
			JTokenType.Null => "null",
			JTokenType.Undefined => "undefined",
			JTokenType.None => "undefined",
			JTokenType.Constructor => "object",
			JTokenType.Property => "object",
			JTokenType.Comment => "undefined",
			JTokenType.Raw => "undefined",
			_ => "undefined",
		};

	/// <summary>
	/// The Zod name of the JSON a contract reads, or <see langword="null"/> when it cannot be named.
	/// </summary>
	/// <param name="contract">The contract.</param>
	/// <returns>The Zod type name, or <see langword="null"/>.</returns>
	public static string? OfContract(JsonContract contract) =>
		contract switch
		{
			JsonArrayContract => "array",
			JsonObjectContract or JsonDictionaryContract or JsonDynamicContract => "object",
			JsonPrimitiveContract or JsonStringContract => OfScalarType(contract.UnderlyingType),
			_ => null,
		};

	/// <summary>
	/// The Zod name of a scalar CLR type, or <see langword="null"/> when it cannot be named.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The Zod type name, or <see langword="null"/>.</returns>
	public static string? OfScalarType(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;

		return type.IsEnum ? "enum" : ScalarNames.GetValueOrDefault(type);
	}
}

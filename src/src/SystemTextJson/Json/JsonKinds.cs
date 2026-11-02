using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ZodSharp.Json;

/// <summary>
/// Names JSON values and CLR types with Zod's type names (<c>string</c>, <c>number</c>, <c>boolean</c>,
/// <c>object</c>, <c>array</c>, <c>null</c>), so messages describe the JSON a client sent rather than the
/// CLR or <see cref="JsonElement"/> type that happened to hold it. Only the kind is ever reported, never the
/// value, so no input is echoed into a message.
/// </summary>
static class JsonKinds
{
	/// <summary>The name used when the expected type cannot be determined.</summary>
	public const string Unknown = "the expected type";

	/// <summary>
	/// The Zod name of a value held as a <see cref="JsonElement"/> or as a plain CLR value.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfValue(object? value) =>
		value switch
		{
			null => "null",
			JsonElement element => OfValueKind(element.ValueKind),
			string or char or Guid or DateTime or DateTimeOffset => "string",
			bool => "boolean",
			byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
			IDictionary => "object",
			IEnumerable => "array",
			_ => "object",
		};

	/// <summary>
	/// The Zod name of a <see cref="JsonValueKind"/>.
	/// </summary>
	/// <param name="kind">The value kind.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfValueKind(JsonValueKind kind) =>
		kind switch
		{
			JsonValueKind.Object => "object",
			JsonValueKind.Array => "array",
			JsonValueKind.String => "string",
			JsonValueKind.Number => "number",
			JsonValueKind.True => "boolean",
			JsonValueKind.False => "boolean",
			JsonValueKind.Null => "null",
			JsonValueKind.Undefined => "undefined",
			_ => "undefined",
		};

	/// <summary>
	/// The Zod name of the JSON a contract reads, or <see langword="null"/> when it cannot be named.
	/// </summary>
	/// <param name="typeInfo">The contract.</param>
	/// <returns>The Zod type name, or <see langword="null"/>.</returns>
	public static string? OfContract(JsonTypeInfo typeInfo) =>
		typeInfo.Kind switch
		{
			JsonTypeInfoKind.Object => "object",
			JsonTypeInfoKind.Dictionary => "object",
			JsonTypeInfoKind.Enumerable => "array",
			JsonTypeInfoKind.None => OfScalarType(typeInfo.Type),
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
		// Binary data is written as a base64 string.
		[typeof(byte[])] = "string",
		[typeof(Memory<byte>)] = "string",
		[typeof(ReadOnlyMemory<byte>)] = "string",
	};
}

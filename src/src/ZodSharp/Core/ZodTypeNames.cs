using System.Collections;
using System.Numerics;
using System.Text.Json;

namespace ZodSharp.Core;

/// <summary>
/// Maps CLR types and values to the type names Zod uses in its messages and <c>expected</c>/<c>received</c>
/// issue fields (<c>string</c>, <c>number</c>, <c>bigint</c>, <c>boolean</c>, <c>date</c>, <c>object</c>,
/// <c>array</c>, …), so CLR names such as <c>Int32</c> or <c>JsonElement</c> never reach a client.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Naming",
	"CA1720:Identifier contains type name",
	Justification = "The constants are Zod's type names."
)]
public static class ZodTypeNames
{
	/// <summary>The Zod name for text values.</summary>
	public const string String = "string";

	/// <summary>The Zod name for numeric values.</summary>
	public const string Number = "number";

	/// <summary>The Zod name for arbitrary-precision integers.</summary>
	public const string BigInt = "bigint";

	/// <summary>The Zod name for <see langword="true"/>/<see langword="false"/> values.</summary>
	public const string Boolean = "boolean";

	/// <summary>The Zod name for dates and times.</summary>
	public const string Date = "date";

	/// <summary>The Zod name for objects (including dictionaries).</summary>
	public const string Object = "object";

	/// <summary>The Zod name for arrays and other sequences.</summary>
	public const string Array = "array";

	/// <summary>The Zod name for tuples.</summary>
	public const string Tuple = "tuple";

	/// <summary>The Zod name for records (string-keyed dictionaries).</summary>
	public const string Record = "record";

	/// <summary>The Zod name for sets.</summary>
	public const string Set = "set";

	/// <summary>The name used for enum values.</summary>
	public const string Enum = "enum";

	/// <summary>The Zod name for the <see langword="null"/> value.</summary>
	public const string Null = "null";

	/// <summary>The Zod name for a not-a-number value.</summary>
	public const string NaN = "nan";

	/// <summary>The Zod name for a value of unknown type.</summary>
	public const string Unknown = "unknown";

	/// <summary>
	/// Gets the Zod type name for <paramref name="type"/>. <see cref="Nullable{T}"/> is unwrapped.
	/// </summary>
	/// <param name="type">The CLR type.</param>
	/// <returns>The Zod type name; <see cref="Unknown"/> when <paramref name="type"/> is <see langword="null"/>.</returns>
	public static string Of(Type? type)
	{
		if (type is null)
			return Unknown;

		type = Nullable.GetUnderlyingType(type) ?? type;

		return OfScalar(type) ?? OfComposite(type);
	}

	static string? OfScalar(Type type)
	{
		if (
			type == typeof(string)
			|| type == typeof(char)
			|| type == typeof(Guid)
			|| type == typeof(Uri)
			|| type == typeof(TimeOnly)
			|| type == typeof(TimeSpan)
		)
		{
			return String;
		}

		if (type == typeof(bool))
			return Boolean;

		if (type.IsEnum)
			return Enum;

		if (type == typeof(BigInteger) || type == typeof(Int128) || type == typeof(UInt128))
			return BigInt;

		if (IsNumber(type))
			return Number;

		if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
			return Date;

		return type == typeof(JsonElement) || type == typeof(object) ? Unknown : null;
	}

	static string OfComposite(Type type)
	{
		if (type.IsArray)
			return Array;

		if (typeof(IDictionary).IsAssignableFrom(type) || ImplementsGeneric(type, typeof(IReadOnlyDictionary<,>)))
			return Object;

		if (
			ImplementsGeneric(type, typeof(ISet<>))
			|| ImplementsGeneric(type, typeof(IReadOnlySet<>))
			|| ImplementsGeneric(type, typeof(HashSet<>))
			|| ImplementsGeneric(type, typeof(SortedSet<>))
		)
		{
			return Set;
		}

		if (typeof(IEnumerable).IsAssignableFrom(type))
			return Array;

		return Object;
	}

	/// <summary>
	/// Gets the Zod type name for <paramref name="value"/>'s runtime type; <see cref="Null"/> for
	/// <see langword="null"/>, <see cref="NaN"/> for a not-a-number floating-point value, and the JSON kind for a
	/// <see cref="JsonElement"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfValue(object? value) =>
		value switch
		{
			null => Null,
			double d when double.IsNaN(d) => NaN,
			float f when float.IsNaN(f) => NaN,
			JsonElement element => OfJsonKind(element.ValueKind),
			_ => Of(value.GetType()),
		};

	/// <summary>
	/// Gets the Zod type name for a JSON value kind.
	/// </summary>
	/// <param name="kind">The JSON value kind.</param>
	/// <returns>The Zod type name.</returns>
	public static string OfJsonKind(JsonValueKind kind) =>
		kind switch
		{
			JsonValueKind.Object => Object,
			JsonValueKind.Array => Array,
			JsonValueKind.String => String,
			JsonValueKind.Number => Number,
			JsonValueKind.True or JsonValueKind.False => Boolean,
			JsonValueKind.Null => Null,
			JsonValueKind.Undefined => Unknown,
			_ => Unknown,
		};

	static bool IsNumber(Type type) =>
		type == typeof(double)
		|| type == typeof(float)
		|| type == typeof(decimal)
		|| type == typeof(Half)
		|| type == typeof(int)
		|| type == typeof(long)
		|| type == typeof(short)
		|| type == typeof(byte)
		|| type == typeof(uint)
		|| type == typeof(ulong)
		|| type == typeof(ushort)
		|| type == typeof(sbyte)
		|| type == typeof(nint)
		|| type == typeof(nuint);

	// Only the type's own generic definition is inspected (not its interfaces), which keeps the lookup
	// trim-safe; the common concrete dictionaries and sets also implement the non-generic interfaces.
	static bool ImplementsGeneric(Type type, Type openGeneric) =>
		type.IsGenericType && type.GetGenericTypeDefinition() == openGeneric;
}

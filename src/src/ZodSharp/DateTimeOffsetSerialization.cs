namespace ZodSharp;

/// <summary>
/// Controls the JSON wire form assumed for <see cref="DateTimeOffset"/> values when the
/// <c>[ZodSchema]</c> source generator exports JSON Schema.
/// </summary>
/// <remarks>
/// <para>
/// This setting affects schema generation only; it does not serialize anything by itself. A <see cref="Utc"/>
/// contract must be paired with a converter that writes UTC and rejects non-UTC input, such as the
/// integration package's <c>UtcDateTimeOffsetConverter</c>, so the schema and the serializer agree.
/// </para>
/// <para>
/// The default, <see cref="PreserveOffset"/>, leaves the offset in the wire form. The exported
/// <c>[NonSentinel]</c> pattern is offset-aware, so it rejects every RFC 3339 representation of the
/// instant-based bounds. <see cref="Utc"/> is a narrower contract: the schema requires a zero offset and is
/// paired with a converter that rejects non-UTC input.
/// </para>
/// </remarks>
public enum DateTimeOffsetSerialization
{
	/// <summary>
	/// The wire form keeps the value's offset. This is the default and works with the serializer's normal
	/// DateTimeOffset behavior.
	/// </summary>
	PreserveOffset = 0,

	/// <summary>
	/// The wire form is UTC: a <c>Z</c> or zero offset. The schema rejects every other offset and is paired
	/// with a converter that also rejects non-UTC input.
	/// </summary>
	Utc = 1,
}

using System.Globalization;
using Newtonsoft.Json;

namespace ZodSharp.Json.Serialization.NewtonsoftJson;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> as UTC and rejects a non-UTC offset on read.
/// </summary>
/// <remarks>
/// <para>
/// Pair this converter with a UTC JSON Schema contract, for example
/// <c>[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc)]</c>
/// or the <c>ZodSharpDateTimeOffsetSerialization</c> MSBuild property. The schema requires a <c>Z</c> or
/// zero offset, so rejecting a non-UTC value here keeps the serializer and the schema in agreement.
/// </para>
/// <para>
/// Newtonsoft.Json parses date-like strings into <see cref="DateTime"/> by default, which discards the
/// offset before a converter sees it. Configure the serializer with
/// <c>DateParseHandling = DateParseHandling.None</c> so this converter can inspect the original string; the
/// converter reports a clear exception when that setting is missing.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1062:Validate arguments of public methods",
	Justification = "Called by Newtonsoft.Json with non-null arguments."
)]
public sealed class UtcDateTimeOffsetConverter : JsonConverter
{
	/// <inheritdoc/>
	public override bool CanConvert(Type objectType) =>
		objectType == typeof(DateTimeOffset) || objectType == typeof(DateTimeOffset?);

	/// <inheritdoc/>
	public override object? ReadJson(
		JsonReader reader,
		Type objectType,
		object? existingValue,
		JsonSerializer serializer
	)
	{
		if (reader.TokenType == JsonToken.Null)
			return null;

		var text = ReadRawString(reader);
		if (!HasUtcSuffix(text))
		{
			throw new JsonSerializationException(
				"DateTimeOffset values must carry an explicit UTC designator ('Z' or a zero offset) under the "
					+ "ZodSharp UTC contract."
			);
		}

		var value = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

		return value.Offset == TimeSpan.Zero
			? value
			: throw new JsonSerializationException(
				$"DateTimeOffset values must be UTC under the ZodSharp UTC contract, but got offset "
					+ $"'{value.Offset}'."
			);
	}

	/// <inheritdoc/>
	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		writer.WriteValue(((DateTimeOffset)value).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
	}

	static bool HasUtcSuffix(string text) =>
		text.EndsWith('Z')
		|| text.EndsWith("+00:00", StringComparison.Ordinal)
		|| text.EndsWith("-00:00", StringComparison.Ordinal);

	internal static string ReadRawString(JsonReader reader) =>
		reader.Value as string
		?? throw new JsonSerializationException(
			"DateTimeOffset values must be read with JsonSerializerSettings.DateParseHandling set to "
				+ "DateParseHandling.None so the offset can be inspected; Newtonsoft.Json parsed the value "
				+ "before the converter could see it."
		);
}

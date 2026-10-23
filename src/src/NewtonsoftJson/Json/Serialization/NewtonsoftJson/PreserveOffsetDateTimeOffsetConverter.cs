using System.Globalization;
using Newtonsoft.Json;

namespace ZodSharp.Json.Serialization.NewtonsoftJson;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> with its original offset.
/// </summary>
/// <remarks>
/// <para>
/// Newtonsoft.Json already preserves the offset by default. This converter exists so a type or property
/// that overrides a project-wide <see cref="UtcDateTimeOffsetConverter"/> with
/// <c>[DateTimeOffsetBehavior(DateTimeOffsetSerialization.PreserveOffset)]</c> can also override the
/// serializer converter explicitly, for example with <c>[JsonConverter]</c>.
/// </para>
/// <para>
/// Like the UTC converter, this one needs <c>DateParseHandling = DateParseHandling.None</c> so it can read
/// the original string and preserve its offset.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1062:Validate arguments of public methods",
	Justification = "Called by Newtonsoft.Json with non-null arguments."
)]
public sealed class PreserveOffsetDateTimeOffsetConverter : JsonConverter
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

		// Read the raw string and parse it with DateTimeOffset.Parse to preserve the offset.
		return DateTimeOffset.Parse(
			UtcDateTimeOffsetConverter.ReadRawString(reader),
			CultureInfo.InvariantCulture,
			DateTimeStyles.RoundtripKind
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

		writer.WriteValue(((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture));
	}
}

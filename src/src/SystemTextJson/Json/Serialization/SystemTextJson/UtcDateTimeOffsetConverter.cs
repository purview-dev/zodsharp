using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZodSharp.Json.Serialization.SystemTextJson;

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
/// Writing normalizes the instant to UTC and emits <c>+00:00</c>. The converter adds no reflection and is
/// safe under trimming and Native AOT.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1062:Validate arguments of public methods",
	Justification = "Called by System.Text.Json with non-null arguments."
)]
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
	/// <inheritdoc/>
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var text =
			reader.GetString()
			?? throw new JsonException("DateTimeOffset values must be a JSON string under the ZodSharp UTC contract.");

		if (!HasUtcSuffix(text))
		{
			throw new JsonException(
				"DateTimeOffset values must carry an explicit UTC designator ('Z' or a zero offset) under the "
					+ "ZodSharp UTC contract."
			);
		}

		var value = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		return value.Offset == TimeSpan.Zero
			? value
			: throw new JsonException(
				$"DateTimeOffset values must be UTC under the ZodSharp UTC contract, but got offset "
					+ $"'{value.Offset}'."
			);
	}

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value.ToUniversalTime());

	static bool HasUtcSuffix(string text) =>
		text.EndsWith('Z')
		|| text.EndsWith("+00:00", StringComparison.Ordinal)
		|| text.EndsWith("-00:00", StringComparison.Ordinal);
}

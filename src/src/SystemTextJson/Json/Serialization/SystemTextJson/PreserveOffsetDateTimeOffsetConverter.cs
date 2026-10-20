using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZodSharp.Json.Serialization.SystemTextJson;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> with its original offset.
/// </summary>
/// <remarks>
/// System.Text.Json already preserves the offset by default. This converter exists so a type or property
/// that overrides a project-wide <see cref="UtcDateTimeOffsetConverter"/> with
/// <c>[DateTimeOffsetBehavior(DateTimeOffsetSerialization.PreserveOffset)]</c> can also override the
/// serializer converter explicitly, for example with <c>[JsonConverter]</c>.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1062:Validate arguments of public methods",
	Justification = "Called by System.Text.Json with non-null arguments."
)]
public sealed class PreserveOffsetDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
	/// <inheritdoc/>
	public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		reader.GetDateTimeOffset();

	/// <inheritdoc/>
	public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
		writer.WriteStringValue(value);
}

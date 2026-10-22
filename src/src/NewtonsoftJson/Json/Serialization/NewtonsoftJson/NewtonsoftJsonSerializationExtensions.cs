using Newtonsoft.Json;

namespace ZodSharp.Json.Serialization.NewtonsoftJson;

/// <summary>
/// Configuration helpers for the DateTimeOffset converters.
/// </summary>
/// <remarks>
/// Newtonsoft.Json parses date-like strings into <see cref="DateTime"/> by default, which discards the
/// offset before a converter sees it. These helpers set <see cref="JsonSerializerSettings.DateParseHandling"/>
/// to <see cref="DateParseHandling.None"/> together with the converter, so the converter can inspect and
/// enforce the original offset.
/// </remarks>
public static class NewtonsoftJsonSerializationExtensions
{
	/// <summary>
	/// Configures the serializer for the UTC DateTimeOffset contract.
	/// </summary>
	/// <param name="settings">The settings to update.</param>
	/// <returns>The same settings instance, for chaining.</returns>
	public static JsonSerializerSettings UseZodSharpUtcDateTimeOffset(this JsonSerializerSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		settings.DateParseHandling = DateParseHandling.None;
		settings.Converters.Add(new UtcDateTimeOffsetConverter());
		return settings;
	}

	/// <summary>
	/// Configures the serializer to preserve DateTimeOffset offsets, including as a per-type/property
	/// override of a global UTC converter.
	/// </summary>
	/// <param name="settings">The settings to update.</param>
	/// <returns>The same settings instance, for chaining.</returns>
	public static JsonSerializerSettings UseZodSharpPreserveOffsetDateTimeOffset(this JsonSerializerSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		settings.DateParseHandling = DateParseHandling.None;
		settings.Converters.Add(new PreserveOffsetDateTimeOffsetConverter());
		return settings;
	}
}

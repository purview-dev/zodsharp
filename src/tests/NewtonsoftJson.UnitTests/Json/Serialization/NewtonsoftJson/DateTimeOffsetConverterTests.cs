using Newtonsoft.Json;

namespace ZodSharp.Json.Serialization.NewtonsoftJson;

public class DateTimeOffsetConverterTests
{
	static readonly DateTimeOffset OffsetValue = new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));

	static JsonSerializerSettings UtcSettings() =>
		new() { DateParseHandling = DateParseHandling.None, Converters = { new UtcDateTimeOffsetConverter() } };

	static JsonSerializerSettings PreserveSettings() =>
		new()
		{
			DateParseHandling = DateParseHandling.None,
			Converters = { new PreserveOffsetDateTimeOffsetConverter() },
		};

	[Test]
	public async Task UtcConverter_GivenNonZeroOffset_WritesUtc()
	{
		// Act
		var json = JsonConvert.SerializeObject(OffsetValue, UtcSettings());

		// Assert
		await Assert.That(json).IsEqualTo("\"2024-01-01T21:34:05.0000000+00:00\"");
	}

	[Test]
	public async Task UtcConverter_GivenUtcInput_ReadsWithZeroOffset()
	{
		// Act
		var value = JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05Z\"", UtcSettings());

		// Assert
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.Zero);
		await Assert.That(value).IsEqualTo(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
	}

	[Test]
	public async Task UtcConverter_GivenNonUtcInput_Throws()
	{
		// Act
		var exception = Assert.Throws<JsonSerializationException>(() =>
			JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05+05:30\"", UtcSettings())
		);

		// Assert
		await Assert.That(exception.Message).Contains("UTC");
	}

	[Test]
	public async Task UtcConverter_GivenNoOffsetDesignator_Throws()
	{
		// Act — an offset of zero is not enough: the JSON Schema requires an explicit Z/zero-offset suffix.
		var exception = Assert.Throws<JsonSerializationException>(() =>
			JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05\"", UtcSettings())
		);

		// Assert
		await Assert.That(exception.Message).Contains("UTC designator");
	}

	[Test]
	public async Task UtcConverter_GivenDateParseHandlingNotDisabled_ReportsTheRequiredSetting()
	{
		// Arrange — without DateParseHandling.None, Newtonsoft parses the offset away before the converter.
		JsonSerializerSettings settings = new() { Converters = { new UtcDateTimeOffsetConverter() } };

		// Act
		var exception = Assert.Throws<JsonSerializationException>(() =>
			JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05+05:30\"", settings)
		);

		// Assert
		await Assert.That(exception.Message).Contains("DateParseHandling");
	}

	[Test]
	public async Task UtcConverter_GivenNullableNull_ReturnsNull()
	{
		// Act
		var value = JsonConvert.DeserializeObject<DateTimeOffset?>("null", UtcSettings());

		// Assert
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task SettingsHelper_GivenUtcContract_ConfiguresDateParseHandlingAndConverter()
	{
		// Act
		var settings = new JsonSerializerSettings().UseZodSharpUtcDateTimeOffset();
		var value = JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05Z\"", settings);

		// Assert
		await Assert.That(settings.DateParseHandling).IsEqualTo(DateParseHandling.None);
		await Assert.That(settings.Converters.Any(converter => converter is UtcDateTimeOffsetConverter)).IsTrue();
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.Zero);
	}

	[Test]
	public async Task SettingsHelper_GivenUtcContract_LeavesDateTimeDeserializationWorking()
	{
		// Act — DateParseHandling.None must not break a normal DateTime property.
		var settings = new JsonSerializerSettings().UseZodSharpUtcDateTimeOffset();
		var value = JsonConvert.DeserializeObject<DateTime>("\"2024-01-02T03:04:05Z\"", settings);

		// Assert
		await Assert.That(value).IsEqualTo(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
	}

	[Test]
	public async Task SettingsHelper_GivenPreserveOffsetContract_ConfiguresDateParseHandlingAndConverter()
	{
		// Act
		var settings = new JsonSerializerSettings().UseZodSharpPreserveOffsetDateTimeOffset();
		var value = JsonConvert.DeserializeObject<DateTimeOffset>("\"2024-01-02T03:04:05+05:30\"", settings);

		// Assert
		await Assert.That(settings.DateParseHandling).IsEqualTo(DateParseHandling.None);
		await Assert
			.That(settings.Converters.Any(converter => converter is PreserveOffsetDateTimeOffsetConverter))
			.IsTrue();
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.FromHours(5.5));
	}

	[Test]
	public async Task PreserveOffsetConverter_GivenNonZeroOffset_RoundTripsTheOffset()
	{
		// Act
		var json = JsonConvert.SerializeObject(OffsetValue, PreserveSettings());
		var value = JsonConvert.DeserializeObject<DateTimeOffset>(json, PreserveSettings());

		// Assert
		await Assert.That(json).IsEqualTo("\"2024-01-02T03:04:05.0000000+05:30\"");
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.FromHours(5.5));
		await Assert.That(value).IsEqualTo(OffsetValue);
	}
}

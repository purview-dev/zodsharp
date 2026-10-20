using System.Text.Json;

namespace ZodSharp.Json.Serialization.SystemTextJson;

public class DateTimeOffsetConverterTests
{
	static readonly DateTimeOffset OffsetValue = new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));

	static JsonSerializerOptions UtcOptions() => new() { Converters = { new UtcDateTimeOffsetConverter() } };

	static JsonSerializerOptions PreserveOptions() =>
		new() { Converters = { new PreserveOffsetDateTimeOffsetConverter() } };

	[Test]
	public async Task UtcConverter_GivenNonZeroOffset_WritesUtc()
	{
		// Act
		var json = JsonSerializer.Serialize(OffsetValue, UtcOptions());

		// Assert
		await Assert.That(json).IsEqualTo("\"2024-01-01T21:34:05+00:00\"");
	}

	[Test]
	public async Task UtcConverter_GivenUtcInput_ReadsWithZeroOffset()
	{
		// Act
		var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2024-01-02T03:04:05Z\"", UtcOptions());

		// Assert
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.Zero);
		await Assert.That(value).IsEqualTo(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
	}

	[Test]
	public async Task UtcConverter_GivenNonUtcInput_Throws()
	{
		// Act
		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<DateTimeOffset>("\"2024-01-02T03:04:05+05:30\"", UtcOptions())
		);

		// Assert
		await Assert.That(exception.Message).Contains("UTC");
	}

	[Test]
	public async Task UtcConverter_GivenNoOffsetDesignator_Throws()
	{
		// Act — an offset of zero is not enough: the JSON Schema requires an explicit Z/zero-offset suffix.
		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<DateTimeOffset>("\"2024-01-02T03:04:05\"", UtcOptions())
		);

		// Assert
		await Assert.That(exception.Message).Contains("UTC designator");
	}

	[Test]
	public async Task UtcConverter_GivenNullableNull_ReturnsNull()
	{
		// Act
		var value = JsonSerializer.Deserialize<DateTimeOffset?>("null", UtcOptions());

		// Assert
		await Assert.That(value).IsNull();
	}

	[Test]
	public async Task PreserveOffsetConverter_GivenNonZeroOffset_RoundTripsTheOffset()
	{
		// Act
		var json = JsonSerializer.Serialize(OffsetValue, PreserveOptions());
		var value = JsonSerializer.Deserialize<DateTimeOffset>(json, PreserveOptions());

		// Assert
		await Assert.That(json).IsEqualTo("\"2024-01-02T03:04:05+05:30\"");
		await Assert.That(value.Offset).IsEqualTo(TimeSpan.FromHours(5.5));
		await Assert.That(value).IsEqualTo(OffsetValue);
	}
}

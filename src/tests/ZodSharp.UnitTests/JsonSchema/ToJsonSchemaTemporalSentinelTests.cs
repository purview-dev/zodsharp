using System.Text.RegularExpressions;
using ZodSharp.Rules;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Coverage for the temporal JSON Schema mappings contributed by <see cref="NonSentinelRule{T}"/>.
/// </summary>
/// <remarks>
/// The DateTimeOffset case is deliberately split in two: preserving offsets cannot express the instant-based
/// bounds, so the rule reports itself as unsupported and only rejects the canonical zero-offset forms; a UTC
/// contract is exact because every accepted string denotes one instant.
/// </remarks>
public class ToJsonSchemaTemporalSentinelTests
{
	[Test]
	public async Task ApplyTo_GivenDateOnly_ExportsPatternThatRejectsBothBounds(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string", Format = "date" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<DateOnly>());

		// Assert
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateOnly>.DateOnlyJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-02")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "9999-12-30")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02")).IsTrue();
	}

	[Test]
	public async Task ApplyTo_GivenTimeOnly_ExportsPatternThatRejectsBothBounds(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string", Format = "time" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<TimeOnly>());

		// Assert — System.Text.Json writes "00:00:00" and "23:59:59.9999999".
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<TimeOnly>.TimeOnlyJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
		await Assert.That(Matches(schema.Pattern!, "00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "00:00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "00:00:00.000")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "23:59:59.9999999")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "00:00:00.0000001")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "23:59:59.999999")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "12:34:56")).IsTrue();
	}

	[Test]
	public async Task TimeOnlyPattern_GivenLocalWireForm_AcceptsValuesAndRejectsOffsets(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act & Assert — System.Text.Json never writes an offset for TimeOnly.
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "00:00:00")).IsTrue();
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "12:34:56")).IsTrue();
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "23:59:59.9999999")).IsTrue();
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "12:34:56Z")).IsFalse();
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "24:00:00")).IsFalse();
		await Assert.That(Matches(JsonSchemaRules.TimeOnlyPattern, "12:60:00")).IsFalse();
	}

	[Test]
	public async Task ApplyTo_GivenDateTime_ExportsPatternThatRejectsDeterministicBounds(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string", Format = "date-time" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<DateTime>());

		// Assert — the no-offset (Unspecified) and Z/zero-offset (Utc) wire forms are the deterministic ones.
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTime>.DateTimeJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00Z")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00.000+00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T23:59:59.9999999")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T23:59:59.9999999Z")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00.0000001Z")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02T03:04:05Z")).IsTrue();
	}

	[Test]
	public async Task DateTimePattern_GivenNonZeroOffset_LeavesTheReceiverShiftedValueUnconstrained(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — System.Text.Json converts an offset-bearing DateTime into the receiver's local time
		// zone, so the string no longer denotes the sentinel ticks.
		var pattern = NonSentinelRule<DateTime>.DateTimeJsonSchemaPattern;

		// Assert
		await Assert.That(Matches(pattern, "0001-01-01T00:00:00-05:30")).IsTrue();
		await Assert.That(Matches(pattern, "9999-12-31T23:59:59.9999999+05:30")).IsTrue();
		await Assert.That(Matches(pattern, "0001-01-01T00:00:00Z")).IsFalse();
	}

	[Test]
	public async Task ApplyTo_GivenDateTimeOffsetWithPreservedOffsets_RejectsEveryOffsetEquivalentBound(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string", Format = "date-time" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<DateTimeOffset>());

		// Assert — the default mapping is exact and carries no unsupported marker.
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00+00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T23:59:59.9999999Z")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T05:30:00+05:30")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T14:00:00+14:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T18:29:59.9999999-05:30")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T09:59:59.9999999-14:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T05:30:00.0000001+05:30")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T18:29:59.9999998-05:30")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02T03:04:05+05:30")).IsTrue();
	}

	[Test]
	public async Task DateTimeOffsetPattern_GivenEveryOffset_RejectsEachBoundAndAcceptsTheAdjacentTick(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		var pattern = NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern;
		var rejectedBounds = 0;
		var acceptedAdjacentValues = 0;

		// Act
		for (var offsetHour = 0; offsetHour <= 14; offsetHour++)
		{
			for (var offsetMinute = 0; offsetMinute <= 59; offsetMinute++)
			{
				var minOffset = $"+{offsetHour:00}:{offsetMinute:00}";
				var minBound = $"0001-01-01T{offsetHour:00}:{offsetMinute:00}:00{minOffset}";
				var minAdjacent = $"0001-01-01T{offsetHour:00}:{offsetMinute:00}:00.0000001{minOffset}";
				if (!Matches(pattern, minBound))
					rejectedBounds++;
				if (Matches(pattern, minAdjacent))
					acceptedAdjacentValues++;

				var localHour = 23 - offsetHour;
				var localMinute = 59 - offsetMinute;
				var maxOffset = offsetHour == 0 && offsetMinute == 0 ? "+00:00" : $"-{offsetHour:00}:{offsetMinute:00}";
				var maxBound = $"9999-12-31T{localHour:00}:{localMinute:00}:59.9999999{maxOffset}";
				var maxAdjacent = $"9999-12-31T{localHour:00}:{localMinute:00}:59.9999998{maxOffset}";
				if (!Matches(pattern, maxBound))
					rejectedBounds++;
				if (Matches(pattern, maxAdjacent))
					acceptedAdjacentValues++;
			}
		}

		// Assert
		await Assert.That(rejectedBounds).IsEqualTo(1800);
		await Assert.That(acceptedAdjacentValues).IsEqualTo(1800);
	}

	[Test]
	public async Task ApplyTo_GivenDateTimeOffsetWithUtcContract_IsExactAndHasNoMarker(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "string", Format = "date-time" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<DateTimeOffset>(), DateTimeOffsetSerialization.Utc);

		// Assert — every accepted string has a zero offset, so both offset-equivalent bounds are rejected.
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00Z")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T00:00:00+00:00")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "0001-01-01T05:30:00+05:30")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T23:59:59.9999999Z")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "9999-12-31T18:29:59.9999999-05:30")).IsFalse();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02T03:04:05Z")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02T03:04:05.123+00:00")).IsTrue();
		await Assert.That(Matches(schema.Pattern!, "2024-01-02T03:04:05+05:30")).IsFalse();
	}

	[Test]
	public async Task ApplyTo_GivenTypeWithoutKnownSentinel_AddsNoConstraintAndNoMarker(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		JsonSchemaDefinition schema = new() { Type = "integer" };

		// Act
		JsonSchemaRules.Apply(schema, new NonSentinelRule<int>());

		// Assert — IsValid always passes for an unknown type, so an unconstrained schema is complete.
		await Assert.That(schema.Pattern).IsNull();
		await Assert.That(schema.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task DateTimeOffsetSchema_GivenSentinelAndOrdinaryValues_ValidatesByInstant(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		var schema = Z.DateTimeOffset().NonSentinel();

		// Act
		var min = schema.Validate(DateTimeOffset.MinValue);
		var offsetEquivalent = schema.Validate(new DateTimeOffset(1, 1, 1, 5, 30, 0, TimeSpan.FromHours(5.5)));
		var ordinary = schema.Validate(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5)));

		// Assert
		await Assert.That(min.IsSuccess).IsFalse();
		await Assert.That(offsetEquivalent.IsSuccess).IsFalse();
		await Assert.That(ordinary.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ToJsonSchema_GivenManualDateTimeOffsetWithUtcOption_UsesTheExactUtcPattern(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var schema = Z.ToJsonSchema(
			Z.DateTimeOffset().NonSentinel(),
			new ToJsonSchemaOptions { DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc }
		);

		// Assert
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task ToJsonSchema_GivenManualDateTimeOffsetWithDefaultOption_UsesTheExactOffsetAwarePattern(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var schema = Z.ToJsonSchema(Z.DateTimeOffset().NonSentinel());

		// Assert
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task ToJsonSchema_GivenFluentDateWithNonSentinel_ExportsDateTimeFormatAndPattern(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var schema = Z.ToJsonSchema(Z.Date().NonSentinel());

		// Assert
		await Assert.That(schema.Type).IsEqualTo("string");
		await Assert.That(schema.Format).IsEqualTo("date-time");
		await Assert.That(schema.Pattern).IsEqualTo(NonSentinelRule<DateTime>.DateTimeJsonSchemaPattern);
		await Assert.That(schema.UnsupportedRules).IsNull();
	}

	static bool Matches(string pattern, string value) => Regex.IsMatch(value, pattern);
}

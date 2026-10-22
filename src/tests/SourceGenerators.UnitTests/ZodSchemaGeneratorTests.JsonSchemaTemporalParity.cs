using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaGeneratorTests
{
	const string TemporalParityOutputRelativePath = "src/tests/cross-platform/temporal-parity";

	/// <summary>
	/// The default PreserveOffset model. The exported DateTimeOffset pattern is offset-aware, so the schema
	/// is exact and carries no <c>x-zodsharp-unsupported</c> marker.
	/// </summary>
	const string TemporalParitySource = """
		using System;
		using ZodSharp.Rules;

		namespace Testing
		{
		    [ZodSchema]
		    public sealed class TemporalHolder
		    {
		        [NonSentinel]
		        public DateTimeOffset ObservedAt { get; set; }

		        [NonSentinel]
		        public DateTime Timestamp { get; set; }

		        [NonSentinel]
		        public DateOnly Day { get; set; }

		        [NonSentinel]
		        public TimeOnly Time { get; set; }

		        [NonSentinel]
		        public DateTimeOffset? Optional { get; set; }
		    }
		}
		""";

	[Test]
	public async Task JsonSchema_GivenGeneratedTemporalSchemas_WritesZodParityArtifacts(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the default PreserveOffset cases. The offset-aware pattern is exact, so the
		// offset-equivalent bounds are rejected without a UTC converter or an unsupported marker.
		using var driverResult = await GenerateAsync(
			TemporalParitySource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var outputDir = Path.Combine("..", "..", "..", "..", "..", "..", TemporalParityOutputRelativePath);

		// Act & Assert
		await WriteParityArtifactAsync(
			assembly,
			"TemporalHolder",
			[
				(
					"valid-utc",
					TemporalPayload(
						"2024-01-02T03:04:05Z",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						"2024-01-03T00:00:00+00:00"
					),
					true
				),
				(
					"valid-offset",
					TemporalPayload(
						"2024-01-02T03:04:05+05:30",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56.1234567",
						null
					),
					true
				),
				(
					"min-utc",
					TemporalPayload(
						"0001-01-01T00:00:00+00:00",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						null
					),
					false
				),
				(
					"max-utc",
					TemporalPayload(
						"9999-12-31T23:59:59.9999999Z",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						null
					),
					false
				),
				(
					"min-offset-equivalent",
					TemporalPayload(
						"0001-01-01T05:30:00+05:30",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						null
					),
					false
				),
				(
					"max-offset-equivalent",
					TemporalPayload(
						"9999-12-31T18:29:59.9999999-05:30",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						null
					),
					false
				),
				(
					"nullable-min",
					TemporalPayload(
						"2024-01-02T03:04:05Z",
						"2024-01-02T03:04:05Z",
						"2024-01-02",
						"12:34:56",
						"0001-01-01T00:00:00Z"
					),
					false
				),
				(
					"dateonly-min",
					TemporalPayload("2024-01-02T03:04:05Z", "2024-01-02T03:04:05Z", "0001-01-01", "12:34:56", null),
					false
				),
				(
					"datetime-min",
					TemporalPayload("2024-01-02T03:04:05Z", "0001-01-01T00:00:00Z", "2024-01-02", "12:34:56", null),
					false
				),
				(
					"timeonly-min",
					TemporalPayload("2024-01-02T03:04:05Z", "2024-01-02T03:04:05Z", "2024-01-02", "00:00:00", null),
					false
				),
			],
			outputDir,
			cancellationToken
		);
	}

	static string TemporalPayload(string observedAt, string timestamp, string day, string time, string? optional) =>
		$$"""
			{
				"observedAt": "{{observedAt}}",
				"timestamp": "{{timestamp}}",
				"day": "{{day}}",
				"time": "{{time}}",
				"optional": {{(optional is null ? "null" : $"\"{optional}\"")}}
			}
			""";
}

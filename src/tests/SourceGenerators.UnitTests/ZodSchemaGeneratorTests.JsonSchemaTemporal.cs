using System.Collections.Immutable;
using ZodSharp.JsonSchema;
using ZodSharp.Rules;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaGeneratorTests
{
	const string TemporalHolderSource = """
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
	public async Task JsonSchema_GivenNonSentinelTemporalProperties_ExportsExactPatterns(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var definition = await BuildTemporalDefinitionAsync(
			TemporalHolderSource,
			"TemporalHolderSchema",
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert — the deterministic types are exact and carry no marker.
		var timestamp = definition.Properties!["timestamp"];
		await Assert.That(timestamp.Format).IsEqualTo("date-time");
		await Assert.That(timestamp.Pattern).IsEqualTo(NonSentinelRule<DateTime>.DateTimeJsonSchemaPattern);
		await Assert.That(timestamp.UnsupportedRules).IsNull();

		var day = definition.Properties!["day"];
		await Assert.That(day.Format).IsEqualTo("date");
		await Assert.That(day.Pattern).IsEqualTo(NonSentinelRule<DateOnly>.DateOnlyJsonSchemaPattern);
		await Assert.That(day.UnsupportedRules).IsNull();

		var time = definition.Properties!["time"];
		await Assert.That(time.Format).IsNull();
		await Assert.That(time.Pattern).IsNull();
		await Assert.That(time.AllOf!).Count().IsEqualTo(2);
		await Assert.That(time.AllOf!.Select(branch => branch.Pattern)).Contains(JsonSchemaRules.TimeOnlyPattern);
		await Assert
			.That(time.AllOf!.Select(branch => branch.Pattern))
			.Contains(NonSentinelRule<TimeOnly>.TimeOnlyJsonSchemaPattern);
		await Assert.That(time.UnsupportedRules).IsNull();

		// Assert — the default PreserveOffset mapping is offset-aware and exact.
		var observedAt = definition.Properties!["observedAt"];
		await Assert.That(observedAt.Format).IsEqualTo("date-time");
		await Assert
			.That(observedAt.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(observedAt.UnsupportedRules).IsNull();

		// Assert — the nullable DateTimeOffset keeps the null branch.
		await Assert.That(definition.Required!).DoesNotContain("optional");
		var optional = definition.Properties!["optional"].AnyOf!;
		await Assert.That(optional.Any(candidate => candidate.Type == "null")).IsTrue();
		var optionalText = optional.Single(candidate => candidate.Type == "string");
		await Assert.That(optionalText.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenMsBuildUtc_ExportsTheExactDateTimeOffsetPatternWithoutAMarker(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the Directory.Build.props shape.
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpDateTimeOffsetSerialization",
				"Utc"
			),
		};

		// Act
		var definition = await BuildTemporalDefinitionAsync(
			TemporalHolderSource,
			"TemporalHolderSchema",
			options,
			cancellationToken
		);

		// Assert
		var observedAt = definition.Properties!["observedAt"];
		await Assert
			.That(observedAt.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(observedAt.UnsupportedRules).IsNull();

		var optional = definition.Properties!["optional"].AnyOf!.Single(candidate => candidate.Type == "string");
		await Assert
			.That(optional.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(optional.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenAssemblyUtc_OverridesTheMsBuildDefault(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the MSBuild default is PreserveOffset; the assembly opts this compilation into Utc.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			[assembly: ZodSharpSchemaDefaults(
			    DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc)]

			namespace Testing
			{
			    [ZodSchema]
			    public sealed class Stamp
			    {
			        [NonSentinel]
			        public DateTimeOffset At { get; set; }
			    }
			}
			""";

		// Act
		var definition = await BuildTemporalDefinitionAsync(
			source,
			"StampSchema",
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var at = definition.Properties!["at"];
		await Assert.That(at.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(at.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenAssemblyPreserveOffset_OverridesTheMsBuildUtcDefault(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			[assembly: ZodSharpSchemaDefaults(
			    DateTimeOffsetSerialization = DateTimeOffsetSerialization.PreserveOffset)]

			namespace Testing
			{
			    [ZodSchema]
			    public sealed class Stamp
			    {
			        [NonSentinel]
			        public DateTimeOffset At { get; set; }
			    }
			}
			""";
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpDateTimeOffsetSerialization",
				"Utc"
			),
		};

		// Act
		var definition = await BuildTemporalDefinitionAsync(source, "StampSchema", options, cancellationToken);

		// Assert
		var at = definition.Properties!["at"];
		await Assert.That(at.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(at.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenTypeAndPropertyOverrides_UsesTheMostSpecificSetting(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the project is Utc; the type keeps Utc for ObservedAt but opts Optional back out.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
			    [ZodSchema]
			    [DateTimeOffsetBehavior(DateTimeOffsetSerialization.Utc)]
			    public sealed class Stamp
			    {
			        [NonSentinel]
			        public DateTimeOffset ObservedAt { get; set; }

			        [NonSentinel]
			        [DateTimeOffsetBehavior(DateTimeOffsetSerialization.PreserveOffset)]
			        public DateTimeOffset? Optional { get; set; }
			    }
			}
			""";
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpDateTimeOffsetSerialization",
				"Utc"
			),
		};

		// Act
		var definition = await BuildTemporalDefinitionAsync(source, "StampSchema", options, cancellationToken);

		// Assert — the type-level Utc wins for ObservedAt even though the MSBuild default is also Utc.
		var observedAt = definition.Properties!["observedAt"];
		await Assert
			.That(observedAt.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(observedAt.UnsupportedRules).IsNull();

		// Assert — the property-level PreserveOffset wins for Optional.
		var optionalText = definition.Properties!["optional"].AnyOf!.Single(candidate => candidate.Type == "string");
		await Assert
			.That(optionalText.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(optionalText.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenPositionalParameterBehavior_MergesTheOverrideOntoTheProperty(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — the project is Utc; the positional parameter opts this property back out.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
			    [ZodSchema]
			    public sealed record Stamp(
			        [NonSentinel]
			        [DateTimeOffsetBehavior(DateTimeOffsetSerialization.PreserveOffset)]
			        DateTimeOffset At);
			}
			""";
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpDateTimeOffsetSerialization",
				"Utc"
			),
		};

		// Act
		var definition = await BuildTemporalDefinitionAsync(source, "StampSchema", options, cancellationToken);

		// Assert
		var at = definition.Properties!["at"];
		await Assert.That(at.Pattern).IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetJsonSchemaPattern);
		await Assert.That(at.UnsupportedRules).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenUtcScalarWithNonSentinel_ExportsTheExactPatternWithoutAMarker(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
				    [Purview.ValueObjects.Serialization.Scalar]
				    [ZodSchema]
				    [DateTimeOffsetBehavior(DateTimeOffsetSerialization.Utc)]
				    [NonSentinel]
				    public partial record struct ObservedAt
				        : Purview.ValueObjects.IScalarValueObject<ObservedAt, DateTimeOffset>
				    {
				        public DateTimeOffset Value { get; init; }
				    }
				}
				""";

		// Act
		var definition = await BuildTemporalDefinitionAsync(
			source,
			"ObservedAtSchema",
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		await Assert.That(definition.Type).IsEqualTo("string");
		await Assert.That(definition.Format).IsEqualTo("date-time");
		await Assert
			.That(definition.Pattern)
			.IsEqualTo(NonSentinelRule<DateTimeOffset>.DateTimeOffsetUtcJsonSchemaPattern);
		await Assert.That(definition.UnsupportedRules).IsNull();
	}

	async Task<JsonSchemaDefinition> BuildTemporalDefinitionAsync(
		string source,
		string schemaTypeName,
		ZodSourceGeneratorTestOptions options,
		CancellationToken cancellationToken
	)
	{
		using var driverResult = await GenerateAsync(source, options, cancellationToken);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		return (JsonSchemaDefinition)
			assembly.GetType($"Testing.{schemaTypeName}")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;
	}
}

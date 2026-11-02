using System.Collections.Immutable;
using ZodSharp.SourceGenerators.Infra;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Proves the [ZodSchema] pipeline caches stage by stage: an identical rerun and an unrelated file are
/// cached, and adding a [JsonPropertyName] (which feeds the generated JSON-name table) re-runs only the type
/// that changed.
/// </summary>
public class ZodSchemaGeneratorCacheTests : ZodSharpSourceGeneratorTestBase<ZodSchemaGenerator>
{
	const string CustomerSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing;

		[ZodSchema]
		public class Customer
		{
			[Required]
			public string? Name { get; set; }
		}
		""";

	const string CustomerWithJsonNameSource = """
		using System.ComponentModel.DataAnnotations;
		using System.Text.Json.Serialization;
		using ZodSharp;

		namespace Testing;

		[ZodSchema]
		public class Customer
		{
			[Required]
			[JsonPropertyName("full_name")]
			public string? Name { get; set; }
		}
		""";

	const string OrderSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing;

		[ZodSchema]
		public class Order
		{
			[Range(1, 10)]
			public int Quantity { get; set; }
		}
		""";

	const string UnrelatedSource = """
		namespace Testing;

		public class Unrelated
		{
			public int Value { get; set; }
		}
		""";

	const string AttributeStage = "ForAttribute_ZodSchemaAttribute";

	static ImmutableDictionary<string, ImmutableArray<StepReason>> StepReasons(IncrementalCacheRun run)
	{
		var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<StepReason>>();
		foreach (var pair in run.Steps)
			builder[pair.Key] =
			[
				.. pair.Value.SelectMany(static step => step.Outputs.Select(static output => output.Reason)),
			];

		return builder.ToImmutable();
	}

	static bool IsCachedOrUnchanged(StepReason reason) => reason is StepReason.Cached or StepReason.Unchanged;

	[Test]
	public async Task IdenticalRerun_GivenSameSources_KeepsTheAttributeStageCached(CancellationToken cancellationToken)
	{
		// Act
		var result = await GenerateIncrementalAsync(
			[CustomerSource, OrderSource],
			cancellationToken: cancellationToken
		);

		// Assert
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second.TryGetValue(AttributeStage, out var reasons)).IsTrue();
		await Assert.That(reasons.All(IsCachedOrUnchanged)).IsTrue();
	}

	[Test]
	public async Task UnrelatedFile_GivenNewSource_KeepsPerTypeOutputsCached(CancellationToken cancellationToken)
	{
		// Arrange
		IncrementalRunInput[] inputs =
		[
			new([CustomerSource, OrderSource]),
			new([CustomerSource, OrderSource, UnrelatedSource]),
		];

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second[AttributeStage].All(IsCachedOrUnchanged)).IsTrue();
	}

	[Test]
	public async Task JsonPropertyName_GivenAddedToOneType_ReRunsOnlyThatType(CancellationToken cancellationToken)
	{
		// Arrange
		IncrementalRunInput[] inputs =
		[
			new([CustomerSource, OrderSource]),
			new([CustomerWithJsonNameSource, OrderSource]),
		];

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert — Customer's output is modified, Order's stays cached.
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second[AttributeStage]).Contains(StepReason.Modified);
		await Assert.That(second[AttributeStage].Count(IsCachedOrUnchanged)).IsEqualTo(1);
	}
}

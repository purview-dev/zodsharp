using System.Collections.Immutable;
using ZodSharp.AspNetCore.SourceGenerators.Infra;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace ZodSharp.AspNetCore.SourceGenerators;

public class ErrorTypeGeneratorCacheTests : ErrorTypeGeneratorTestBase
{
	const string Source = """
		namespace Testing;

		public static partial class ConcurrentErrorType
		{
			[ErrorType]
			public static readonly ErrorType SaveFailed = new(
				Code: "aggregate_save_failed",
				HttpStatus: 409)
			{
				Parameters = ["OrderId", "AggregateType"]
			};
		}
		""";

	const string ChangedSource = """
		namespace Testing;

		public static partial class ConcurrentErrorType
		{
			[ErrorType]
			public static readonly ErrorType SaveFailed = new(
				Code: "aggregate_save_failed",
				HttpStatus: 409)
			{
				Parameters = ["OrderId"]
			};
		}
		""";

	static readonly string[] FrameworkStages =
	[
		"GetGenerationContext_ErrorTypeGeneratorCapabilities",
		"GetGenerationConfiguration",
		"ForAttribute_ErrorTypeAttribute",
		"CollectErrorTypeFields",
	];

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
	public async Task IdenticalRerun_AllStagesCached(CancellationToken cancellationToken)
	{
		// Arrange/Act
		var result = await GenerateIncrementalAsync([Source], cancellationToken: cancellationToken);

		// Assert
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second).IsNotEmpty();
		await Assert
			.That(
				FrameworkStages.All(stage =>
					second.TryGetValue(stage, out var reasons) && reasons.All(IsCachedOrUnchanged)
				)
			)
			.IsTrue();
	}

	[Test]
	public async Task ChangedParameters_MarksAttributeStageModified_PropertyStagesStayCached(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		IncrementalRunInput[] inputs = [new([Source]), new([ChangedSource])];

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second["ForAttribute_ErrorTypeAttribute"]).Contains(StepReason.Modified);
		await Assert
			.That(second["GetGenerationContext_ErrorTypeGeneratorCapabilities"].All(IsCachedOrUnchanged))
			.IsTrue();
	}

	[Test]
	public async Task PropertyChange_MarksPropertyStageModified_AttributeStageStaysCached(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		IncrementalRunInput[] inputs =
		[
			new([Source]),
			new([Source], [("build_property.DisableAspNetCoreErrorTypeGenerator", "true")]),
		];

		// Act
		var result = await GenerateIncrementalAsync(inputs, cancellationToken: cancellationToken);

		// Assert
		var second = StepReasons(result.Runs[1]);
		await Assert.That(second["GetGenerationContext_ErrorTypeGeneratorCapabilities"]).Contains(StepReason.Modified);
		await Assert.That(second["ForAttribute_ErrorTypeAttribute"].All(IsCachedOrUnchanged)).IsTrue();
	}
}

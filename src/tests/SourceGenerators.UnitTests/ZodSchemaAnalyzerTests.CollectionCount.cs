using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	[Arguments("[MinCount(1)]")]
	[Arguments("[MaxCount(1)]")]
	[Arguments("[CountRange(1, 2)]")]
	public async Task CollectionCount_GivenNonCollectionMember_ProducesZODSGEN030(
		string attribute,
		CancellationToken cancellationToken
	)
	{
		// Arrange — a count has no meaning on a scalar; the count rules validate IEnumerable.
		var source = $$"""
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public class Widget
				{
					{{attribute}}
					public int Value { get; set; }
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	[Test]
	public async Task CollectionCount_GivenCollectionMember_ProducesNoZODSGEN030(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public class Widget
				{
					[MinCount(1)]
					[MaxCount(3)]
					public List<int> Values { get; set; } = [];

					[CountRange(1, 2)]
					public Dictionary<string, int> Counts { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}
}

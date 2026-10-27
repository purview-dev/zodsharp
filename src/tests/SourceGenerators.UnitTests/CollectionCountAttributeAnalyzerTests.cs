using System.Collections.Immutable;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public class CollectionCountAttributeAnalyzerTests : CollectionCountAnalyzerTestBase
{
	static ZodAnalyzerTestOptions TrimmedOptions(string property = "PublishAot") =>
		new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add($"build_property.{property}", "true"),
		};

	[Test]
	[Arguments("PublishAot")]
	[Arguments("PublishTrimmed")]
	[Arguments("IsAotCompatible")]
	[Arguments("IsTrimmable")]
	public async Task Analyze_GivenMinLengthOnCollectionInTrimmedProject_ReportsZODSGEN053(
		string property,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[MinLength(1)]
					public List<string> Lines { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, TrimmedOptions(property), cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute);
	}

	[Test]
	public async Task Analyze_GivenEachSizeAttributeOnCollections_ReportsOnePerAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[MinLength(1)]
					[MaxLength(5)]
					public string[] Lines { get; set; } = [];

					[Length(1, 3)]
					public Dictionary<string, int> Counts { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostics(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute, 3);
	}

	[Test]
	public async Task Analyze_GivenPositionalRecordParameter_ReportsZODSGEN053(CancellationToken cancellationToken)
	{
		// Arrange — the attribute lands on the primary-constructor parameter.
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Order([MaxLength(5)] IReadOnlyList<string> Lines);
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute);
	}

	[Test]
	public async Task Analyze_GivenProjectThatIsNotTrimmed_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[MinLength(1)]
					public List<string> Lines { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, new ZodAnalyzerTestOptions(), cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute);
	}

	[Test]
	public async Task Analyze_GivenMinLengthOnString_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange — a string length is not a collection count, and [MinLength] on a string has no trim-safe twin.
		const string source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[MinLength(1)]
					public string Name { get; set; } = "";
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute);
	}

	[Test]
	public async Task Analyze_GivenTypeWithoutZodSchema_ReportsNothing(CancellationToken cancellationToken)
	{
		// Arrange — only the generator validates the count attributes, so they are suggested on [ZodSchema] types only.
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				public class Order
				{
					[MinLength(1)]
					public List<string> Lines { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UseTrimSafeCollectionCountAttribute);
	}
}

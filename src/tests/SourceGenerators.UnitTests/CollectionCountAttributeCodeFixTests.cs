using System.Collections.Immutable;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public class CollectionCountAttributeCodeFixTests : CollectionCountCodeFixTestBase
{
	static ZodCodeFixTestOptions TrimmedOptions() =>
		new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add("build_property.PublishAot", "true"),
		};

	[Test]
	[Arguments("MinLength(1)", "MinCount(1)")]
	[Arguments("MaxLength(5)", "MaxCount(5)")]
	[Arguments("Length(1, 3)", "CountRange(1, 3)")]
	[Arguments("MinLength(2, ErrorMessage = \"Too few\")", "MinCount(2, ErrorMessage = \"Too few\")")]
	public async Task CodeFix_GivenSizeAttributeOnCollection_ReplacesWithCountAttribute(
		string original,
		string replacement,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = $$"""
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[{{original}}]
					public List<string> Lines { get; set; } = [];
				}
			}
			""";

		// Act
		var result = await ApplyCodeFixAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result.FixedSource).Contains($"[{replacement}]");
		await Assert.That(result.FixedSource).Contains("using ZodSharp.Rules;");
		await Assert.That(result.FixedSource).DoesNotContain($"[{original}]");
	}

	[Test]
	public async Task CodeFix_GivenRulesNamespaceAlreadyImported_DoesNotDuplicateTheUsing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Rules;

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
		var result = await ApplyCodeFixAsync(source, TrimmedOptions(), cancellationToken);

		// Assert
		await Assert.That(result.FixedSource).Contains("[MinCount(1)]");
		await Assert.That(result.FixedSource.Split("using ZodSharp.Rules;").Length - 1).IsEqualTo(1);
	}
}

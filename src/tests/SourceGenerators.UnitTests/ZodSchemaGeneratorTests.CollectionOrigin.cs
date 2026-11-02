using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task CollectionSize_GivenTheDocumentedBasket_ReportsTheZodOriginOfEachCollectionShape(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the Basket example from Source-Generator-DataAnnotations.md, plus a map and a set.
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed class Basket
				{
					[Required]
					[Length(2, 5)]
					public List<string>? Items { get; set; }

					[MinLength(1)]
					public Dictionary<string, int> Prices { get; set; } = [];

					[MaxCount(1)]
					public HashSet<string> Tags { get; set; } = ["a", "b"];
				}
			}
			""";
		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var basket = Create(assembly, "Testing.Basket", ("Items", new List<string> { "apple" }));

		// Act
		var errors = RunValidate(assembly, "Testing.BasketSchema", basket)
			.ToDictionary(static error => error.FullPath.ToDottedString());

		// Assert
		await Assert.That(errors["Items"].Code).IsEqualTo("too_small");
		await Assert.That(errors["Items"].Minimum).IsEqualTo(2);
		await Assert.That(errors["Items"].Inclusive ?? false).IsTrue();
		await Assert.That(errors["Items"].Origin).IsEqualTo("array");
		await Assert.That(errors["Prices"].Origin).IsEqualTo("record");
		await Assert.That(errors["Tags"].Origin).IsEqualTo("array");
	}
}

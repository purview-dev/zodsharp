using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string DuplicateSimpleNamesSource = """
		using System.ComponentModel.DataAnnotations;

		namespace Alpha
		{
			[ZodSchema]
			public class Widget
			{
				[Required]
				public string? Name { get; set; }
			}

			public partial class Outer
			{
				[ZodSchema]
				public partial class Item
				{
					[Range(1, 5)]
					public int Size { get; set; }
				}
			}

			public partial class Other
			{
				[ZodSchema]
				public partial class Item
				{
					[MinLength(2)]
					public string Code { get; set; } = "";
				}
			}
		}

		namespace Beta
		{
			[ZodSchema]
			public class Widget
			{
				[Range(1, 5)]
				public int Size { get; set; }
			}
		}

		namespace Gamma
		{
			[ZodSchema]
			public class Box
			{
				public Alpha.Widget? First { get; set; }

				public Beta.Widget? Second { get; set; }
			}
		}
		""";

	[Test]
	public async Task DuplicateSimpleNames_GivenTypesInDifferentNamespaces_GeneratesEveryOutput(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			DuplicateSimpleNamesSource,
			ZodSourceGeneratorTestOptions.CompileWithDependencyInjection,
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		ZodSchemaFactory factory = new();

		// Act
		InvokeStatic<IZodSchemaFactory>(
			assembly,
			"ZodSharp.Generated.TestAssemblyZodSharpSchemas",
			"Register",
			factory
		);
		var alpha = RunValidate(assembly, "Alpha.WidgetSchema", Create(assembly, "Alpha.Widget"));
		var beta = RunValidate(assembly, "Beta.WidgetSchema", Create(assembly, "Beta.Widget"));
		var outerItem = RunValidate(assembly, "Alpha.Outer+ItemSchema", Create(assembly, "Alpha.Outer+Item"));
		var otherItem = RunValidate(assembly, "Alpha.Other+ItemSchema", Create(assembly, "Alpha.Other+Item"));

		// Assert
		await Assert.That(result.DriverResult.Diagnostics.Where(static d => d.Id == "ZODSGEN001")).IsEmpty();
		await Assert.That(factory.TryGetUntypedValidator(assembly.GetType("Alpha.Widget")!, out _)).IsTrue();
		await Assert.That(factory.TryGetUntypedValidator(assembly.GetType("Beta.Widget")!, out _)).IsTrue();
		await Assert.That(factory.TryGetUntypedValidator(assembly.GetType("Gamma.Box")!, out _)).IsTrue();
		await Assert.That(alpha.Single().FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(beta.Single().FullPath.ToDottedString()).IsEqualTo("Size");
		await Assert.That(outerItem.Single().FullPath.ToDottedString()).IsEqualTo("Size");
		await Assert.That(otherItem.Single().FullPath.ToDottedString()).IsEqualTo("Code");
	}

	[Test]
	public async Task DuplicateSimpleNames_GivenTheSameSource_UsesDeterministicDistinctHintNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var first = await GenerateAsync(DuplicateSimpleNamesSource, cancellationToken);
		using var second = await GenerateAsync(DuplicateSimpleNamesSource, cancellationToken);

		// Act
		string[] firstHints =
		[
			.. first.DriverResult.GeneratedTrees.Select(static tree => Path.GetFileName(tree.FilePath)),
		];
		string[] secondHints =
		[
			.. second.DriverResult.GeneratedTrees.Select(static tree => Path.GetFileName(tree.FilePath)),
		];

		// Assert
		await Assert.That(firstHints.Distinct(StringComparer.OrdinalIgnoreCase).Count()).IsEqualTo(firstHints.Length);
		await Assert.That(firstHints).IsEquivalentTo(secondHints, TUnit.Assertions.Enums.CollectionOrdering.Matching);
		await Assert.That(firstHints).Contains("Alpha.WidgetSchema.g.cs");
		await Assert.That(firstHints).Contains("Beta.WidgetSchema.g.cs");
		await Assert.That(firstHints).Contains("Alpha.Outer.ItemSchema.g.cs");
		await Assert.That(firstHints).Contains("Alpha.Other.ItemSchema.g.cs");
	}
}

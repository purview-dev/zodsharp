using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task StaticProperties_GivenPublicStaticFactories_AreNotIncludedInSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public readonly partial record struct ProviderType
				{
					[Required(AllowEmptyStrings = false)]
					public string Value { get; init; }

					public static ProviderType GitHub => new() { Value = "GitHub" };

					public static ProviderType AzureDevOps => new() { Value = "AzureDevOps" };
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("ProviderTypeSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("value.Value");
		await Assert.That(generated).DoesNotContain("GitHub");
		await Assert.That(generated).DoesNotContain("AzureDevOps");
		await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
	}

	[Test]
	public async Task IndexerProperties_GivenIndexer_AreNotIncludedInSchema(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class WithIndexer
				{
					[Required]
					[StringLength(10, MinimumLength = 1)]
					public string Name { get; set; } = string.Empty;

					public string this[int index]
					{
						get => Name;
						set => Name = value;
					}
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("WithIndexerSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("value.Name");
		await Assert.That(generated).DoesNotContain("value.Item");
		await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
	}
}

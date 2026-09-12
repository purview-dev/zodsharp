using System.Diagnostics.CodeAnalysis;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

[SuppressMessage(
	"Maintainability",
	"CA1506",
	Justification = "Test class naturally couples to many framework and generated types."
)]
partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenEmptySource_GeneratesAttributesOnly(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult.AllSyntaxTrees.Length).IsEqualTo(ExpectedFileCount);
	}

	[Test]
	public async Task Generate_GivenAttributeFiles_ContainsGenerateZodAttributes(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Assert — attribute files are generated
		var attributeSources = driverResult.AllSyntaxTrees.Select(static t => t.GetText().ToString()).ToList();

		await Assert.That(attributeSources).Count().IsEqualTo(ExpectedFileCount);

		var allAttributeSource = string.Join("\n", attributeSources);
		await Assert.That(allAttributeSource).Contains("class EmbeddedAttribute");
		await Assert.That(allAttributeSource).Contains("class ZodSchemaAttribute");
	}

	[Test]
	public async Task Generate_GivenAttributeFiles_ContainsRefinementMethodNameProperty(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert — the generated attribute exposes the refinement method name property
		var attributeSources = driverResult.AllSyntaxTrees.Select(static t => t.GetText().ToString()).ToList();
		var allAttributeSource = string.Join("\n", attributeSources);

		await Assert.That(allAttributeSource).Contains("RefinementMethodName");
	}
}

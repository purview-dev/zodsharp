namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenEmptyZodSchema_OutputCompilationHasNoErrors(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	class BasicModel { }
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult.EnsureValid).ThrowsNothing();
	}

	[Test]
	[Arguments("public")]
	[Arguments("internal")]
	// This is internal too
	[Arguments("")]
	public async Task Generate_GivenClassModifier_GeneratesSchemaWithSameModifier(
		string modifier,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var expectedModifier = string.IsNullOrEmpty(modifier) ? "internal" : modifier;
		var expectation = $"{expectedModifier} static partial class ModifierTestSchema";

		var source =
			$@"
namespace Testing
{{
	[ZodSchema]
	{modifier} class ModifierTest {{ }}
}}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource();

		// Assert — generated file starts with auto-generated header
		await Assert.That(generatedSource).ContainsGeneratedCode(expectation);
	}
}

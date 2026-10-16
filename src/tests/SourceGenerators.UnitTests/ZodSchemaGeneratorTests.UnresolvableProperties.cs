using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// An unresolved property type (the compilation is already failing with CS0246) must not make the generator
	/// throw and surface as CS8785. The property is skipped; the compiler's own error stays the single signal.
	/// </summary>
	[Test]
	public async Task UnresolvedPropertyType_GivenMissingType_GeneratesSchemaWithoutFailingGeneration(
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
				public partial class Broken
				{
					[Required]
					public MissingType Value { get; init; } = null!;
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			ZodSourceGeneratorTestOptions.NoValidation,
			cancellationToken
		);
		var generated = driverResult.GetSource("BrokenSchema");

		// Assert
		driverResult.AssertNoGenerationExceptions();
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).ContainsGeneratedCode("class BrokenSchema");
		await Assert.That(generated).DoesNotContain("value.Value");
	}

	/// <summary>
	/// A <c>dynamic</c> property is not a resolvable named type either; it is dropped rather than crashing the
	/// pipeline, and the rest of the schema is still emitted.
	/// </summary>
	[Test]
	public async Task DynamicProperty_GivenNonRepresentableType_IsOmittedFromSchema(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public partial class WithDynamic
				{
					[Required]
					public string Name { get; init; } = string.Empty;

					public dynamic Payload { get; set; } = null!;
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("WithDynamicSchema");

		// Assert
		driverResult.AssertNoGenerationExceptions();
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).ContainsGeneratedCode("value.Name");
		await Assert.That(generated).DoesNotContain("Payload");
		await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
	}

	/// <summary>
	/// A collection whose element type cannot be resolved still runs the collection path (the collection itself is
	/// representable), so the nested-schema lookup has to tolerate the error element too.
	/// </summary>
	[Test]
	public async Task CollectionOfUnresolvedType_GivenMissingElementType_GeneratesSchemaWithoutFailingGeneration(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public partial class BrokenCollection
				{
					public List<MissingType> Items { get; init; } = [];
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			ZodSourceGeneratorTestOptions.NoValidation,
			cancellationToken
		);
		var generated = driverResult.GetSource("BrokenCollectionSchema");

		// Assert
		driverResult.AssertNoGenerationExceptions();
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated).ContainsGeneratedCode("class BrokenCollectionSchema");
		await Assert.That(generated).ContainsGeneratedCode("value.Items");
	}
}

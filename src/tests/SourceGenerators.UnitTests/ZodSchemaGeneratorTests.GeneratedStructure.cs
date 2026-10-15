namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenModel_GeneratedSchemaHasExpectedStructure(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class Customer
	{
		public string? Name { get; set; }
	}
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("CustomerSchema");

		// Assert
		await Assert.That(generatedSource).ContainsGeneratedCode("namespace Testing");
		await Assert.That(generatedSource).ContainsGeneratedCode("#nullable enable");
		await Assert.That(generatedSource).ContainsGeneratedCode("public static partial class CustomerSchema");
	}

	[Test]
	public async Task Generate_GivenClassWithoutZodSchema_DoesNotGenerateSchema(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Customer { }
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("CustomerSchema");

		// Assert
		await Assert.That(generatedSource).IsNull();
	}

	[Test]
	public async Task Generate_GivenMultipleAnnotatedTypes_GeneratesAllSchemas(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class Customer { }

	[ZodSchema]
	public struct Address { }

	[ZodSchema]
	public record Order { }
}
";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult.GetSource("CustomerSchema")).IsNotEmpty();
		await Assert.That(driverResult.GetSource("AddressSchema")).IsNotEmpty();
		await Assert.That(driverResult.GetSource("OrderSchema")).IsNotEmpty();
	}
}

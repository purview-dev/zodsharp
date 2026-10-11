using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task JsonSchemaExport_GivenExportedTypeReferencingDisabledNestedType_ProducesZODSGEN045(
		CancellationToken cancellationToken
	)
	{
		// Arrange — Wrapper exports JSON Schema but Inner opts out, so Wrapper's schema cannot describe it.
		const string source =
			@"
namespace Testing
{
    [ZodSchema(GenerateJsonSchema = false)]
    public class Inner
    {
        public bool Flag { get; set; }
    }

    [ZodSchema]
    public class Wrapper
    {
        public Inner Inner { get; set; } = new();
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NestedSchemaExportDisabled);
	}

	[Test]
	public async Task JsonSchemaExport_GivenNestedTypeThatExports_ProducesNoZODSGEN045(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Inner
    {
        public bool Flag { get; set; }
    }

    [ZodSchema]
    public class Wrapper
    {
        public Inner Inner { get; set; } = new();
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NestedSchemaExportDisabled);
	}

	[Test]
	public async Task JsonSchemaExport_GivenOptedOutTypeThatIsNotReferenced_ProducesNoZODSGEN045(
		CancellationToken cancellationToken
	)
	{
		// Arrange — opting a type out is not itself a diagnostic; only a reference from an exported type is.
		const string source =
			@"
namespace Testing
{
    [ZodSchema(GenerateJsonSchema = false)]
    public class Standalone
    {
        public bool Flag { get; set; }
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.NestedSchemaExportDisabled);
	}
}

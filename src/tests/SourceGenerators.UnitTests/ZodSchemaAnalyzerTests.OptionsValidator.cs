using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task OptionsValidator_GivenStructOptIn_ProducesZODSGEN028(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public struct AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.IValidateOptionsValueTypeTarget);
	}

	[Test]
	public async Task OptionsValidator_GivenValidClassOptIn_ProducesNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public sealed class AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task OptionsValidator_GivenAutoDetectedSuffix_ProducesNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema]
				public sealed class AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}
}

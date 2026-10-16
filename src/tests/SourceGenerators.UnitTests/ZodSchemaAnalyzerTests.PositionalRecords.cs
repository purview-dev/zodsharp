using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// The analyzer resolves the same effective attribute set as the generator, so a validation attribute written
/// on a positional record parameter is reported from the parameter it was applied to.
/// </summary>
public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task Analyze_GivenRuleAttributeOnPositionalRecordParameterWithoutSchema_ReportsZODSGEN033(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the attribute is applied to the parameter, not to the synthesized property, so the analyzer
		// must read it from the parameter to keep reporting a rule that will not run.
		const string source = """
			using System;
			using ZodSharp.Rules;

			namespace Testing
			{
				public sealed record Summary([NonSentinel] DateTimeOffset LastObservedAt);
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.RuleAttributeWithoutSchema);
	}

	[Test]
	public async Task Analyze_GivenRuleAttributeOnPositionalRecordParameterWithSchema_ReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary([NonSentinel] DateTimeOffset LastObservedAt);
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.RuleAttributeWithoutSchema);
	}

	[Test]
	public async Task Analyze_GivenUnsupportedDataAnnotationOnPositionalRecordParameter_ReportsZODSGEN006(
		CancellationToken cancellationToken
	)
	{
		// Arrange - an email rule cannot validate a DateTimeOffset, whichever target the attribute lands on.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary([EmailAddress] DateTimeOffset LastObservedAt);
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedDataAnnotationsUsage);
	}

	[Test]
	public async Task Analyze_GivenSupportedDataAnnotationOnPositionalRecordParameter_ReportsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary(
					[Range(typeof(DateTimeOffset), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					DateTimeOffset LastObservedAt
				);
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedDataAnnotationsUsage);
	}
}

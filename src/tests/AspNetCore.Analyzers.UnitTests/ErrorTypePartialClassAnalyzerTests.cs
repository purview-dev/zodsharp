using ZodSharp.AspNetCore.Analyzers.Infra;

namespace ZodSharp.AspNetCore.Analyzers;

public class ErrorTypePartialClassAnalyzerTests : ErrorTypePartialClassAnalyzerTestBase
{
	[Test]
	public async Task GivenNonPartialContainingClass_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(Code: "aggregate_save_failed")
				{
					Parameters = ["OrderId", "AggregateType"]
				};
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ErrorTypePartialClassAnalyzer.DiagnosticId);
	}

	[Test]
	public async Task GivenPartialContainingClass_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static partial class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(Code: "aggregate_save_failed")
				{
					Parameters = ["OrderId", "AggregateType"]
				};
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenNonStaticErrorTypeField_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public partial class ConcurrentErrorType
			{
				[ErrorType]
				public readonly ErrorType SaveFailed = new(Code: "aggregate_save_failed");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic("ZODSASP003");
	}

	[Test]
	public async Task GivenUnattributedErrorTypeField_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(Code: "aggregate_save_failed");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenNonErrorTypeField_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public sealed record Other(string Code);

			public static partial class SomeType
			{
				[ErrorType]
				public static readonly Other Value = new("something");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}

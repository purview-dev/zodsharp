using ZodSharp.AspNetCore.Analyzers.Infra;

namespace ZodSharp.AspNetCore.Analyzers;

public partial class ErrorTypeMessageFormatAnalyzerTests : ErrorTypeAnalyzerTestBase
{
	[Test]
	public async Task GivenMessageFormat_WithUndeclaredPlaceholder_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					HttpStatus: 409,
					MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save",
					Parameters: [new ErrorTypeParameter("AggregateId", typeof(string))]);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ErrorTypeMessageFormatAnalyzer.DiagnosticId);
	}

	[Test]
	public async Task GivenMessageFormat_WithAllParametersDeclared_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					HttpStatus: 409,
					MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save",
					Parameters:
					[
						new ErrorTypeParameter("AggregateId", typeof(string)),
						new ErrorTypeParameter("AggregateType", typeof(string))
					]);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenMessageFormat_WithTargetTypedParametersDeclared_HasNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					Description: "The order could not be saved because it was modified concurrently.",
					HttpStatus: StatusCodes.Status409Conflict,
					MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
					Parameters: [new("OrderId", typeof(string)), new("AggregateType", typeof(string))]);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenMessageFormat_WithParamInvocationParametersDeclared_HasNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					HttpStatus: 409,
					MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
					Parameters:
					[
						new ErrorTypeParameter("OrderId", typeof(string)),
						ErrorType.Param<string>("AggregateType")
					]);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenMessageFormat_PositionalArguments_ReportsPlaceholders(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				public static readonly ErrorType SaveFailed = new(
					"aggregate_save_failed", null, 409, null, null, "Aggregate '{AggregateId}' failed to save");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostics(1);
	}

	[Test]
	public async Task GivenMessageFormat_WithEscapedBraces_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class Escaped
			{
				public static readonly ErrorType Saved = new(
					Code: "saved",
					MessageFormat: "The value {{Value}} is fine.")
				{
					Parameters = [new ErrorTypeParameter("Value", typeof(string))]
				};
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenMessageFormat_WithNumericPlaceholder_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class Numeric
			{
				public static readonly ErrorType Saved = new(
					Code: "saved",
					MessageFormat: "Field {0} is required.");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenParametersOmitted_ReportsPlaceholders(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class MissingParameters
			{
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					MessageFormat: "Aggregate '{AggregateId}' failed to save");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(ErrorTypeMessageFormatAnalyzer.DiagnosticId);
	}

	[Test]
	public async Task GivenNoMessageFormat_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public static class Plain
			{
				public static readonly ErrorType Conflict = new(Code: "conflict", HttpStatus: 409);
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task GivenNonErrorTypeObjectCreation_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing;

			public sealed record Other(string MessageFormat);

			public static class SomeType
			{
				public static readonly Other Value = new(MessageFormat: "Aggregate '{AggregateId}' failed to save");
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasNoDiagnostics();
	}
}

using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task ValidationMethods_GivenBothSyncAndAsync_ProducesZODSGEN029(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Both
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate() => [];

					internal static ValueTask<ValidationResult<Both>> CustomValidationAsync(
						Both value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<Both>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.AmbiguousValidationMethods);
	}

	[Test]
	public async Task ValidationMethods_GivenSyncOnModelAndAsyncOnSchemaValidator_ProducesZODSGEN029(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public partial class Both
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate() => [];
				}

				public partial class BothSchemaValidator
				{
					public ValueTask<ValidationResult<Both>> CustomValidationAsync(
						Both value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<Both>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.AmbiguousValidationMethods);
	}

	[Test]
	public async Task ValidationMethods_GivenOnlySyncMethod_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class OnlySync
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate() => [];
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task ValidationMethods_GivenOnlyAsyncMethod_HasNoDiagnostics(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class OnlyAsync
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<OnlyAsync>> CustomValidationAsync(
						OnlyAsync value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<OnlyAsync>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}
}

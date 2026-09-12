using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task SyncValidation_GivenExplicitNameMissing_ProducesZODSGEN007(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema(RefinementMethodName = "DoesNotExist")]
				public class MissingMethod { public string? Name { get; set; } }
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationMethodNotFound);
	}

	[Test]
	public async Task SyncValidation_GivenVoidReturnType_ProducesZODSGEN022(CancellationToken cancellationToken)
	{
		var source = """
			namespace Testing
			{
				[ZodSchema]
				public class VoidReturn
				{
					public string? Name { get; set; }

					public void Validate()
					{
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.SyncValidationInvalidReturnType);
	}

	[Test]
	public async Task SyncValidation_GivenTooManyParameters_ProducesZODSGEN023(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class TooManyParams
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate(int extra, string another) => [];
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.SyncValidationInvalidParameterCount);
	}

	[Test]
	public async Task SyncValidation_GivenStaticMethod_ProducesZODSGEN024(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class StaticMethod
				{
					public string? Name { get; set; }

					public static IEnumerable<ValidationError> Validate() => [];
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.SyncValidationInvalidStaticInstance);
	}

	[Test]
	public async Task SyncValidation_GivenPrivateMethod_ProducesZODSGEN025(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class PrivateMethod
				{
					public string? Name { get; set; }

					IEnumerable<ValidationError> Validate() => [];
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.SyncValidationInaccessible);
	}

	[Test]
	public async Task SyncValidation_GivenNonRefineContextParameter_ProducesZODSGEN026(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class BadContext
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate(string ctx) => [];
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.SyncValidationInvalidContextParameter);
	}

	[Test]
	public async Task SyncValidation_GivenValidParameterlessMethod_ProducesNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class ValidSync
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate()
					{
						yield break;
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task SyncValidation_GivenValidRefineContextMethod_ProducesNoDiagnostics(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public class ValidCtxSync
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate(RefineCtx<ValidCtxSync> ctx)
					{
						yield break;
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task SyncValidation_GivenInternalMethod_ProducesNoDiagnostics(CancellationToken cancellationToken)
	{
		var source = """
			using System.Collections.Generic;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class InternalSync
				{
					public string? Name { get; set; }

					internal IEnumerable<ValidationError> Validate()
					{
						yield break;
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task SyncValidation_GivenNoMethod_ProducesNoDiagnostics(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema]
				public class NoMethod { public string? Name { get; set; } }
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}
}

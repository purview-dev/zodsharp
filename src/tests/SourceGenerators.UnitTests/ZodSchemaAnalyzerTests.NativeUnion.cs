#if NET11_0_OR_GREATER
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task NativeUnion_GivenAllReferenceTypeCases_ProducesZODSGEN041(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public static class Schemas
				{
					public static void Build()
					{
						var schema = Z.Union(Z.String(), Z.Object().Build());
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NativeUnionRecommended);
	}

	[Test]
	public async Task NativeUnion_GivenAValueTypeCase_AlsoProducesZODSGEN041(CancellationToken cancellationToken)
	{
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public static class Schemas
				{
					public static void Build()
					{
						var schema = Z.Union(Z.String(), Z.Number());
					}
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);

		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.NativeUnionRecommended);
	}
}
#endif

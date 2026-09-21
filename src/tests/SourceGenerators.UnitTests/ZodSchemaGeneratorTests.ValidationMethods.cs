namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task ValidationMethods_GivenBothSyncAndAsync_EmitsSyncRefinementOnly(
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
				public class WithBoth
				{
					public string? Name { get; set; }

					public IEnumerable<ValidationError> Validate() => [];

					internal static ValueTask<ValidationResult<WithBoth>> CustomValidationAsync(
						WithBoth value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<WithBoth>.Success(value));
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WithBothSchema");

		// The synchronous refinement is honoured...
		await Assert.That(generated).ContainsGeneratedCode("var refinementErrors = value.Validate();");

		// ...and the async custom method is dropped so the validator does not reference both.
		await Assert.That(generated).DoesNotContain("CustomValidationAsync", StringComparison.Ordinal);
		await Assert.That(generated).ContainsGeneratedCode("ValueTask.FromResult");
	}
}

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task ValidationMethods_GivenHookAndAsync_EmitsHookOnly(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public partial class WithBoth
				{
					public string? Name { get; set; }

					partial void OnZodValidate(RefineCtx<WithBoth> context)
					{
						context.AddIssue("custom", "Nope.", [nameof(Name)]);
					}

					internal static ValueTask<ValidationResult<WithBoth>> CustomValidationAsync(
						WithBoth value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<WithBoth>.Success(value));
				}
			}
			""";

		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WithBothSchema");

		// The refinement hook is honoured...
		await Assert.That(generated).ContainsGeneratedCode("InvokeZodRefinementHook(value, refineContext)");

		// ...and the async custom method is dropped so the validator does not reference both.
		await Assert.That(generated).DoesNotContain("CustomValidationAsync", StringComparison.Ordinal);
		await Assert.That(generated).ContainsGeneratedCode("=> new(ValidateCore(value, depth));");
	}
}

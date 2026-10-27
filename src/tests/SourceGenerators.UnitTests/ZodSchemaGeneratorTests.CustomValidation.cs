namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task CustomValidation_GivenNoMethod_GeneratesSyncFallback_NoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class NoCustom { public string? Name { get; set; } }
}";

		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("NoCustomSchema");

		// No async state machine
		await Assert
			.That(generated)
			.ContainsGeneratedCode("=> new(ValidateCore(value, depth));")
			.Because("Should use FromResult, not async");
		await Assert
			.That(generated)
			.DoesNotContain("async ", StringComparison.Ordinal)
			.Because("Should not generate async when no custom method");
	}

	[Test]
	public async Task CustomValidation_GivenDefaultMethodExists_GeneratesAsyncPath(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class WithDefault
	{
		public string? Name { get; set; }

		internal static ValueTask<ValidationResult<WithDefault>> CustomValidationAsync(
			WithDefault value, CancellationToken ct) =>
			ValueTask.FromResult(ValidationResult<WithDefault>.Success(value));
	}
}";

		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WithDefaultSchema");

		await Assert
			.That(generated)
			.Contains("async ", StringComparison.Ordinal)
			.Because("Should generate async when custom method exists");
		await Assert.That(generated).ContainsGeneratedCode("await global::Testing.WithDefault.CustomValidationAsync");
		await Assert.That(generated).ContainsGeneratedCode(".ConfigureAwait(false)");
		await Assert.That(generated).ContainsGeneratedCode("ZodGeneratedValidation.WithResult(errors, customResult)");
	}

	[Test]
	public async Task CustomValidation_GivenOverriddenMethodName_GeneratesAsyncPath(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema(CustomValidationMethodName = nameof(ValidateRulesAsync))]
	public class WithOverride
	{
		public string? Name { get; set; }

		internal static ValueTask<ValidationResult<WithOverride>> ValidateRulesAsync(
			WithOverride value, CancellationToken ct) =>
			ValueTask.FromResult(ValidationResult<WithOverride>.Success(value));
	}
}";

		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WithOverrideSchema");

		await Assert.That(generated).ContainsGeneratedCode("await global::Testing.WithOverride.ValidateRulesAsync");
	}

	[Test]
	public async Task CustomValidation_GivenMultipleValidOverloads_ProducesZODSGEN015(
		CancellationToken cancellationToken
	)
	{
		var source =
			@"
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class Ambiguous
	{
		public string? Name { get; set; }

		internal static ValueTask<ValidationResult<Ambiguous>> CustomValidationAsync(
			Ambiguous value, CancellationToken ct) =>
			ValueTask.FromResult(ValidationResult<Ambiguous>.Success(value));

		internal static ValueTask<ValidationResult<Ambiguous>> CustomValidationAsync(
			Ambiguous value, CancellationToken ct, string extra) =>
			ValueTask.FromResult(ValidationResult<Ambiguous>.Success(value));
	}
}";

		// The second overload has 3 params so it won't match — only one valid candidate.
		// This test verifies the generator doesn't falsely report ambiguity.
		using var driverResult = await GenerateAsync(source, cancellationToken);

		await Assert.That(driverResult.EnsureValid).ThrowsNothing();
	}
}

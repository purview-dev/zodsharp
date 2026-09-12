using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string OptionsValidatorSource = """
		using System.ComponentModel.DataAnnotations;
		using System.Collections.Generic;

		namespace Testing
		{
			[ZodSchema]
			public sealed class AdminPortalOptions
			{
				[Required]
				public string? RoutePrefix { get; set; }
			}
		}
		""";

	[Test]
	public async Task OptionsValidator_GivenExplicitOptIn_GeneratesValidator(CancellationToken cancellationToken)
	{
		var source = """
			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public sealed class AdminPortal
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(source, new ZodSourceGeneratorTestOptions(), cancellationToken);
		var generated = driverResult.GetSource("AdminPortalSchema");

		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"public sealed partial class AdminPortalValidator : global::Microsoft.Extensions.Options.IValidateOptions<global::Testing.AdminPortal>"
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"public global::Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, global::Testing.AdminPortal options)"
			);
		await Assert.That(generated).ContainsGeneratedCode("var schemaResult = AdminPortalSchema.Validate(options);");
		await Assert.That(generated).ContainsGeneratedCode("schemaResult.Errors.Select(static error => error.Message)");
	}

	[Test]
	public async Task OptionsValidator_GivenNoFlagAndNoSuffix_DoesNotGenerateValidator(
		CancellationToken cancellationToken
	)
	{
		var source = """
			namespace Testing
			{
				[ZodSchema]
				public sealed class AdminPortal
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AdminPortalSchema");

		await Assert.That(generated).DoesNotContain("AdminPortalValidator");
	}

	[Test]
	public async Task OptionsValidator_GivenDefaultAutoDetect_GeneratesForOptionsSuffix(
		CancellationToken cancellationToken
	)
	{
		var driverResult = await GenerateAsync(
			OptionsValidatorSource,
			new ZodSourceGeneratorTestOptions(),
			cancellationToken
		);
		var generated = driverResult.GetSource("AdminPortalOptionsSchema");

		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"public sealed partial class AdminPortalOptionsValidator : global::Microsoft.Extensions.Options.IValidateOptions<global::Testing.AdminPortalOptions>"
			);
	}

	[Test]
	public async Task OptionsValidator_GivenExplicitOptOut_DoesNotGenerateForOptionsSuffix(
		CancellationToken cancellationToken
	)
	{
		var source = """
			namespace Testing
			{
				[ZodSchema(SuppressIValidateOptions = true)]
				public sealed class AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AdminPortalOptionsSchema");

		await Assert.That(generated).DoesNotContain("AdminPortalOptionsValidator");
	}

	[Test]
	public async Task OptionsValidator_GivenAutoDetectDisabled_DoesNotGenerateForOptionsSuffix(
		CancellationToken cancellationToken
	)
	{
		ZodSourceGeneratorTestOptions options = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpAutoGenerateOptionsValidators",
				"false"
			),
		};

		var driverResult = await GenerateAsync(OptionsValidatorSource, options, cancellationToken);
		var generated = driverResult.GetSource("AdminPortalOptionsSchema");

		await Assert.That(generated).DoesNotContain("AdminPortalOptionsValidator");
	}

	[Test]
	public async Task OptionsValidator_GivenCustomSuffix_UsesConfiguredSuffixes(CancellationToken cancellationToken)
	{
		var source = """
			namespace Testing
			{
				[ZodSchema]
				public sealed class AdminPortalConfig
				{
					public string? RoutePrefix { get; set; }
				}

				[ZodSchema]
				public sealed class AdminPortalOptions
				{
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		ZodSourceGeneratorTestOptions options = new()
		{
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpAutoGenerateOptionsValidatorSuffixes",
				"Config"
			),
		};

		var driverResult = await GenerateAsync(source, options, cancellationToken);

		var configSource = driverResult.GetSource("AdminPortalConfigSchema");
		await Assert.That(configSource).ContainsGeneratedCode("public sealed partial class AdminPortalConfigValidator");

		var optionsSource = driverResult.GetSource("AdminPortalOptionsSchema");
		await Assert.That(optionsSource).DoesNotContain("AdminPortalOptionsValidator");
	}

	[Test]
	public async Task OptionsValidator_Runtime_ReturnsFailedForInvalidAndSucceededForValid(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public sealed class AdminPortalOptions
				{
					[Required]
					public string? RoutePrefix { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.AdminPortalOptions")!;
		var validatorType = assembly.GetType("Testing.AdminPortalOptionsValidator")!;

		var validator = Activator.CreateInstance(validatorType)!;
		var validateMethod = validatorType.GetMethod("Validate")!;

		var invalid = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("RoutePrefix")!.SetValue(invalid, null);
		var invalidResult = (ValidateOptionsResult)validateMethod.Invoke(validator, [null, invalid])!;
		await Assert.That(invalidResult.Failed).IsTrue();
		await Assert.That(invalidResult.FailureMessage).Contains("RoutePrefix");

		var valid = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("RoutePrefix")!.SetValue(valid, "/admin/api");
		var validResult = (ValidateOptionsResult)validateMethod.Invoke(validator, [null, valid])!;
		await Assert.That(validResult.Succeeded).IsTrue();
	}
}

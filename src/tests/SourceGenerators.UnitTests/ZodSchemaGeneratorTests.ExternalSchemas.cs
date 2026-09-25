using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string ServiceOptionsContractSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing.Contracts
		{
			[ZodSchema]
			public class ServiceOptions
			{
				[Required]
				[StringLength(15, MinimumLength = 3)]
				public string ServiceName { get; set; } = string.Empty;
			}
		}
		""";

	const string ServiceOptionsWithoutSchemaSource = """
		namespace Testing.Contracts
		{
			public class ServiceOptions
			{
				public string ServiceName { get; set; } = string.Empty;
			}
		}
		""";

	const string LookAlikeSchemaSource = """
		namespace Testing.Contracts
		{
			public class ServiceOptions
			{
				public string ServiceName { get; set; } = string.Empty;
			}

			public static class ServiceOptionsSchema
			{
			}
		}
		""";

	const string AppOptionsSource = """
		using System.ComponentModel.DataAnnotations;
		using Testing.Contracts;
		using ZodSharp;

		namespace Testing.App
		{
			[ZodSchema]
			public class AppOptions
			{
				[Required]
				public ServiceOptions Services { get; set; } = new();
			}
		}
		""";

	const string AppOptionsCollectionSource = """
		using System.Collections.Generic;
		using System.ComponentModel.DataAnnotations;
		using Testing.Contracts;
		using ZodSharp;

		namespace Testing.App
		{
			[ZodSchema]
			public class AppOptions
			{
				[Required]
				public List<ServiceOptions> Services { get; set; } = [];
			}
		}
		""";

	const string LocalOptionsSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing.Local
		{
			[ZodSchema]
			public class LocalServiceOptions
			{
				[Required]
				public string ServiceName { get; set; } = string.Empty;
			}

			[ZodSchema]
			public class LocalAppOptions
			{
				[Required]
				public LocalServiceOptions Services { get; set; } = new();
			}
		}
		""";

	[Test]
	public async Task ExternalSchema_GivenReferencedTypeWithGeneratedSchema_DoesNotEmitDuplicateSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsContractSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsSource, options, cancellationToken);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("ServiceOptionsSchema.g.cs")).IsFalse();
		await Assert.That(hintNames.Contains("AppOptionsSchema.g.cs")).IsTrue();
	}

	[Test]
	public async Task ExternalSchema_GivenReferencedTypeWithGeneratedSchema_ReferencesTheExistingSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsContractSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsSource, options, cancellationToken);

		// Assert
		var generated = driverResult.GetSource("AppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("global::Testing.Contracts.ServiceOptionsSchema.Validate(");
	}

	[Test]
	public async Task ExternalSchema_GivenReferencedTypeWithGeneratedSchema_CompilesWithoutDuplicateTypeWarning(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsContractSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsSource, options, cancellationToken);

		// Assert
		var diagnostics = driverResult.CompilationResult.Compilation.GetDiagnostics(cancellationToken);
		await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Id == "CS0436")).IsFalse();
		await Assert
			.That(diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
			.IsFalse();
	}

	[Test]
	public async Task ExternalSchema_GivenCollectionOfReferencedType_ReferencesTheExistingElementSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsContractSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsCollectionSource, options, cancellationToken);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("ServiceOptionsSchema.g.cs")).IsFalse();

		var generated = driverResult.GetSource("AppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("global::Testing.Contracts.ServiceOptionsSchema.Validate(");
	}

	[Test]
	public async Task ExternalSchema_GivenReferencedTypeWithoutGeneratedSchema_DoesNotEmitSchemaForIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsWithoutSchemaSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsSource, options, cancellationToken);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("ServiceOptionsSchema.g.cs")).IsFalse();

		var generated = driverResult.GetSource("AppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).DoesNotContain("ServiceOptionsSchema.Validate");
	}

	[Test]
	public async Task ExternalSchema_GivenUserDeclaredTypeWithSchemaNameButNoMarker_IsNotReferenced(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(LookAlikeSchemaSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(AppOptionsSource, options, cancellationToken);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("ServiceOptionsSchema.g.cs")).IsFalse();

		var generated = driverResult.GetSource("AppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).DoesNotContain("ServiceOptionsSchema.Validate");
	}

	[Test]
	public async Task ExternalSchema_GivenTypeDeclaredInThisCompilation_StillEmitsNestedSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		// Act
		var driverResult = await GenerateAsync(
			LocalOptionsSource,
			new ZodSourceGeneratorTestOptions(),
			cancellationToken
		);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("LocalServiceOptionsSchema.g.cs")).IsTrue();
		await Assert.That(hintNames.Contains("LocalAppOptionsSchema.g.cs")).IsTrue();

		var generated = driverResult.GetSource("LocalAppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert
			.That(generated!)
			.ContainsGeneratedCode("global::Testing.Local.LocalServiceOptionsSchema.Validate(");
	}

	const string NestedAppOptionsSource = """
		using System.ComponentModel.DataAnnotations;
		using Testing.Contracts;
		using ZodSharp;

		namespace Testing.App
		{
			public partial class ChangeOpsApp
			{
				[ZodSchema]
				public partial class ChangeOpsAppOptions
				{
					[Required]
					public ServiceOptions Services { get; set; } = new();
				}
			}
		}
		""";

	[Test]
	public async Task ExternalSchema_GivenNestedOptionsTypeWithReferencedProperty_ReferencesTheExistingSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var contractReference = await CompileContractAsync(ServiceOptionsContractSource, cancellationToken);
		ZodSourceGeneratorTestOptions options = new() { AdditionalReferences = [contractReference] };

		// Act
		var driverResult = await GenerateAsync(NestedAppOptionsSource, options, cancellationToken);

		// Assert
		var hintNames = GeneratedHintNames(driverResult);
		await Assert.That(hintNames.Contains("ServiceOptionsSchema.g.cs")).IsFalse();
		await Assert.That(hintNames.Contains("ChangeOpsAppOptionsSchema.g.cs")).IsTrue();

		var generated = driverResult.GetSource("ChangeOpsAppOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("global::Testing.Contracts.ServiceOptionsSchema.Validate(");

		var diagnostics = driverResult.CompilationResult.Compilation.GetDiagnostics(cancellationToken);
		await Assert.That(diagnostics.Any(static diagnostic => diagnostic.Id == "CS0436")).IsFalse();
	}

	static List<string> GeneratedHintNames(DriverRunResult driverResult) =>
		[
			.. driverResult
				.DriverResult.Results.SelectMany(static result => result.GeneratedSources)
				.Select(static source => source.HintName),
		];

	async Task<MetadataReference> CompileContractAsync(string source, CancellationToken cancellationToken)
	{
		ZodSourceGeneratorTestOptions options = new() { CompilationAssemblyName = "Testing.Contracts" };
		var contractRun = await GenerateAsync(source, options, cancellationToken);

		using MemoryStream stream = new();
		var emitResult = contractRun.CompilationResult.Compilation.Emit(stream, cancellationToken: cancellationToken);
		await Assert.That(emitResult.Success).IsTrue();

		return MetadataReference.CreateFromImage(stream.ToArray());
	}
}

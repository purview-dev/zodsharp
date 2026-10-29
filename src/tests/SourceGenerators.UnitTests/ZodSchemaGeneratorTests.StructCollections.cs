using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	[Arguments("ImmutableArray<string>", "ImmutableArray.Create<string>((string)null!)", "invalid_type")]
	[Arguments("ImmutableArray<string>?", "ImmutableArray.Create<string>((string)null!)", "invalid_type")]
	[Arguments("ImmutableArray<string>?", "null", "")]
	[Arguments("ImmutableArray<string>?", "default(ImmutableArray<string>)", "")]
	[Arguments("ImmutableArray<string>", "default", "invalid_type")]
	[Arguments("ImmutableArray<string>", "ImmutableArray<string>.Empty", "too_small")]
	[Arguments("ImmutableArray<Child>", "ImmutableArray.Create(new Child())", "missing_field")]
	[Arguments("ImmutableArray<Child>?", "ImmutableArray.Create(new Child())", "missing_field")]
	[Arguments("ImmutableArray<Child?>", "ImmutableArray.Create<Child?>((Child?)null)", "")]
	[Arguments("List<Child>?", "null", "")]
	[Arguments("List<Child>", "new List<Child> { null! }", "invalid_type")]
	[Arguments("Bag<Child>", "new Bag<Child>(new[] { new Child() })", "missing_field")]
	[Arguments("Bag<Child>?", "new Bag<Child>(new[] { new Child() })", "missing_field")]
	[Arguments("Bag<Child>?", "null", "")]
	public async Task Collections_GivenStructAndReferenceNullability_CompileWithoutWarningsAndApplyNullRules(
		string collectionType,
		string initializer,
		string expectedCode,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = $$"""
			using System.Collections.Generic;
			using System.Collections.Immutable;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			namespace Testing;

			[ZodSchema]
			public class Container
			{
				[MinLength(1)]
				public {{collectionType}} Items { get; set; } = {{initializer}};
			}

			[ZodSchema]
			public class Child
			{
				[Required]
				public string? Name { get; set; }
			}

			public readonly struct Bag<T>(T[] items) : IReadOnlyCollection<T>
			{
				int IReadOnlyCollection<T>.Count => items.Length;
				public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
				System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
			}
			""";

		// Act
		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions { IncludeDefaultNamespaces = false }.Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var container = Create(assembly, "Testing.Container");
		var errors = RunValidate(assembly, "Testing.ContainerSchema", container);
		var memberErrors = RunValidateMember(assembly, "Testing.ContainerSchema", container, "Items");

		// Assert
		await AssertWarningFreeCompilation(result.CompilationResult.Compilation, cancellationToken);
		await Assert.That(result.Generated().HasClass("ContainerSchema")).IsTrue();
		await Assert
			.That(errors.Select(static error => error.Code).ToArray())
			.IsEquivalentTo(expectedCode.Length == 0 ? [] : new[] { expectedCode });
		await Assert.That(memberErrors.Length).IsEqualTo(expectedCode is "" or "missing_field" ? 0 : 1);
	}

	[Test]
	public async Task ImmutableArray_GivenDefaultValues_UsesRequiredAndNullableSemantics(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Immutable;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			namespace Testing;

			[ZodSchema]
			public class Container
			{
				public ImmutableArray<int> Missing { get; set; }

				[Required(ErrorMessage = "Supply items")]
				public ImmutableArray<int> Required { get; set; }

				[Required]
				public ImmutableArray<int>? RequiredNullable { get; set; } = default(ImmutableArray<int>);

				[Required]
				public ImmutableArray<int>? RequiredNullWrapper { get; set; }

				public ImmutableArray<int>? Optional { get; set; } = default(ImmutableArray<int>);

				[Required]
				public ImmutableArray<int> Empty { get; set; } = ImmutableArray<int>.Empty;

				[ZodSharp.Rules.RequiredZod]
				public ImmutableArray<int> RequiredRule { get; set; }

				[ZodSharp.Rules.RequiredZod]
				public ImmutableArray<int>? RequiredNullableRule { get; set; } = default(ImmutableArray<int>);
			}
			""";
		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions { IncludeDefaultNamespaces = false }.Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;

		// Act
		var errors = RunValidate(assembly, "Testing.ContainerSchema", Create(assembly, "Testing.Container"));

		// Assert
		await AssertWarningFreeCompilation(result.CompilationResult.Compilation, cancellationToken);
		await Assert
			.That(errors.Select(static error => $"{error.Path[0]}:{error.Code}").ToArray())
			.IsEquivalentTo([
				"Missing:invalid_type",
				"Required:missing_field",
				"RequiredNullable:missing_field",
				"RequiredNullWrapper:missing_field",
				"RequiredRule:missing_field",
				"RequiredNullableRule:missing_field",
			]);
		await Assert.That(errors.Single(static error => error.Path[0] == "Required").Message).IsEqualTo("Supply items");
	}

	[Test]
	[Arguments("", "", "", "invalid_type")]
	[Arguments("TreatAsEmpty", "", "", "too_small")]
	[Arguments("TreatAsEmpty", "ValidationError", "", "invalid_type")]
	[Arguments("ValidationError", "TreatAsEmpty", "", "too_small")]
	[Arguments("TreatAsEmpty", "TreatAsEmpty", "ValidationError", "invalid_type")]
	[Arguments("ValidationError", "ValidationError", "TreatAsEmpty", "too_small")]
	public async Task ImmutableArray_GivenConfiguredDefaultBehavior_ResolvesPropertyAssemblyAndBuildPrecedence(
		string buildBehavior,
		string assemblyBehavior,
		string propertyBehavior,
		string expectedCode,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var assemblyAttribute =
			assemblyBehavior.Length == 0
				? ""
				: $"[assembly: ZodSharpSchemaDefaults(DefaultImmutableArrayBehavior = DefaultImmutableArrayBehavior.{assemblyBehavior})]";
		var propertyAttribute =
			propertyBehavior.Length == 0
				? ""
				: $"[CollectionBehavior(DefaultImmutableArrayBehavior = DefaultImmutableArrayBehavior.{propertyBehavior})]";
		var source = $$"""
			using System.Collections.Immutable;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			{{assemblyAttribute}}
			namespace Testing;

			[ZodSchema]
			public class Container
			{
				{{propertyAttribute}}
				[Required]
				[MinLength(1)]
				public ImmutableArray<int>? Items { get; set; } = default(ImmutableArray<int>);
			}
			""";
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			IncludeDefaultNamespaces = false,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.ZodSharpDefaultImmutableArrayBehavior",
				buildBehavior
			),
		};
		using var result = await GenerateAsync(source, options, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;

		// Act
		var errors = RunValidate(assembly, "Testing.ContainerSchema", Create(assembly, "Testing.Container"));

		// Assert
		await AssertWarningFreeCompilation(result.CompilationResult.Compilation, cancellationToken);
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo(expectedCode == "invalid_type" ? "missing_field" : expectedCode);
	}

	[Test]
	[Arguments(false, false, false)]
	[Arguments(true, false, false)]
	[Arguments(false, true, false)]
	[Arguments(true, true, false)]
	[Arguments(false, true, true)]
	[Arguments(true, true, true)]
	public async Task ImmutableArray_GivenAsyncNestedSchemasAndNullableCollection_ValidatesAndResolvesMembers(
		bool nullableCollection,
		bool defaultArray,
		bool treatAsEmpty,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = $$"""
			using System.Collections.Immutable;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;
			using ZodSharp.Core;
			namespace Testing;

			[ZodSchema]
			public class Container
			{
				[CollectionBehavior(DefaultImmutableArrayBehavior = DefaultImmutableArrayBehavior.{{(
				treatAsEmpty ? "TreatAsEmpty" : "ValidationError"
			)}})]
				public ImmutableArray<Child?>{{(nullableCollection ? "?" : "")}} Items { get; set; } =
					{{(
				defaultArray ? "default(ImmutableArray<Child?>)" : "ImmutableArray.Create<Child?>(null, new Child())"
			)}};
			}

			[ZodSchema]
			public struct Child
			{
				public string? Name { get; set; }

				internal static ValueTask<ValidationResult<Child>> CustomValidationAsync(Child value, CancellationToken cancellationToken)
					=> ValueTask.FromResult(ValidationResult<Child>.Failure(
						new[] { ValidationError.Create("child_async", "Invalid child", ChildSchema.Paths.Name) }));
			}
			""";
		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions { IncludeDefaultNamespaces = false }.Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var container = Create(assembly, "Testing.Container");

		// Act
		var errors = await RunValidationAsync(
			assembly,
			"Testing.ContainerSchema",
			container,
			cancellationToken: cancellationToken
		);
		var resolved = RunTryResolveMember(
			assembly,
			"Testing.ContainerSchema",
			container,
			ZodPath.ParseDotted("Items[1].Name"),
			out var owner,
			out var member
		);

		// Assert
		await AssertWarningFreeCompilation(result.CompilationResult.Compilation, cancellationToken);
		string[] expectedCodes = defaultArray
			? nullableCollection || treatAsEmpty
				? []
				: ["invalid_type"]
			: ["child_async"];
		await Assert.That(errors.Select(static error => error.Code).ToArray()).IsEquivalentTo(expectedCodes);
		await Assert.That(resolved).IsEqualTo(!defaultArray);
		if (!defaultArray)
		{
			await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Items[1].Name");
			await Assert.That(owner).IsSameReferenceAs(container);
			await Assert.That(member).IsEqualTo("Items");
		}
	}

	static async Task AssertWarningFreeCompilation(Compilation compilation, CancellationToken cancellationToken)
	{
		var strict = compilation.WithOptions(compilation.Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error));
		var problems = strict
			.GetDiagnostics(cancellationToken)
			.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
			.Select(static diagnostic => diagnostic.ToString())
			.ToArray();
		await Assert.That(problems).IsEmpty();
	}
}

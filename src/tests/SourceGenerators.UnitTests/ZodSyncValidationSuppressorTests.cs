using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// CA1849 is reported at the call site, so a package can only answer it with a suppressor. These tests pair
/// <see cref="ZodSyncValidationSuppressor"/> with a stand-in for the SDK's analyzer (which is not referenced here,
/// and whose async-context condition would only add setup noise) that reports CA1849 for every invocation of a
/// method that has an <c>Async</c> sibling.
/// </summary>
public class ZodSyncValidationSuppressorTests : ZodSharpSourceGeneratorTestBase<ZodSchemaGenerator>
{
	static string SourceWith(string call) =>
		$$"""
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Person
				{
					public string Name { get; set; } = string.Empty;
				}

				public class Widget
				{
					public static int Validate() => 0;

					public static ValueTask<int> ValidateAsync() => ValueTask.FromResult(0);
				}

				public static class Holder
				{
					public static string Result => string.Empty;
				}

				public static class Caller
				{
					public static object Call(Person person) => {{call}};
				}
			}
			""";

	[Test]
	[Arguments("PersonSchema.Validate(person)")]
	[Arguments("PersonSchema.ValidateMember(person, nameof(Person.Name))")]
	[Arguments("new PersonSchemaValidator().Validate(person)")]
	[Arguments("Z.String().Validate(\"value\")")]
	public async Task Ca1849_GivenZodValidationCall_IsSuppressed(string call, CancellationToken cancellationToken)
	{
		// Arrange
		var compilation = await CompileAsync(SourceWith(call), cancellationToken);

		// Act
		var diagnostics = await RunAnalyzersAsync(compilation, cancellationToken);

		// Assert
		await Assert.That(diagnostics.Length).IsEqualTo(1);
		await Assert.That(diagnostics[0].Id).IsEqualTo(ZodSyncValidationSuppressor.SuppressedDiagnosticId);
		await Assert.That(diagnostics[0].IsSuppressed).IsTrue();
	}

	[Test]
	public async Task Ca1849_GivenForeignValidateCall_IsNotSuppressed(CancellationToken cancellationToken)
	{
		// Arrange — a type outside ZodSharp that pairs Validate with ValidateAsync.
		var compilation = await CompileAsync(SourceWith("Widget.Validate()"), cancellationToken);

		// Act
		var diagnostics = await RunAnalyzersAsync(compilation, cancellationToken);

		// Assert
		await Assert.That(diagnostics.Length).IsEqualTo(1);
		await Assert.That(diagnostics[0].IsSuppressed).IsFalse();
	}

	[Test]
	public async Task Ca1849_GivenBlockingMemberInsideAZodValidationCall_IsNotSuppressed(
		CancellationToken cancellationToken
	)
	{
		// Arrange — CA1849 also reports blocking members (Task.Wait, Task.Result), which can sit inside the
		// arguments of a Validate call; only the call itself is ours to suppress.
		var compilation = await CompileAsync(SourceWith("Z.String().Validate(Holder.Result)"), cancellationToken);

		// Act
		var diagnostics = await RunAnalyzersAsync(compilation, cancellationToken);
		var unsuppressed = diagnostics.Where(static diagnostic => !diagnostic.IsSuppressed).ToArray();

		// Assert
		await Assert.That(diagnostics.Length).IsEqualTo(2);
		await Assert.That(unsuppressed.Length).IsEqualTo(1);
		var text = await unsuppressed[0].Location.SourceTree!.GetTextAsync(cancellationToken);
		await Assert.That(text.ToString(unsuppressed[0].Location.SourceSpan)).IsEqualTo("Holder.Result");
	}

	[Test]
	public async Task SupportedSuppressions_GivenEverySuppression_CoversCa1849Only()
	{
		// Arrange
		ZodSyncValidationSuppressor suppressor = new();

		// Act
		var suppressions = suppressor.SupportedSuppressions;

		// Assert
		await Assert.That(suppressions.Length).IsEqualTo(1);
		await Assert.That(suppressions[0].Id).IsEqualTo(ZodSyncValidationSuppressor.SuppressionId);
		await Assert
			.That(suppressions[0].SuppressedDiagnosticId)
			.IsEqualTo(ZodSyncValidationSuppressor.SuppressedDiagnosticId);
	}

	async Task<Compilation> CompileAsync(string source, CancellationToken cancellationToken)
	{
		using var result = await GenerateAsync(source, cancellationToken);
		return result.CompilationResult.Compilation;
	}

	static async Task<ImmutableArray<Diagnostic>> RunAnalyzersAsync(
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		CompilationWithAnalyzersOptions options = new(
			new AnalyzerOptions([]),
			onAnalyzerException: null,
			concurrentAnalysis: true,
			logAnalyzerExecutionTime: false,
			reportSuppressedDiagnostics: true
		);

		var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
			new FakeCa1849Analyzer(),
			new ZodSyncValidationSuppressor()
		);

		return await compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync(cancellationToken);
	}

	/// <summary>
	/// Reports CA1849 wherever a method has an <c>Async</c> sibling, so the tests do not depend on the real
	/// analyzer's Task-returning-context condition.
	/// </summary>
	/// <remarks>
	/// It reuses the real rule's id on purpose, and a test assembly is neither a packaged analyzer nor an
	/// <c>netstandard2.0</c> one, so the analyzer-authoring rules that would police a shipped component are
	/// silenced here.
	/// </remarks>
#pragma warning disable RS1026, RS1029, RS1032, RS1036, RS1038, RS1041, RS2008
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class FakeCa1849Analyzer : DiagnosticAnalyzer
	{
		static readonly DiagnosticDescriptor Rule = new(
			ZodSyncValidationSuppressor.SuppressedDiagnosticId,
			"Call async methods when in an async method",
			"In async method, call '{0}' instead of '{1}'",
			"Performance",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterOperationAction(Analyze, OperationKind.Invocation);
			context.RegisterOperationAction(AnalyzeBlockingMember, OperationKind.PropertyReference);
		}

		static void AnalyzeBlockingMember(OperationAnalysisContext context)
		{
			var reference = (IPropertyReferenceOperation)context.Operation;
			if (reference.Property.Name != "Result")
				return;

			context.ReportDiagnostic(Diagnostic.Create(Rule, reference.Syntax.GetLocation(), "Result", "Result"));
		}

		static void Analyze(OperationAnalysisContext context)
		{
			var invocation = (IInvocationOperation)context.Operation;
			var name = invocation.TargetMethod.Name;
			if (!HasAsyncSibling(invocation.TargetMethod.ContainingType, name + "Async"))
				return;

			context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), name + "Async", name));
		}

		static bool HasAsyncSibling(INamedTypeSymbol? type, string name)
		{
			for (var current = type; current is not null; current = current.BaseType)
			{
				if (current.GetMembers(name).OfType<IMethodSymbol>().Any())
					return true;
			}

			return false;
		}
	}
#pragma warning restore RS1026, RS1029, RS1032, RS1036, RS1038, RS1041, RS2008
}

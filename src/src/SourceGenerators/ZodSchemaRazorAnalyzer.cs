using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Reports ZODSGEN051 for a <c>[ZodSchema]</c> type declared in a Razor component. Razor compiles components
/// with its own source generator, and generators cannot see each other's output, so the ZodSharp generator
/// never sees the type. The type only exists in generated code, so — unlike <see cref="ZodSchemaAnalyzer"/> —
/// this analyzer analyzes generated code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZodSchemaRazorAnalyzer : DiagnosticAnalyzer
{
	public override System.Collections.Immutable.ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[DiagnosticLibrary.ZodSchemaInRazorFile];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(
			GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
		);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
	}

	static void AnalyzeNamedType(SymbolAnalysisContext context)
	{
		if (context.Symbol is not INamedTypeSymbol type)
			return;

		foreach (var location in type.Locations)
		{
			if (!location.IsInSource || !IsRazorFile(location.SourceTree?.FilePath))
				continue;

			if (!ZodSchemaAttributeData.FromAttributeData(type, out _).Exists)
				return;

			context.ReportDiagnostic(Diagnostic.Create(DiagnosticLibrary.ZodSchemaInRazorFile, location, type.Name));
			return;
		}
	}

	/// <summary>
	/// Whether a syntax tree comes from a Razor file: the <c>.razor</c> file itself (design time) or the Razor
	/// generator's output (<c>Pages_Counter_razor.g.cs</c>, <c>Counter.razor.g.cs</c>).
	/// </summary>
	internal static bool IsRazorFile(string? path) =>
		path is not null
		&& (
			path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(".razor.g.cs", StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith("_razor.g.cs", StringComparison.OrdinalIgnoreCase)
		);
}

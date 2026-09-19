using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZodSharp.AspNetCore.Analyzers;

/// <summary>
/// Reports when an <c>[ErrorType]</c> static field's containing class is not declared
/// <c>partial</c>, or when the field is not <c>static readonly</c>, so the generated
/// <c>Create</c>/<c>Throw</c> helpers cannot be emitted.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorTypePartialClassAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic id for an error type whose containing type is not partial.
	/// </summary>
	public const string DiagnosticId = "ZODSASP002";

	const string ErrorTypeAttributeMetadataName = "ZodSharp.AspNetCore.ErrorTypeAttribute";

	static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
	[
		DiagnosticLibrary.ErrorTypeContainingTypeNotPartial,
		DiagnosticLibrary.ErrorTypeFieldInvalid,
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			var errorTypeAttribute = compilationContext.Compilation.GetTypeByMetadataName(
				ErrorTypeAttributeMetadataName
			);
			if (errorTypeAttribute is null)
				return;

			compilationContext.RegisterSymbolAction(
				symbolContext => AnalyzeField(symbolContext, errorTypeAttribute),
				SymbolKind.Field
			);
		});
	}

	static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol errorTypeAttribute)
	{
		if (context.Symbol is not IFieldSymbol field)
			return;

		if (!HasAttribute(field, errorTypeAttribute))
			return;

		var location = GetMemberLocation(field);
		if (location is null)
			return;

		if (!field.IsStatic || !field.IsReadOnly)
		{
			context.ReportDiagnostic(Diagnostic.Create(DiagnosticLibrary.ErrorTypeFieldInvalid, location, field.Name));
			return;
		}

		var containing = field.ContainingType;
		if (
			containing is null
			|| containing.TypeKind != TypeKind.Class
			|| containing.TypeParameters.Length > 0
			|| containing.ContainingType is not null
		)
			return;

		if (!IsPartial(containing))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.ErrorTypeContainingTypeNotPartial,
					GetMemberLocation(containing) ?? location,
					containing.Name
				)
			);
		}
	}

	static bool HasAttribute(IFieldSymbol field, INamedTypeSymbol attribute) =>
		field.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

	static bool IsPartial(INamedTypeSymbol containing)
	{
		foreach (var reference in containing.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax() is ClassDeclarationSyntax classDeclaration
				&& classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
			)
				return true;
		}

		return false;
	}

	static Location? GetMemberLocation(ISymbol symbol)
	{
		foreach (var location in symbol.Locations)
		{
			if (location.IsInSource)
				return location;
		}

		return null;
	}
}

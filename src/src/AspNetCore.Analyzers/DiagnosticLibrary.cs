using Microsoft.CodeAnalysis;

namespace ZodSharp.AspNetCore.Analyzers;

/// <summary>
/// Diagnostic descriptors reported by the ZodSharp.AspNetCore analyzers.
/// </summary>
static class DiagnosticLibrary
{
	const string Category = "ZodSharp.AspNetCore";

	public static readonly DiagnosticDescriptor MessageFormatPlaceholderNotDeclared = new(
		id: "ZODSASP001",
		title: "MessageFormat placeholder is not declared in Parameters",
		messageFormat: "MessageFormat placeholder '{0}' is not declared in ErrorType.Parameters",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeContainingTypeNotPartial = new(
		id: "ZODSASP002",
		title: "Error type containing type must be partial",
		messageFormat: "The containing type '{0}' must be declared 'partial' so that Create/Throw methods can be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeFieldInvalid = new(
		id: "ZODSASP003",
		title: "ErrorType field must be static readonly",
		messageFormat: "The ErrorType field '{0}' must be declared 'static readonly' for Create/Throw methods to be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);
}

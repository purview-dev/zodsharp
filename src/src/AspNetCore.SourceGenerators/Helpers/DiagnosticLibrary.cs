using Microsoft.CodeAnalysis;

namespace ZodSharp.AspNetCore.SourceGenerators.Helpers;

/// <summary>
/// Diagnostic descriptors reported by the <see cref="ErrorTypeGenerator"/>.
/// </summary>
static class DiagnosticLibrary
{
	const string Category = "ZodSharp.AspNetCore";

	public static readonly DiagnosticDescriptor UnhandledException = new(
		id: "ZODSASP100",
		title: "Unhandled exception in the ErrorType source generator",
		messageFormat: "The ErrorType source generator failed for '{0}': {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InvalidParameters = new(
		id: "ZODSASP101",
		title: "Unable to extract ErrorType parameters",
		messageFormat: "The Parameters of ErrorType field '{0}' in '{1}' could not be extracted; only string collection literals are supported",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}

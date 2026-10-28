using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Suppresses CA1849 ("call async methods when in an async method") for ZodSharp's synchronous validation entry
/// points, <c>Validate</c> and <c>ValidateMember</c>.
/// </summary>
/// <remarks>
/// CA1849 reports every <c>Validate</c> call inside a Task-returning method because a <c>ValidateAsync</c> that
/// takes a superset of the parameters exists on the same type. That pairing is deliberate: the synchronous
/// pipeline is the cheap path (no <c>ValueTask</c>, no state machine) and every fluent schema completes it
/// synchronously. <c>ValidateAsync</c> earns its keep only when the type's rules are asynchronous, which
/// <c>RequiresAsyncValidation</c> and <c>ZODSGEN048</c> already say, and a synchronous call on such a type throws
/// rather than silently skipping them.
/// <para>
/// The diagnostic is reported at the call site, so no attribute on the declaring method can suppress it; a
/// <see cref="DiagnosticSuppressor"/> is the only mechanism a package can ship. It is deliberately narrow — an
/// unrelated library's <c>Validate</c>/<c>ValidateAsync</c> pair, and every genuine blocking call such as
/// <c>Task.Wait</c> or <c>Thread.Sleep</c>, still gets the warning.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZodSyncValidationSuppressor : DiagnosticSuppressor
{
	/// <summary>The suppression id, visible on the suppressed diagnostic's tooltip.</summary>
	public const string SuppressionId = "ZODSUP001";

	/// <summary>The only rule this suppressor covers.</summary>
	public const string SuppressedDiagnosticId = "CA1849";

	const string ValidateName = "Validate";
	const string ValidateMemberName = "ValidateMember";
	const string ValidateCoreName = "ValidateCore";

	/// <summary>The assembly a schema type has to come from, or from whose contract it has to be reached.</summary>
	public const string CoreAssemblyName = "Purview.ZodSharp";

	static readonly SuppressionDescriptor Rule = new(
		SuppressionId,
		SuppressedDiagnosticId,
		"Validate runs the whole synchronous rule set without blocking; ValidateAsync is the asynchronous path and is only required when the type's rules are asynchronous."
	);

	/// <inheritdoc/>
	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = [Rule];

	/// <inheritdoc/>
	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach (var diagnostic in context.ReportedDiagnostics)
		{
			if (!IsSuppressedRule(diagnostic) || diagnostic.Location.SourceTree is not { } tree)
				continue;

			var root = tree.GetRoot(context.CancellationToken);
			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

			// The diagnostic is reported on the invoked expression itself. Anything else — CA1849 also reports
			// blocking members such as Task.Wait() or Task.Result — is left alone; walking up to an enclosing
			// invocation would attribute it to the wrong call.
			if (node is not InvocationExpressionSyntax invocation)
				continue;

			var operation = context.GetSemanticModel(tree).GetOperation(invocation, context.CancellationToken);
			if (operation is IInvocationOperation { TargetMethod: { } target } && IsZodSynchronousValidation(target))
				context.ReportSuppression(Suppression.Create(Rule, diagnostic));
		}
	}

	static bool IsSuppressedRule(Diagnostic diagnostic) =>
		string.Equals(diagnostic.Id, SuppressedDiagnosticId, StringComparison.Ordinal);

	/// <summary>
	/// Whether the invoked method is one of ZodSharp's synchronous validators. Schema types declared in the package
	/// are recognised by their assembly; generated schemas live in the consuming assembly, so a static schema is
	/// recognised by the <c>ValidateCore</c> helper it pairs with and a generated validator adapter by the
	/// ZodSharp contract it implements.
	/// </summary>
	static bool IsZodSynchronousValidation(IMethodSymbol method)
	{
		var definition = method.ReducedFrom ?? method;
		if (definition.Name is not (ValidateName or ValidateMemberName))
			return false;

		var type = definition.ContainingType;
		if (type is null)
			return false;

		if (IsZodSharpAssembly(type.ContainingAssembly))
			return true;

		// The checks are an ordered chain of early returns; a conditional expression would obscure it.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (HasMethod(type, ValidateCoreName, 2))
			return true;
#pragma warning restore IDE0046

		return type.AllInterfaces.Any(static candidate => IsZodSharpAssembly(candidate.ContainingAssembly));
	}

	static bool HasMethod(INamedTypeSymbol type, string name, int parameterCount) =>
		type.GetMembers(name).OfType<IMethodSymbol>().Any(method => method.Parameters.Length == parameterCount);

	static bool IsZodSharpAssembly(IAssemblySymbol? assembly) =>
		assembly is not null
		&& (
			string.Equals(assembly.Name, CoreAssemblyName, StringComparison.Ordinal)
			|| assembly.Name.StartsWith(CoreAssemblyName + ".", StringComparison.Ordinal)
		);
}

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZodSharp.AspNetCore.Analyzers;

/// <summary>
/// Reports when an <c>ErrorType.MessageFormat</c> placeholder is not declared in the
/// <c>ErrorType.Parameters</c> list, so message templating gaps are caught at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorTypeMessageFormatAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic id for undeclared MessageFormat placeholders.
	/// </summary>
	public const string DiagnosticId = "ZODSASP001";

	const string ErrorTypeMetadataName = "ZodSharp.AspNetCore.ErrorType";

	static readonly Regex s_placeholderRegex = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

	static readonly DiagnosticDescriptor s_descriptor = new(
		id: DiagnosticId,
		title: "MessageFormat placeholder is not declared in Parameters",
		messageFormat: "MessageFormat placeholder '{0}' is not declared in ErrorType.Parameters",
		category: "ZodSharp.AspNetCore",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_descriptor];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(
			AnalyzeObjectCreation,
			Microsoft.CodeAnalysis.CSharp.SyntaxKind.ObjectCreationExpression,
			Microsoft.CodeAnalysis.CSharp.SyntaxKind.ImplicitObjectCreationExpression
		);
	}

	static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
	{
		if (
			context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol
			is not IMethodSymbol ctor
		)
			return;

		if (ctor.ContainingType?.ToDisplayString() != ErrorTypeMetadataName)
			return;

		var argumentList = context.Node switch
		{
			ObjectCreationExpressionSyntax objectCreation => objectCreation.ArgumentList,
			ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList,
			_ => null,
		};

		if (argumentList is null)
			return;

		var messageFormatExpression = FindMemberExpression(argumentList, ctor, "MessageFormat");
		var messageFormat = GetConstantString(
			context.SemanticModel,
			messageFormatExpression,
			context.CancellationToken
		);
		if (messageFormat is null)
			return;

		var declaredParameters = TryGetDeclaredParameters(
			context.SemanticModel,
			FindMemberExpression(argumentList, ctor, "Parameters"),
			context.CancellationToken
		);
		if (declaredParameters is null)
			return;

		var placeholders = s_placeholderRegex
			.Matches(messageFormat)
			.Cast<Match>()
			.Select(static m => m.Groups[1].Value)
			.Distinct(StringComparer.Ordinal);

		var location = messageFormatExpression!.GetLocation();
		foreach (var placeholder in placeholders)
		{
			if (!declaredParameters.Contains(placeholder))
				context.ReportDiagnostic(Diagnostic.Create(s_descriptor, location, placeholder));
		}
	}

	static ExpressionSyntax? FindMemberExpression(
		ArgumentListSyntax argumentList,
		IMethodSymbol ctor,
		string memberName
	)
	{
		for (var i = 0; i < argumentList.Arguments.Count; i++)
		{
			var argument = argumentList.Arguments[i];
			if (argument.NameColon is not null)
			{
				if (argument.NameColon.Name.Identifier.ValueText == memberName)
					return argument.Expression;

				continue;
			}

			if (i < ctor.Parameters.Length && ctor.Parameters[i].Name == memberName)
				return argument.Expression;
		}

		var initializer = argumentList.Parent switch
		{
			ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
			ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,
			_ => null,
		};

		if (initializer is null)
			return null;

		foreach (var element in initializer.Expressions)
		{
			if (
				element is AssignmentExpressionSyntax assignment
				&& assignment.Left is IdentifierNameSyntax identifier
				&& identifier.Identifier.ValueText == memberName
			)
			{
				return assignment.Right;
			}
		}

		return null;
	}

	static string? GetConstantString(
		SemanticModel semanticModel,
		ExpressionSyntax? expression,
		CancellationToken cancellationToken
	)
	{
		if (expression is null)
			return null;

		var constant = semanticModel.GetConstantValue(expression, cancellationToken);
		return constant.HasValue && constant.Value is string value ? value : null;
	}

	/// <summary>
	/// Returns the declared parameter names, an empty set when the argument is omitted, or
	/// <c>null</c> when the expression is present but not analyzable (analysis is skipped).
	/// </summary>
	static HashSet<string>? TryGetDeclaredParameters(
		SemanticModel semanticModel,
		ExpressionSyntax? expression,
		CancellationToken cancellationToken
	)
	{
		if (expression is null)
			return [];

		var elements = expression switch
		{
			CollectionExpressionSyntax collection => collection
				.Elements.OfType<ExpressionElementSyntax>()
				.Select(static element => element.Expression),
			ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
			ArrayCreationExpressionSyntax array => array.Initializer?.Expressions,
			_ => null,
		};

		if (elements is null)
			return null;

		HashSet<string> names = new(StringComparer.Ordinal);
		foreach (var element in elements)
		{
			if (GetConstantString(semanticModel, element, cancellationToken) is { } value)
				names.Add(value);
		}

		return names;
	}
}

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

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

	const string ErrorTypeMetadataName = "ZodSharp.Core.ErrorType";

	static readonly Regex s_placeholderRegex = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

	static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
	[
		DiagnosticLibrary.MessageFormatPlaceholderNotDeclared,
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
			var errorType = compilationContext.Compilation.GetTypeByMetadataName(ErrorTypeMetadataName);
			if (errorType is null)
				return;

			compilationContext.RegisterOperationAction(
				operationContext => AnalyzeObjectCreation(operationContext, errorType),
				OperationKind.ObjectCreation
			);
		});
	}

	static void AnalyzeObjectCreation(OperationAnalysisContext context, INamedTypeSymbol errorType)
	{
		if (context.Operation is not IObjectCreationOperation creation)
			return;

		if (!SymbolEqualityComparer.Default.Equals(creation.Type, errorType))
			return;

		if (GetArgumentValue(creation, "MessageFormat") is not string messageFormat)
			return;

		var declaredParameters = GetDeclaredParameters(creation);
		if (declaredParameters is null)
			return;

		var location = GetArgumentLocation(creation, "MessageFormat");
		if (location is null)
			return;

		var placeholders = s_placeholderRegex
			.Matches(messageFormat)
			.Cast<Match>()
			.Select(static m => m.Groups[1].Value)
			.Distinct(StringComparer.Ordinal);

		foreach (var placeholder in placeholders)
		{
			if (!declaredParameters.Contains(placeholder))
			{
				context.ReportDiagnostic(
					Diagnostic.Create(DiagnosticLibrary.MessageFormatPlaceholderNotDeclared, location, placeholder)
				);
			}
		}
	}

	static object? GetArgumentValue(IObjectCreationOperation creation, string parameterName)
	{
		foreach (var argument in creation.Arguments)
		{
			if (argument.Parameter?.Name != parameterName)
				continue;

			return UnwrapConversion(argument.Value).ConstantValue.Value;
		}

		return null;
	}

	static Location? GetArgumentLocation(IObjectCreationOperation creation, string parameterName)
	{
		foreach (var argument in creation.Arguments)
		{
			if (argument.Parameter?.Name == parameterName)
				return argument.Syntax.GetLocation();
		}

		return null;
	}

	/// <summary>
	/// Returns the declared parameter names, an empty set when the initializer is omitted, or
	/// <c>null</c> when the expression is present but not analyzable (analysis is skipped).
	/// </summary>
	static HashSet<string>? GetDeclaredParameters(IObjectCreationOperation creation)
	{
		// `Parameters` may be supplied as a constructor argument (the canonical form) or assigned
		// in an object initializer.
		foreach (var argument in creation.Arguments)
		{
			if (argument.ArgumentKind != ArgumentKind.Explicit)
				continue;

			if (argument.Parameter?.Name == "Parameters")
				return ExtractStrings(argument.Value);
		}

		if (creation.Initializer is null)
			return [];

		foreach (var initializer in creation.Initializer.Initializers)
		{
			if (
				initializer is ISimpleAssignmentOperation
				{
					Target: IPropertyReferenceOperation { Property.Name: "Parameters" },
				} assignment
			)
				return ExtractStrings(assignment.Value);
		}

		return [];
	}

	static HashSet<string>? ExtractStrings(IOperation operation)
	{
		operation = UnwrapConversion(operation);

#pragma warning disable format
		switch (operation)
		{
			case IArrayCreationOperation array when array.Initializer is not null:
			{
				HashSet<string> names = new(StringComparer.Ordinal);
				foreach (var element in array.Initializer.ElementValues)
					AddParameterName(element, names);

				return names;
			}

			case ICollectionExpressionOperation collection:
			{
				HashSet<string> names = new(StringComparer.Ordinal);
				foreach (var element in collection.Elements)
					AddParameterName(element, names);

				return names;
			}

			default:
				return null;
		}
#pragma warning restore format
	}

	static void AddParameterName(IOperation element, HashSet<string> names)
	{
		if (element is ISpreadOperation)
			return;

		// Target-typed `new(...)` collection elements arrive wrapped in an implicit conversion.
		element = UnwrapConversion(element);

		// A declared parameter is `new ErrorTypeParameter("OrderId", typeof(string))`; the name is
		// the first constructor argument.
		if (
			element is IObjectCreationOperation creation
			&& creation.Arguments.Length > 0
			&& creation.Arguments[0] is { Parameter.Name: "Name" } nameArgument
			&& UnwrapConversion(nameArgument.Value).ConstantValue.Value is string name
		)
			names.Add(name);
		else if (
			element is IInvocationOperation { TargetMethod.Name: "Param" } invocation
			&& IsErrorTypeParam(invocation.TargetMethod)
			&& invocation.Arguments.Length > 0
			&& UnwrapConversion(invocation.Arguments[0].Value).ConstantValue.Value is string paramName
		)
			names.Add(paramName);
		else if (element.ConstantValue.Value is string legacy)
			names.Add(legacy);
	}

	static bool IsErrorTypeParam(IMethodSymbol method) =>
		method.ContainingType?.Name == "ErrorType"
		&& method.ContainingType?.ContainingNamespace?.ToDisplayString() == "ZodSharp.Core";

	static IOperation UnwrapConversion(IOperation operation) =>
		operation is IConversionOperation { IsImplicit: true } conversion
			? UnwrapConversion(conversion.Operand)
			: operation;
}

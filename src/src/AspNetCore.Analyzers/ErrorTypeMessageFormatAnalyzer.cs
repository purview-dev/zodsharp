using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

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

		switch (operation)
		{
			case IArrayCreationOperation array when array.Initializer is not null:
				{
					HashSet<string> names = new(StringComparer.Ordinal);
					foreach (var element in array.Initializer.ElementValues)
					{
						if (UnwrapConversion(element).ConstantValue.Value is string value)
							names.Add(value);
					}

					return names;
				}

			case ICollectionExpressionOperation collection:
				{
					HashSet<string> names = new(StringComparer.Ordinal);
					foreach (var element in collection.Elements)
					{
						if (element is ISpreadOperation)
							continue;

						if (UnwrapConversion(element).ConstantValue.Value is string value)
							names.Add(value);
					}

					return names;
				}

			default:
				return null;
		}
	}

	static IOperation UnwrapConversion(IOperation operation) =>
		operation is IConversionOperation { IsImplicit: true } conversion
			? UnwrapConversion(conversion.Operand)
			: operation;
}

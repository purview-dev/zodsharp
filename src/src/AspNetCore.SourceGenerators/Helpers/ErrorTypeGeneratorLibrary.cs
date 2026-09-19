using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.AspNetCore.SourceGenerators.Models;

namespace ZodSharp.AspNetCore.SourceGenerators.Helpers;

/// <summary>
/// Builds the value-equatable <see cref="ErrorTypeFieldModel"/> from attribute-annotated fields.
/// </summary>
static class ErrorTypeGeneratorLibrary
{
	const string ErrorTypeMetadataName = "ZodSharp.AspNetCore.ErrorType";

	public static GeneratorResult<ErrorTypeFieldModel> CreateModel(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		if (context.TargetSymbol is not IFieldSymbol field)
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		if (!field.IsStatic)
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		var errorTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(ErrorTypeMetadataName);
		if (errorTypeSymbol is null || !SymbolEqualityComparer.Default.Equals(field.Type, errorTypeSymbol))
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		var containing = field.ContainingType;
		if (
			containing is null
			|| containing.TypeKind != TypeKind.Class
			|| containing.TypeParameters.Length > 0
			|| containing.ContainingType is not null
		)
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		if (context.TargetNode is not VariableDeclaratorSyntax declarator)
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		var classDeclaration = declarator.FirstAncestorOrSelf<ClassDeclarationSyntax>();
		if (
			classDeclaration is null
			|| !classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword))
		)
			return GeneratorResult<ErrorTypeFieldModel>.Empty;

		var @namespace = containing.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: containing.ContainingNamespace.ToDisplayString();

		var parametersResult = ExtractParameters(declarator.Initializer?.Value, context, cancellationToken);
		if (!parametersResult.HasValue)
		{
			return GeneratorResult<ErrorTypeFieldModel>.Create([
				ReportableDiagnostic.Create(
					DiagnosticLibrary.InvalidParameters,
					isBlocking: true,
					declarator.GetLocation(),
					[field.Name, containing.Name]
				),
			]);
		}

		var parameters = parametersResult.Value;

		return GeneratorResult<ErrorTypeFieldModel>.Create(
			new ErrorTypeFieldModel(
				@namespace,
				containing.Name,
				containing.DeclaredAccessibility.ToTypeDeclarationAccessibility()
					?? TypeDeclarationAccessibility.Internal,
				containing.IsStatic,
				containing.IsAbstract && !containing.IsStatic,
				containing.IsSealed && !containing.IsStatic,
				field.Name,
				new EquatableArray<string>(parameters)
			)
		);
	}

	/// <summary>
	/// Extracts the declared <c>Parameters</c> from the field initializer. Returns <c>null</c>
	/// when a <c>Parameters</c> member is present but cannot be statically analysed.
	/// </summary>
	static ImmutableArray<string>? ExtractParameters(
		ExpressionSyntax? initializer,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		if (initializer is null)
			return [];

		var objectInitializer = initializer switch
		{
			ObjectCreationExpressionSyntax objectCreation => objectCreation.Initializer,
			ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.Initializer,
			_ => null,
		};

		if (objectInitializer is null)
			return [];

		var parametersExpression = FindParametersExpression(objectInitializer);
		if (parametersExpression is null)
			return [];

		var elements = CollectionElements(parametersExpression);
		if (elements is null)
			return null;

		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var element in elements)
		{
			var constant = context.SemanticModel.GetConstantValue(element, cancellationToken);
			if (constant.HasValue && constant.Value is string value)
				builder.Add(value);
		}

		return builder.ToImmutable();
	}

	static ExpressionSyntax? FindParametersExpression(InitializerExpressionSyntax objectInitializer)
	{
		foreach (var element in objectInitializer.Expressions)
		{
			if (
				element is AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } assignment
				&& identifier.Identifier.ValueText == "Parameters"
			)
				return assignment.Right;
		}

		return null;
	}

	/// <summary>
	/// Returns the collection elements for recognised collection syntax, or <c>null</c> when the
	/// expression is not a statically-analysable collection literal.
	/// </summary>
	static IEnumerable<ExpressionSyntax>? CollectionElements(ExpressionSyntax expression)
	{
		return expression switch
		{
			CollectionExpressionSyntax collection => collection
				.Elements.OfType<ExpressionElementSyntax>()
				.Select(static element => element.Expression),
			ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
			ArrayCreationExpressionSyntax array => array.Initializer?.Expressions ?? [],
			_ => null,
		};
	}
}

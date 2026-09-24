using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Builds the value-equatable <see cref="ErrorTypeFieldModel"/> from attribute-annotated fields.
/// </summary>
static class ErrorTypeGeneratorLibrary
{
	const string ErrorTypeMetadataName = "ZodSharp.Core.ErrorType";

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
					DiagnosticLibrary.ErrorTypeInvalidParameters,
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
				new EquatableArray<ErrorTypeParameterModel>(parameters)
			)
		);
	}

	/// <summary>
	/// Extracts the declared <c>Parameters</c> from the field initializer. Returns <c>null</c>
	/// when a <c>Parameters</c> member is present but cannot be statically analysed.
	/// </summary>
	static ImmutableArray<ErrorTypeParameterModel>? ExtractParameters(
		ExpressionSyntax? initializer,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		if (initializer is not BaseObjectCreationExpressionSyntax creation)
			return [];

		// `Parameters` may be supplied as a constructor argument (the canonical form) or assigned
		// in an object initializer.
		var parametersExpression =
			FindParametersArgument(creation, context, cancellationToken)?.Expression
			?? FindParametersExpression(creation.Initializer);
		if (parametersExpression is null)
			return [];

		var elements = CollectionElements(parametersExpression);
		if (elements is null)
			return null;

		var builder = ImmutableArray.CreateBuilder<ErrorTypeParameterModel>();
		foreach (var element in elements)
		{
			if (!TryExtractParameter(element, context, cancellationToken, out var parameter))
				return null;

			builder.Add(parameter);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Returns the constructor argument bound to the <c>Parameters</c> parameter, or <c>null</c>
	/// when none is supplied. Matches both the named (<c>Parameters:</c>) and positional forms.
	/// </summary>
	static ArgumentSyntax? FindParametersArgument(
		BaseObjectCreationExpressionSyntax creation,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		var argumentList = creation.ArgumentList;
		if (argumentList is null)
			return null;

		if (context.SemanticModel.GetSymbolInfo(creation, cancellationToken).Symbol is not IMethodSymbol constructor)
			return null;

		var positionalIndex = 0;
		foreach (var argument in argumentList.Arguments)
		{
			if (argument.NameColon is null)
			{
				if (
					positionalIndex < constructor.Parameters.Length
					&& constructor.Parameters[positionalIndex] is { Name: "Parameters" }
				)
					return argument;

				positionalIndex++;
				continue;
			}

			foreach (var parameter in constructor.Parameters)
			{
				if (parameter.Name == argument.NameColon.Name.Identifier.ValueText && parameter.Name == "Parameters")
					return argument;
			}
		}

		return null;
	}

	/// <summary>
	/// Parses a single parameter declaration into a value-equatable <see cref="ErrorTypeParameterModel"/>.
	/// Supports <c>new ErrorTypeParameter("Name", typeof(T))</c>, target-typed
	/// <c>new("Name", typeof(T))</c>, and <c>ErrorType.Param&lt;T&gt;("Name")</c>. Returns
	/// <c>false</c> when the element is not a statically-analysable declaration.
	/// </summary>
	static bool TryExtractParameter(
		ExpressionSyntax element,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken,
		out ErrorTypeParameterModel parameter
	)
	{
		if (!TryExtractParameterNameAndType(element, context, cancellationToken, out var name, out var type))
		{
			parameter = default;
			return false;
		}

		if (!TryGetParameterType(type, out var identity, out var isNullable, out var arrayRank))
		{
			parameter = default;
			return false;
		}

		parameter = new ErrorTypeParameterModel(name, identity, isNullable, arrayRank);
		return true;
	}

	/// <summary>
	/// Resolves the declared name and type for a recognised parameter declaration form.
	/// </summary>
	static bool TryExtractParameterNameAndType(
		ExpressionSyntax element,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken,
		out string name,
		out ITypeSymbol type
	)
	{
		switch (element)
		{
			case ObjectCreationExpressionSyntax { ArgumentList: { } named } creation
				when IsErrorTypeParameter(creation.Type, context):
				return TryExtractConstructorArguments(named.Arguments, context, cancellationToken, out name, out type);

			case ImplicitObjectCreationExpressionSyntax { ArgumentList: { } implicitNamed }:
				return TryExtractConstructorArguments(
					implicitNamed.Arguments,
					context,
					cancellationToken,
					out name,
					out type
				);

			case InvocationExpressionSyntax invocation:
				return TryExtractParamInvocation(invocation, context, cancellationToken, out name, out type);

			default:
				name = null!;
				type = null!;
				return false;
		}
	}

	/// <summary>
	/// Resolves <c>new ErrorTypeParameter("Name", typeof(T))</c> from its constructor arguments.
	/// </summary>
	static bool TryExtractConstructorArguments(
		SeparatedSyntaxList<ArgumentSyntax> arguments,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken,
		out string name,
		out ITypeSymbol type
	)
	{
		name = null!;
		type = null!;

		if (arguments is not { Count: >= 2 } args)
			return false;

		var nameConstant = context.SemanticModel.GetConstantValue(args[0].Expression, cancellationToken);
		if (!nameConstant.HasValue || nameConstant.Value is not string nameValue)
			return false;

		if (args[1].Expression is not TypeOfExpressionSyntax typeOf)
			return false;

		var typeInfo = context.SemanticModel.GetTypeInfo(typeOf.Type, cancellationToken);
		if (typeInfo.Type is null)
			return false;

		name = nameValue;
		type = typeInfo.Type;
		return true;
	}

	/// <summary>
	/// Resolves <c>ErrorType.Param&lt;T&gt;("Name")</c> from its generic type argument.
	/// </summary>
	static bool TryExtractParamInvocation(
		InvocationExpressionSyntax invocation,
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken,
		out string name,
		out ITypeSymbol type
	)
	{
		name = null!;
		type = null!;

		if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
			return false;

		var errorType = context.SemanticModel.Compilation.GetTypeByMetadataName(ErrorTypeMetadataName);
		if (
			errorType is null
			|| method.Name != "Param"
			|| !SymbolEqualityComparer.Default.Equals(method.ContainingType, errorType)
		)
			return false;

		if (invocation.ArgumentList.Arguments is not { Count: >= 1 } args)
			return false;

		var nameConstant = context.SemanticModel.GetConstantValue(args[0].Expression, cancellationToken);
		if (!nameConstant.HasValue || nameConstant.Value is not string nameValue)
			return false;

		if (method.TypeArguments.Length != 1)
			return false;

		name = nameValue;
		type = method.TypeArguments[0];
		return true;
	}

	static bool IsErrorTypeParameter(TypeSyntax typeSyntax, GeneratorAttributeSyntaxContext context)
	{
		var type = context.SemanticModel.GetTypeInfo(typeSyntax).Type as INamedTypeSymbol;
		return type is not null
			&& type.Name == "ErrorTypeParameter"
			&& type.ContainingNamespace?.ToDisplayString() == "ZodSharp.Core";
	}

	/// <summary>
	/// Resolves a <c>typeof(...)</c> argument into the value-equatable components needed to emit its
	/// type reference: the underlying <c>TypeIdentity</c> (nullable value types unwrapped),
	/// whether it is <c>Nullable&lt;T&gt;</c>, and the array rank. Returns <c>false</c> when the
	/// symbol cannot be represented.
	/// </summary>
	static bool TryGetParameterType(ITypeSymbol type, out TypeIdentity identity, out bool isNullable, out int arrayRank)
	{
		isNullable = false;
		arrayRank = 0;

		if (type is IArrayTypeSymbol arrayType)
		{
			if (!TryGetParameterType(arrayType.ElementType, out identity, out isNullable, out _))
				return false;

			arrayRank = arrayType.Rank;
			return true;
		}

		if (
			type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			&& nullable.TypeArguments[0] is INamedTypeSymbol underlying
		)
		{
			isNullable = true;
			type = underlying;
		}

		if (!TypeIdentity.TryCreate(type, out identity))
			return false;

		// We don't support nullable reference types because they are not represented in the emitted
		return true;
	}

	static ExpressionSyntax? FindParametersExpression(InitializerExpressionSyntax? objectInitializer)
	{
		if (objectInitializer is null)
			return null;

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
			ObjectCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
			ImplicitObjectCreationExpressionSyntax { Initializer: { } implicitInitializer } =>
				implicitInitializer.Expressions,
			_ => null,
		};
	}
}

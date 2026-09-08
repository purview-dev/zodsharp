using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

partial class SourceGenLibrary
{
	/// <summary>
	/// Reads the <c>CustomValidationMethodName</c> property from the <c>[ZodSchema]</c> attribute,
	/// discovers and validates the matching method on the schema type, and returns
	/// an immutable <see cref="CustomValidationMethodData"/>.
	/// </summary>
	internal static GeneratorResult<CustomValidationMethodData> ResolveCustomValidationMethod(
		INamedTypeSymbol classSymbol,
		ZodSchemaAttributeData zodSchemaAttributeData,
		AttributeData zodSchemaAttribute
	)
	{
		var configuredName = zodSchemaAttributeData.CustomValidationMethodName;
		var isExplicitlyConfigured = !string.IsNullOrEmpty(configuredName);

		var methodName = string.IsNullOrWhiteSpace(configuredName)
			? TypeLibraryGenerator.DefaultCustomValidationMethodName
			: configuredName!;

		// Validate the configured name is a valid C# identifier.
		if (isExplicitlyConfigured && !IsValidIdentifier(methodName))
		{
			return GeneratorResult<CustomValidationMethodData>.Create(
				CustomValidationMethodData.None,
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidMethodName,
					zodSchemaAttribute,
					methodName,
					classSymbol.Name
				)
			);
		}

		// Discover candidate methods declared directly on the type (not inherited).
		var candidates = GetMethodsByName(classSymbol, methodName);
		var validationMethodClassSymbol = classSymbol;
		if (candidates.Count == 0)
		{
			var (s, c) = FindCandidatesFromSchemaValidator(classSymbol, methodName);
			validationMethodClassSymbol = s;

			if (c.Count == 0)
			{
				// No method found, checking the model and the schema validator
				// ...but only report a diagnostic if explicitly configured.
				if (isExplicitlyConfigured)
				{
					return GeneratorResult<CustomValidationMethodData>.Create(
						CustomValidationMethodData.None,
						DiagnosticInfo.Create(
							DiagnosticLibrary.CustomValidationMethodNotFound,
							GetAttributeLocation(zodSchemaAttribute, classSymbol),
							methodName,
							classSymbol.Name
						)
					);
				}
			}

			if (validationMethodClassSymbol == null)
			{
				// We checked the methods on the model and on the schema validator...
				return CustomValidationMethodData.None;
			}

			// Set the current candidates to the one(s) we found on the schema.
			candidates = c;
		}

		var invocationKind = SymbolEqualityComparer.Default.Equals(validationMethodClassSymbol, classSymbol)
			? CustomValidationInvocationKind.StaticOnModelType
			: CustomValidationInvocationKind.DefinedOnSchemaValidator;

		// Validate each candidate and collect valid ones + diagnostics.
		var validCandidates = new List<IMethodSymbol>();
		var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
		foreach (var candidate in candidates)
		{
			var isValid = ValidateMethodSignature(candidate, classSymbol, invocationKind, diagnostics);
			if (isValid)
				validCandidates.Add(candidate);
		}

		if (validCandidates.Count == 0)
		{
			return GeneratorResult<CustomValidationMethodData>.Create(
				CustomValidationMethodData.None,
				diagnostics.ToImmutable()
			);
		}

		if (validCandidates.Count > 1)
		{
			// Ambiguous — multiple valid overloads.
			return GeneratorResult<CustomValidationMethodData>.Create(
				CustomValidationMethodData.None,
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationAmbiguousOverloads,
					validCandidates[0].Locations.Length > 0 ? validCandidates[0].Locations[0] : null,
					methodName,
					classSymbol.Name
				)
			);
		}

		// Exactly one valid method.
		return new CustomValidationMethodData(
			IsConfigured: true,
			Exists: true,
			IsValid: true,
			MethodName: methodName,
			InvocationKind: invocationKind
		);
	}

	static (INamedTypeSymbol?, List<IMethodSymbol>) FindCandidatesFromSchemaValidator(
		INamedTypeSymbol classSymbol,
		string validationMethodName
	)
	{
		var schemaSymbol = classSymbol.ContainingType is not null
			? classSymbol.ContainingType.GetTypeMembers($"{classSymbol.Name}SchemaValidator").FirstOrDefault()
			: classSymbol.ContainingNamespace.GetTypeMembers($"{classSymbol.Name}SchemaValidator").FirstOrDefault();
		if (schemaSymbol == null)
			return (null, []);

		return (
			schemaSymbol,
			[
				.. schemaSymbol
					.GetMembers(validationMethodName)
					.Where(static m => m is IMethodSymbol)
					.Cast<IMethodSymbol>(),
			]
		);
	}

	static Location? GetAttributeLocation(AttributeData? attributeData, INamedTypeSymbol classSymbol)
	{
		if (attributeData?.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
			return syntax.GetLocation();

		// Fallback: return the first location of the class symbol (e.g. the class declaration).
		return classSymbol.Locations.Length > 0 ? classSymbol.Locations[0] : null;
	}

	static List<IMethodSymbol> GetMethodsByName(INamedTypeSymbol classSymbol, string methodName) =>
		[
			.. classSymbol
				.GetMembers(methodName)
				.OfType<IMethodSymbol>()
				.Where(static m => m.MethodKind == MethodKind.Ordinary && !m.IsImplicitlyDeclared),
		];

	static bool ValidateMethodSignature(
		IMethodSymbol method,
		INamedTypeSymbol classSymbol,
		CustomValidationInvocationKind invocationKind,
		ImmutableArray<DiagnosticInfo>.Builder diagnostics
	)
	{
		var typeName = classSymbol.Name;
		var methodLocation = method.Locations.Length > 0 ? method.Locations[0] : null;
		var comparer = SymbolEqualityComparer.Default;

		// Must not be generic.
		if (method.IsGenericMethod)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationGenericMethod,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Must not be abstract.
		if (method.IsAbstract)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationAbstractMethod,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Must not be an unimplemented partial method.
		if (method.PartialDefinitionPart is not null && method.PartialImplementationPart is null)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationUnimplementedPartial,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}
		else if (method.IsPartialDefinition && method.PartialImplementationPart is null)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationUnimplementedPartial,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Must be static — the generated validator is a separate type, not a partial of the model.
		if (invocationKind == CustomValidationInvocationKind.StaticOnModelType && !method.IsStatic)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidStaticInstance,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Check parameter modifiers (ref, in, out, params, scoped).
		foreach (var param in method.Parameters)
		{
			if (
				param.RefKind is RefKind.Ref or RefKind.In or RefKind.Out
				|| param.IsParams
				|| param.ScopedKind != ScopedKind.None
			)
			{
				diagnostics.Add(
					DiagnosticInfo.Create(
						DiagnosticLibrary.CustomValidationInvalidParameterModifier,
						methodLocation,
						method.Name,
						typeName
					)
				);
				break;
			}
		}

		// Must have exactly two parameters.
		if (method.Parameters.Length != 2)
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidParameterCount,
					methodLocation,
					method.Name,
					typeName
				)
			);

			// Can't validate individual params if count is wrong; return early.
			return false;
		}

		// First parameter must be the schema model type.
		var firstParam = method.Parameters[0];
		if (!TypeHelpers.IsSameType(firstParam.Type, classSymbol, comparer))
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidModelParameter,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Second parameter must be CancellationToken.
		var secondParam = method.Parameters[1];
		if (!TypeLibraryGenerator.CancellationToken.Equals(secondParam.Type))
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidCancellationToken,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Return type must be ValueTask<ValidationResult<T>>.
		var expectedReturnType = GetExpectedReturnType(classSymbol);
		var expectedReturnTypeName = expectedReturnType.ToString();
		var actualReturnTypeName = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		if (!string.Equals(expectedReturnTypeName, actualReturnTypeName, StringComparison.Ordinal))
		{
			diagnostics.Add(
				DiagnosticInfo.Create(
					DiagnosticLibrary.CustomValidationInvalidReturnType,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		if (invocationKind == CustomValidationInvocationKind.StaticOnModelType)
		{
			// Accessibility check — the generated validator is in the same assembly + namespace,
			// so internal and public are accessible. Private is not.
			if (method.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
			{
				diagnostics.Add(
					DiagnosticInfo.Create(
						DiagnosticLibrary.CustomValidationInaccessible,
						methodLocation,
						method.Name,
						typeName
					)
				);
			}
		}

		return diagnostics.Count == 0;
	}

	/// <summary>
	/// Constructs the expected <c>ValueTask&lt;ValidationResult&lt;T&gt;&gt;</c> symbol
	/// from the compilation's framework symbols.
	/// </summary>
	static TypeIdentity GetExpectedReturnType(INamedTypeSymbol classSymbol) =>
		TypeLibraryGenerator.ValueTask.MakeGeneric(
			TypeLibrary.ZodSharp.Core.ValidationResult.MakeGeneric(
				classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			)
		);

	static bool IsValidIdentifier(string name) =>
		!string.IsNullOrWhiteSpace(name) && SyntaxFacts.IsValidIdentifier(name);
}

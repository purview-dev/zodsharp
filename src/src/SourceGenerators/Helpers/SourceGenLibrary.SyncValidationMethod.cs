using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

partial class SourceGenLibrary
{
	/// <summary>
	/// Reads the <c>RefinementMethodName</c> property from the <c>[ZodSchema]</c> attribute,
	/// discovers and validates the matching synchronous refinement method on the schema type,
	/// and returns an immutable <see cref="SyncValidationMethodData"/>.
	/// </summary>
	/// <remarks>
	/// The refinement method is an instance method named <c>Validate</c> by default (or
	/// <c>RefinementMethodName</c> when explicitly configured) with the signature
	/// <c>IEnumerable&lt;ValidationError&gt; Method()</c> or
	/// <c>IEnumerable&lt;ValidationError&gt; Method(RefineCtx&lt;T&gt; ctx)</c>. It is invoked by the
	/// generated synchronous <c>Validate</c> and its errors are merged with the attribute-based errors.
	/// </remarks>
	internal static GeneratorResult<SyncValidationMethodData> ResolveSyncValidationMethod(
		INamedTypeSymbol classSymbol,
		ZodSchemaAttributeData zodSchemaAttributeData,
		AttributeData zodSchemaAttribute
	)
	{
		var configuredName = zodSchemaAttributeData.RefinementMethodName;
		var isExplicitlyConfigured = !string.IsNullOrEmpty(configuredName);

		var methodName = string.IsNullOrWhiteSpace(configuredName)
			? TypeLibraryGenerator.DefaultSyncValidationMethodName
			: configuredName!;

		// Validate the configured name is a valid C# identifier.
		if (isExplicitlyConfigured && !IsValidIdentifier(methodName))
		{
			return GeneratorResult<SyncValidationMethodData>.Create(
				SyncValidationMethodData.None,
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationInvalidMethodName,
					true,
					zodSchemaAttribute,
					methodName,
					classSymbol.Name
				)
			);
		}

		// Discover candidate methods declared directly on the type (not inherited).
		var candidates = GetMethodsByName(classSymbol, methodName);
		if (candidates.Count == 0)
		{
			// No method found — only report a diagnostic if explicitly configured.
			if (isExplicitlyConfigured)
			{
				return GeneratorResult<SyncValidationMethodData>.Create(
					SyncValidationMethodData.None,
					ReportableDiagnostic.Create(
						DiagnosticLibrary.CustomValidationMethodNotFound,
						true,
						GetAttributeLocation(zodSchemaAttribute, classSymbol),
						methodName,
						classSymbol.Name
					)
				);
			}

			// No method found and not explicitly configured — this is valid, so return a "none" result.
			return SyncValidationMethodData.None;
		}

		// Validate each candidate and collect valid ones + diagnostics.
		List<IMethodSymbol> validCandidates = [];
		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		foreach (var candidate in candidates)
		{
			if (ValidateSyncMethodSignature(candidate, classSymbol, diagnostics))
				validCandidates.Add(candidate);
		}

		if (validCandidates.Count == 0)
		{
			return GeneratorResult<SyncValidationMethodData>.Create(
				SyncValidationMethodData.None,
				diagnostics.ToImmutable()
			);
		}

		if (validCandidates.Count > 1)
		{
			// Ambiguous — multiple valid overloads.
			return GeneratorResult<SyncValidationMethodData>.Create(
				SyncValidationMethodData.None,
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationAmbiguousOverloads,
					true,
					validCandidates[0].Locations.Length > 0 ? validCandidates[0].Locations[0] : null,
					methodName,
					classSymbol.Name
				)
			);
		}

		// Exactly one valid method.
		var validMethod = validCandidates[0];
		return new SyncValidationMethodData(
			IsConfigured: true,
			Exists: true,
			IsValid: true,
			MethodName: methodName,
			InvocationKind: validMethod.Parameters.Length == 1
				? SyncValidationInvocationKind.WithRefineContext
				: SyncValidationInvocationKind.Parameterless
		);
	}

	static bool ValidateSyncMethodSignature(
		IMethodSymbol method,
		INamedTypeSymbol classSymbol,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		var typeName = classSymbol.Name;
		var methodLocation = method.Locations.Length > 0 ? method.Locations[0] : null;

		// Must not be generic.
		if (method.IsGenericMethod)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationGenericMethod,
					true,
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
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationAbstractMethod,
					true,
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
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationUnimplementedPartial,
					true,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}
		else if (method.IsPartialDefinition && method.PartialImplementationPart is null)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.CustomValidationUnimplementedPartial,
					false,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Must be an instance method — the generated schema invokes it on the value.
		if (method.IsStatic)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.SyncValidationInvalidStaticInstance,
					true,
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
					ReportableDiagnostic.Create(
						DiagnosticLibrary.CustomValidationInvalidParameterModifier,
						false,
						methodLocation,
						method.Name,
						typeName
					)
				);
				break;
			}
		}

		// Must have zero parameters or a single RefineCtx<T> parameter.
		if (method.Parameters.Length > 1)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.SyncValidationInvalidParameterCount,
					true,
					methodLocation,
					method.Name,
					typeName
				)
			);

			// Can't validate individual params if count is wrong; return early.
			return false;
		}

		if (method.Parameters.Length == 1 && !IsRefineContextOf(method.Parameters[0].Type, classSymbol))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.SyncValidationInvalidContextParameter,
					true,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Return type must be assignable to IEnumerable<ValidationError>.
		if (!ReturnsEnumerableOfValidationError(method))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.SyncValidationInvalidReturnType,
					true,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		// Accessibility — the generated validator is in the same assembly + namespace, so
		// internal and public are accessible. Private and protected are not.
		if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.SyncValidationInaccessible,
					false,
					methodLocation,
					method.Name,
					typeName
				)
			);
		}

		return diagnostics.Count == 0;
	}

	static bool IsRefineContextOf(ITypeSymbol type, INamedTypeSymbol classSymbol)
	{
		if (
			type is not INamedTypeSymbol named
			|| named.MetadataName != "RefineCtx`1"
			|| named.ContainingNamespace.ToDisplayString() != TypeLibraryGenerator.ZodSharpSchemasNamespace
			|| named.TypeArguments.Length != 1
		)
		{
			return false;
		}

		// The type argument must match the schema type.
		return SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], classSymbol);
	}

	static bool ReturnsEnumerableOfValidationError(IMethodSymbol method)
	{
		var returnType = method.ReturnType;
		if (returnType is IArrayTypeSymbol arrayType)
			return IsValidationErrorType(arrayType.ElementType);

		if (
			returnType is INamedTypeSymbol { OriginalDefinition.MetadataName: "IEnumerable`1" } named
			&& named.TypeArguments.Length == 1
		)
		{
			return IsValidationErrorType(named.TypeArguments[0]);
		}

		foreach (var iface in returnType.AllInterfaces)
		{
			if (
				iface is INamedTypeSymbol { OriginalDefinition.MetadataName: "IEnumerable`1" } enumerable
				&& enumerable.TypeArguments.Length == 1
				&& IsValidationErrorType(enumerable.TypeArguments[0])
			)
			{
				return true;
			}
		}

		return false;
	}

	static bool IsValidationErrorType(ITypeSymbol type) =>
		type is INamedTypeSymbol { MetadataName: "ValidationError" } named
		&& named.ContainingNamespace.ToDisplayString() == TypeLibraryGenerator.ZodSharpCoreNamespace;
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZodSchemaAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
	[
		DiagnosticLibrary.InvalidLengthAttribute,
		DiagnosticLibrary.UnsupportedLengthAttributeTarget,
		DiagnosticLibrary.InvalidDataAnnotationsErrorMessage,
		DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
		DiagnosticLibrary.CustomValidationMethodNotFound,
		DiagnosticLibrary.CustomValidationInvalidReturnType,
		DiagnosticLibrary.CustomValidationInvalidParameterCount,
		DiagnosticLibrary.CustomValidationInvalidModelParameter,
		DiagnosticLibrary.CustomValidationInvalidCancellationToken,
		DiagnosticLibrary.CustomValidationGenericMethod,
		DiagnosticLibrary.CustomValidationInvalidStaticInstance,
		DiagnosticLibrary.CustomValidationInaccessible,
		DiagnosticLibrary.CustomValidationAmbiguousOverloads,
		DiagnosticLibrary.CustomValidationInvalidMethodName,
		DiagnosticLibrary.CustomValidationAbstractMethod,
		DiagnosticLibrary.CustomValidationUnimplementedPartial,
		DiagnosticLibrary.CustomValidationInvalidParameterModifier,
		DiagnosticLibrary.ComparePropertyNotFound,
		DiagnosticLibrary.DataAnnotationsReferenceNotFound,
		DiagnosticLibrary.SyncValidationInvalidReturnType,
		DiagnosticLibrary.SyncValidationInvalidParameterCount,
		DiagnosticLibrary.SyncValidationInvalidStaticInstance,
		DiagnosticLibrary.SyncValidationInaccessible,
		DiagnosticLibrary.SyncValidationInvalidContextParameter,
		DiagnosticLibrary.IValidateOptionsReferenceNotFound,
		DiagnosticLibrary.IValidateOptionsValueTypeTarget,
		DiagnosticLibrary.AmbiguousValidationMethods,
		DiagnosticLibrary.UnsupportedCustomRuleTarget,
		DiagnosticLibrary.UnmappableCustomRuleArgument,
		DiagnosticLibrary.RuleAttributeWithoutSchema,
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
			var hasDataAnnotations = TypeHelpers.HasType(
				compilationContext.Compilation,
				TypeLibrary.System.ComponentModel.DataAnnotations.RequiredAttribute
			);
			var hasIValidateOptions =
				compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Options.IValidateOptions`1")
				is not null;

			ExternalSchemaResolver externalSchemas = new(compilationContext.Compilation);

			// Types that will receive a generated schema: [ZodSchema] roots plus, transitively, the complex
			// property types the generator discovers and emits secondary schemas for.
			var schemaReachableTypes = BuildSchemaReachableTypes(compilationContext.Compilation, externalSchemas);

			compilationContext.RegisterSymbolAction(
				symbolContext =>
					AnalyzeNamedType(
						symbolContext,
						hasDataAnnotations,
						hasIValidateOptions,
						externalSchemas,
						schemaReachableTypes
					),
				SymbolKind.NamedType
			);
		});
	}

	static void AnalyzeNamedType(
		SymbolAnalysisContext context,
		bool hasDataAnnotations,
		bool hasIValidateOptions,
		ExternalSchemaResolver externalSchemas,
		ImmutableHashSet<TypeIdentity> schemaReachableTypes
	)
	{
		if (context.Symbol is not INamedTypeSymbol type)
			return;

		ReportRuleAttributesWithoutSchema(context, type, schemaReachableTypes);

		var zodSchemaData = ZodSchemaAttributeData.FromAttributeData(type, out var zodSchemaAttribute);
		if (!zodSchemaData.Exists)
			return;

		var typeLocation = GetTypeLocation(type);

		if (!hasDataAnnotations)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(DiagnosticLibrary.DataAnnotationsReferenceNotFound, typeLocation)
			);
			return;
		}

		var customValidationResult = SourceGenLibrary.ResolveCustomValidationMethod(
			type,
			zodSchemaData,
			zodSchemaAttribute!
		);
		foreach (var diagnosticInfo in customValidationResult.Diagnostics)
			context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());

		var syncValidationResult = SourceGenLibrary.ResolveSyncValidationMethod(
			type,
			zodSchemaData,
			zodSchemaAttribute!
		);
		foreach (var diagnosticInfo in syncValidationResult.Diagnostics)
			context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());

		// A model may declare either a synchronous refinement method or an async custom
		// validation method, but not both.
		if (customValidationResult.Value.HasCustomValidation && syncValidationResult.Value.HasSyncValidation)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.AmbiguousValidationMethods,
					typeLocation,
					type.Name,
					syncValidationResult.Value.MethodName,
					customValidationResult.Value.MethodName
				)
			);
		}

		if (zodSchemaData.GenerateIValidateOptions == true)
		{
			if (!hasIValidateOptions)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(DiagnosticLibrary.IValidateOptionsReferenceNotFound, typeLocation, type.Name)
				);
			}
			else if (type.TypeKind == TypeKind.Struct)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(DiagnosticLibrary.IValidateOptionsValueTypeTarget, typeLocation, type.Name)
				);
			}
		}

		foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
		{
			if (
				property.DeclaredAccessibility != Accessibility.Public
				|| property.IsStatic
				|| property.IsIndexer
				|| !TypeHelpers.HasDataAnnotationAttribute(property)
			)
			{
				continue;
			}

			var propertyResult = SourceGenLibrary.GetValidatablePropertyDescriptor(property, externalSchemas);
			foreach (var diagnosticInfo in propertyResult.Diagnostics)
			{
				var diagnostic = diagnosticInfo.ToDiagnostic();
				if (diagnostic.Location == Location.None)
				{
					var propertyLocation = GetMemberLocation(property);
					diagnostic = Diagnostic.Create(
						diagnostic.Descriptor,
						propertyLocation,
						diagnosticInfo.MessageArgs.ToArray()
					);
				}

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	static Location GetTypeLocation(INamedTypeSymbol type)
	{
		foreach (var location in type.Locations)
		{
			if (location.IsInSource)
				return location;
		}

		return Location.None;
	}

	static Location GetMemberLocation(ISymbol member)
	{
		foreach (var location in member.Locations)
		{
			if (location.IsInSource)
				return location;
		}

		return Location.None;
	}

	/// <summary>
	/// Collects the types that will receive a generated schema: every <c>[ZodSchema]</c> type in this
	/// assembly plus, transitively, the complex property types the generator discovers and emits secondary
	/// schemas for.
	/// </summary>
	/// <param name="compilation">The compilation being analyzed.</param>
	/// <param name="externalSchemas">The resolver that decides schema ownership.</param>
	/// <returns>The set of schema-reachable target types.</returns>
	static ImmutableHashSet<TypeIdentity> BuildSchemaReachableTypes(
		Compilation compilation,
		ExternalSchemaResolver externalSchemas
	)
	{
		HashSet<TypeIdentity> reachable = [];
		Queue<INamedTypeSymbol> queue = new();

		foreach (var type in EnumerateNamedTypes(compilation.Assembly.GlobalNamespace))
		{
			if (ZodSchemaAttributeData.FromAttributeData(type, out _).Exists)
				queue.Enqueue(type);
		}

		while (queue.Count > 0)
		{
			var symbol = queue.Dequeue();
			if (!reachable.Add(new TypeIdentity(symbol)))
				continue;

			foreach (var property in symbol.GetMembers().OfType<IPropertySymbol>())
			{
				if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic || property.IsIndexer)
					continue;

				if (SourceGenLibrary.TryGetNestedSchemaType(property, externalSchemas, out var nested))
					queue.Enqueue(nested);
			}
		}

		return reachable.ToImmutableHashSet();
	}

	static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol root)
	{
		foreach (var member in root.GetMembers())
		{
			switch (member)
			{
				case INamespaceSymbol nestedNamespace:
					foreach (var nested in EnumerateNamedTypes(nestedNamespace))
						yield return nested;
					break;
				case INamedTypeSymbol namedType:
					yield return namedType;
					break;
				default:
					break;
			}
		}
	}

	/// <summary>
	/// Reports <c>ZODSGEN033</c> when a rule-mapped attribute is applied to a type (or one of its
	/// properties) that never gets a generated schema, because the rule can then never run.
	/// </summary>
	static void ReportRuleAttributesWithoutSchema(
		SymbolAnalysisContext context,
		INamedTypeSymbol type,
		ImmutableHashSet<TypeIdentity> schemaReachableTypes
	)
	{
		if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
			return;

		if (schemaReachableTypes.Contains(new TypeIdentity(type)))
			return;

		ReportRuleAttributes(context, type.GetAttributes(), type.Name);

		foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
		{
			if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic || property.IsIndexer)
				continue;

			ReportRuleAttributes(context, property.GetAttributes(), type.Name);
		}
	}

	static void ReportRuleAttributes(
		SymbolAnalysisContext context,
		ImmutableArray<AttributeData> attributes,
		string typeName
	)
	{
		foreach (var attribute in attributes)
		{
			if (attribute.AttributeClass is not INamedTypeSymbol attributeClass)
				continue;

			// Only attributes explicitly mapped to a ZodSharp rule are ZodSharp-specific; plain
			// DataAnnotations attributes are also used by other validators and must not be flagged.
			if (!CustomRuleResolver.IsRuleMapped(attributeClass))
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.RuleAttributeWithoutSchema,
					attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None,
					attributeClass.Name,
					typeName
				)
			);
		}
	}
}

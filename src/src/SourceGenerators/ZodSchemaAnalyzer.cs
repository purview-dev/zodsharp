using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZodSchemaAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsList =
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
		DiagnosticLibrary.ZodRefinementHookTypeNotPartial,
		DiagnosticLibrary.ZodRefinementHookInvalidSignature,
		DiagnosticLibrary.SyncRefinementMethodRetired,
		DiagnosticLibrary.IValidateOptionsReferenceNotFound,
		DiagnosticLibrary.IValidateOptionsValueTypeTarget,
		DiagnosticLibrary.AmbiguousValidationMethods,
		DiagnosticLibrary.UnsupportedCustomRuleTarget,
		DiagnosticLibrary.UnmappableCustomRuleArgument,
		DiagnosticLibrary.UnusedRuleAttributeArgument,
		DiagnosticLibrary.RuleAttributeWithoutSchema,
		DiagnosticLibrary.NativeUnionRecommended,
		DiagnosticLibrary.AutomaticScalarSchemaUnavailable,
		DiagnosticLibrary.NestedSchemaExportDisabled,
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsList;

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

			// Native C# 15 unions are only meaningful when the referenced ZodSharp build exposes the
			// native-union API, which only the net11.0+ assembly does.
			var nativeUnionApi = compilationContext.Compilation.GetTypeByMetadataName("ZodSharp.Unions.NativeUnion`2");
			var zodTypedUnion = compilationContext.Compilation.GetTypeByMetadataName(
				"ZodSharp.Schemas.ZodTypedUnion`2"
			);
			if (nativeUnionApi is not null && zodTypedUnion is not null)
			{
				compilationContext.RegisterOperationAction(
					operationContext => AnalyzeTypedUnionConstruction(operationContext, zodTypedUnion),
					OperationKind.Invocation,
					OperationKind.ObjectCreation
				);
			}

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

		ReportAutomaticScalarDiagnostics(context, type, zodSchemaData, typeLocation);

		// Type-level rules validate the whole value. The generator resolves the same attributes to emit those
		// validations, so a rule that resolves to nothing is reported here rather than dropped silently.
		ReportTypeRuleDiagnostics(context, type, typeLocation);

		if (zodSchemaData.GenerateJsonSchema && ReadGenerateJsonSchemas(context.Options))
			ReportNestedSchemaExportDisabled(context, type, externalSchemas, typeLocation);

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

		var hookResult = ZodRefinementHookResolver.Resolve(type);
		foreach (var diagnosticInfo in hookResult.Diagnostics)
			context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());

		// A model may declare either the Zod refinement hook or an async custom validation method,
		// but not both.
		if (customValidationResult.Value.HasCustomValidation && hookResult.Value.IsImplemented)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.AmbiguousValidationMethods,
					typeLocation,
					type.Name,
					$"{TypeLibraryGenerator.ZodRefinementHookName}({TypeLibraryGenerator.ZodRefineContextName}<T>)",
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
			// A positional record property inherits the attributes of the primary-constructor parameter it is
			// synthesized from, so the analyzer resolves the same effective set the generator validates with.
			var validationAttributes = SourceGenLibrary.GetValidationAttributes(property, context.Compilation);

			if (
				property.DeclaredAccessibility != Accessibility.Public
				|| property.IsStatic
				|| property.IsIndexer
				|| !TypeHelpers.HasDataAnnotationAttribute(validationAttributes)
			)
			{
				continue;
			}

			var propertyResult = SourceGenLibrary.GetValidatablePropertyDescriptor(
				property,
				externalSchemas,
				context.Compilation
			);
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

	/// <summary>
	/// Reports the diagnostics produced while resolving the type-level rule attributes on
	/// <paramref name="type"/>. The generator resolves the same attributes to emit the whole-value
	/// validations, so reporting them here keeps a rule that never runs visible in the build.
	/// </summary>
	/// <param name="context">The analysis context the diagnostics are reported to.</param>
	/// <param name="type">The schema type whose type-level rules are resolved.</param>
	/// <param name="typeLocation">The location to fall back to when a diagnostic carries none.</param>
	static void ReportTypeRuleDiagnostics(SymbolAnalysisContext context, INamedTypeSymbol type, Location typeLocation)
	{
		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		_ = CustomRuleResolver.Resolve(type, type, diagnostics, isTypeLevel: true);

		foreach (var diagnosticInfo in diagnostics)
		{
			var diagnostic = diagnosticInfo.ToDiagnostic();
			if (diagnostic.Location == Location.None)
			{
				diagnostic = Diagnostic.Create(
					diagnostic.Descriptor,
					typeLocation,
					diagnosticInfo.MessageArgs.ToArray()
				);
			}

			context.ReportDiagnostic(diagnostic);
		}
	}

	/// <summary>
	/// Reports <c>ZODSGEN044</c> when a <c>[ZodSchema]</c> type uses an automatic scalar form
	/// (<c>[Scalar&lt;TValue&gt;]</c> or <c>[Scalar(typeof(TValue))]</c>) that cannot produce a usable schema.
	/// The value-object generator always emits a <c>Create</c> that calls
	/// <c>{Type}Schema.Validate(instance)</c>, so both a suppressed <c>Validate</c> method and an underlying
	/// type that cannot be represented leave a dangling reference.
	/// </summary>
	/// <param name="context">The analysis context the diagnostic is reported to.</param>
	/// <param name="type">The schema target type.</param>
	/// <param name="zodSchemaData">The resolved <c>[ZodSchema]</c> attribute data.</param>
	/// <param name="typeLocation">The location to report against.</param>
	static void ReportAutomaticScalarDiagnostics(
		SymbolAnalysisContext context,
		INamedTypeSymbol type,
		ZodSchemaAttributeData zodSchemaData,
		Location typeLocation
	)
	{
		if (
			!CustomRuleResolver.TryGetScalarAttribute(type, out var scalar)
			|| !scalar.IsAutomatic
			|| scalar.ValueType is null
		)
		{
			return;
		}

		if (!zodSchemaData.GenerateValidateMethod)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.AutomaticScalarSchemaUnavailable,
					typeLocation,
					type.Name,
					"[ZodSchema(GenerateValidateMethod = false)] suppresses the Validate method the value-object generator calls"
				)
			);
			return;
		}

		if (!SourceGenLibrary.CanRepresentScalarValueType(scalar.ValueType))
		{
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.AutomaticScalarSchemaUnavailable,
					typeLocation,
					type.Name,
					$"its underlying type '{scalar.ValueType.ToDisplayString()}' cannot be represented"
				)
			);
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
	/// Reads the assembly-level JSON Schema switch (<c>ZodSharpGenerateJsonSchemas</c>). On by default; only
	/// an explicit <c>false</c> disables it.
	/// </summary>
	/// <param name="options">The analyzer options.</param>
	/// <returns><see langword="true"/> when JSON Schema export is enabled for the compilation.</returns>
	static bool ReadGenerateJsonSchemas(AnalyzerOptions options) =>
		!options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
			$"build_property:{PropertyLibrary.GenerateJsonSchemasProperty}",
			out var value
		)
		|| !bool.TryParse(value, out var enabled)
		|| enabled;

	/// <summary>
	/// Reports <c>ZODSGEN045</c> for each property whose nested <c>[ZodSchema]</c> type disables JSON Schema
	/// export, so the parent's exported schema cannot describe it (it falls back to an empty schema).
	/// </summary>
	/// <param name="context">The analysis context the diagnostics are reported to.</param>
	/// <param name="type">The exporting schema type.</param>
	/// <param name="externalSchemas">The resolver that decides schema ownership.</param>
	/// <param name="typeLocation">The location to fall back to when a property carries none.</param>
	static void ReportNestedSchemaExportDisabled(
		SymbolAnalysisContext context,
		INamedTypeSymbol type,
		ExternalSchemaResolver externalSchemas,
		Location typeLocation
	)
	{
		foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
		{
			if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic || property.IsIndexer)
				continue;

			if (!SourceGenLibrary.TryGetNestedSchemaType(property, externalSchemas, out var nested))
				continue;

			var nestedData = ZodSchemaAttributeData.FromAttributeData(nested, out _);
			if (!nestedData.Exists || nestedData.GenerateJsonSchema)
				continue;

			var location = GetMemberLocation(property);
			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.NestedSchemaExportDisabled,
					location == Location.None ? typeLocation : location,
					property.Name,
					nested.Name
				)
			);
		}
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

		return [.. reachable];
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

			// A positional record property inherits the attributes of the primary-constructor parameter it is
			// synthesized from, so the same effective set the generator validates with is reported here.
			ReportRuleAttributes(
				context,
				SourceGenLibrary.GetValidationAttributes(property, context.Compilation),
				type.Name
			);
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

	/// <summary>
	/// Reports <c>ZODSGEN041</c> when a typed union is constructed whose option types are all
	/// reference types, so the allocation-free native union is a viable alternative on net11+.
	/// </summary>
	static void AnalyzeTypedUnionConstruction(OperationAnalysisContext context, INamedTypeSymbol zodTypedUnion)
	{
		var resultType = context.Operation switch
		{
			IInvocationOperation invocation => invocation.Type,
			IObjectCreationOperation creation => creation.Type,
			_ => null,
		};

		if (
			resultType is not INamedTypeSymbol named
			|| named.TypeArguments.Length != 2
			|| !SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, zodTypedUnion)
			|| !named.TypeArguments.All(static argument => argument.IsReferenceType)
		)
		{
			return;
		}

		context.ReportDiagnostic(
			Diagnostic.Create(
				DiagnosticLibrary.NativeUnionRecommended,
				context.Operation.Syntax.GetLocation(),
				named.Name,
				string.Join(", ", named.TypeArguments.Select(static argument => argument.Name))
			)
		);
	}
}

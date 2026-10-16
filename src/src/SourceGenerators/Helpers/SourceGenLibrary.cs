using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Helpers;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cohesive helper library for the ZodSchema generator: the schema model, the DataAnnotations descriptors and the rule resolver are all in scope by design. The incremental stages that only configure generation live in GenerationSettingsResolver, and the enum/JSON wire metadata in EnumJsonResolver."
)]
static partial class SourceGenLibrary
{
	const string IValidateOptionsMetadataName = "Microsoft.Extensions.Options.IValidateOptions`1";

	static bool HasIValidateOptionsType(Compilation compilation) =>
		compilation.GetTypeByMetadataName(IValidateOptionsMetadataName) is not null;

	public static IncrementalValueProvider<SchemaGenerationModel> GetGeneratorValueProviders(
		IncrementalGeneratorInitializationContext context
	)
	{
		var generationContext = IncrementalPipeline.GenerationContextValueProvider<
			SchemaGenerationCapabilities,
			ZodSchemaGenerator
		>(
			context,
			static (compilation, _, _, _) =>
				new()
				{
					HasRequiredAttribute = TypeHelpers.HasType(
						compilation,
						TypeLibrary.System.ComponentModel.DataAnnotations.RequiredAttribute
					),
					HasIValidateOptions = HasIValidateOptionsType(compilation),
				},
			PropertyLibrary.DisableZodSharpSourceGeneratorProperty
		);
		var schemaSets = IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			TypeLibrary.ZodSharp.ZodSchemaAttribute,
			predicate: static (node, _) => node is TypeDeclarationSyntax,
			transform: static (attributeContext, cancellationToken) =>
				GetSchemasForGeneration(attributeContext, cancellationToken)
		);
		var autoDetectSettings = context.AnalyzerConfigOptionsProvider.Select(
			static (provider, _) => GenerationSettingsResolver.Read(provider)
		);
		var assemblyDefaults = context.CompilationProvider.Select(
			static (compilation, _) => GenerationSettingsResolver.ReadAssemblyDefaults(compilation)
		);

		return generationContext
			.CollectWith(
				schemaSets,
				static (outputContext, sets, _) =>
					new SchemaGenerationModel(outputContext) { ZodSchemas = Deduplicate(sets) },
				"CollectZodSchemas"
			)
			.Combine(autoDetectSettings)
			.Combine(assemblyDefaults)
			.Select(
				static (input, _) => GenerationSettingsResolver.Apply(input.Left.Left, input.Left.Right, input.Right)
			);
	}

	static GeneratorResult<SchemaSet> GetSchemasForGeneration(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode, cancellationToken) is not INamedTypeSymbol root)
			return default;

		var compilation = context.SemanticModel.Compilation;
		ExternalSchemaResolver externalSchemas = new(compilation);
		var schemas = ImmutableArray.CreateBuilder<ZodSchemaDescriptor>();
		HashSet<TypeIdentity> seen = [];
		Queue<(INamedTypeSymbol Symbol, bool IsPrimary)> queue = new();
		queue.Enqueue((root, true));

		while (queue.Count > 0)
		{
			var (symbol, isPrimary) = queue.Dequeue();

			// Never emit a schema for a type this compilation does not own: the declaring assembly has
			// already generated one, and a second copy surfaces as CS0436 in this compilation.
			if (!isPrimary && !externalSchemas.IsSchemaOwnedByThisCompilation(symbol))
				continue;

			TypeIdentity target = new(symbol);
			if (!seen.Add(target))
				continue;

			var zodSchemaAttribute = ZodSchemaAttributeData.FromAttributeData(symbol, out var attribute);
			var schemaName = zodSchemaAttribute.SchemaName is { Length: > 0 } customSchemaName
				? customSchemaName
				: $"{target.Name}Schema";
			var schema = target with { Name = schemaName };
			var targetCanBeNull = TypeHelpers.CanBeNull(symbol);
			var typeAllowNulls = ReadAllowNulls(attribute);
			var properties = GetZodProperties(symbol, externalSchemas, compilation, typeAllowNulls);
			properties = AddAutomaticScalarProperty(symbol, properties, externalSchemas);

			// Type-level rules ([ZodRule]-mapped attributes on the target itself) validate the whole value
			// rather than a property, which is what makes a scalar value object validatable as a unit. The
			// generator only needs the descriptors: ZodSchemaAnalyzer resolves the same attributes and
			// reports the diagnostics, so a dropped rule is visible in the build instead of failing silently.
			var typeRules = CustomRuleResolver.Resolve(symbol, symbol, isTypeLevel: true);
			var accessibility = symbol.ContainingType is null
				? symbol.DeclaredAccessibility == Accessibility.Public
					? TypeDeclarationAccessibility.Public
					: TypeDeclarationAccessibility.Internal
				: symbol.DeclaredAccessibility.ToTypeDeclarationAccessibility();
			var customValidation = ResolveCustomValidationMethod(symbol, zodSchemaAttribute, attribute!);
			var refinementHook = ZodRefinementHookResolver.Resolve(symbol);

			// A model may declare either the Zod refinement hook or an async custom validation method,
			// but not both (reported as ZODSGEN029 by the analyzer). When both are present, the hook
			// wins so the emitted validator does not reference two competing validation methods.
			if (customValidation.Value.HasCustomValidation && refinementHook.Value.IsImplemented)
				customValidation = CustomValidationMethodData.None;
			var isValueType = symbol.TypeKind == TypeKind.Struct;

			// A Purview.ValueObjects scalar serialises as its underlying value, so its JSON Schema is the
			// underlying type's schema (for example a Guid scalar is a canonical Guid string, not Zod's
			// RFC UUID format), not an object wrapping a Value property.
			TypeIdentity? scalarUnderlyingType = null;
			var scalarUnderlyingCanBeNull = false;
			if (CustomRuleResolver.TryGetScalarUnderlyingValue(symbol, out var scalarValueType))
			{
				scalarUnderlyingType = CreateTypeIdentity(TypeHelpers.UnwrapNullableType(scalarValueType));
				scalarUnderlyingCanBeNull = IsNullableProperty(scalarValueType);
			}
			bool? generateIValidateOptions =
				zodSchemaAttribute.GenerateIValidateOptions ? true
				: zodSchemaAttribute.SuppressIValidateOptions ? false
				: null;

			// Nested complex types discovered without a [ZodSchema] attribute default to emitting both
			// methods (the attribute's [Property(DefaultValue = true)] defaults only apply when the
			// attribute is actually present; ZodSchemaAttributeData.Empty carries default(bool)).
			var generateValidateMethod = !zodSchemaAttribute.Exists || zodSchemaAttribute.GenerateValidateMethod;
			var generateParseMethod = !zodSchemaAttribute.Exists || zodSchemaAttribute.GenerateParseMethod;
			var validateEnumValues = !zodSchemaAttribute.Exists || zodSchemaAttribute.ValidateEnumValues;
			var generateJsonSchema = !zodSchemaAttribute.Exists || zodSchemaAttribute.GenerateJsonSchema;

			schemas.Add(
				new(
					target,
					schema,
					targetCanBeNull,
					GetContainingTypes(symbol),
					accessibility,
					isValueType,
					properties,
					customValidation,
					refinementHook,
					generateIValidateOptions,
					zodSchemaAttribute.EnableComposition,
					generateValidateMethod,
					generateParseMethod,
					typeRules,
					validateEnumValues,
					isPrimary,
					scalarUnderlyingType,
					scalarUnderlyingCanBeNull,
					generateJsonSchema,
					false,
					default
				)
			);

			foreach (
				var property in symbol
					.GetMembers()
					.OfType<IPropertySymbol>()
					.Where(m =>
						m.DeclaredAccessibility == Accessibility.Public
						&& !m.IsStatic
						&& !m.IsIndexer
						&& (
							TypeHelpers.HasDataAnnotationAttribute(GetValidationAttributes(m, compilation))
							|| IsPropertyWithNestedSchema(m, externalSchemas)
						)
					)
			)
			{
				if (TryGetNestedSchemaType(property, externalSchemas, out var nested))
					queue.Enqueue((nested, false));
			}
		}

		return GeneratorResult<SchemaSet>.Create(new SchemaSet(schemas.ToImmutable()));
	}

	static bool IsPropertyWithNestedSchema(IPropertySymbol property, ExternalSchemaResolver externalSchemas)
	{
		var propertyType = TypeHelpers.UnwrapNullableType(property.Type);
		if (propertyType is IArrayTypeSymbol arrayType)
			propertyType = arrayType.ElementType;
		else if (propertyType is INamedTypeSymbol namedType)
		{
			var enumerable = namedType.AllInterfaces.FirstOrDefault(
				TypeLibrary.System.Collections.Generic.IEnumerable.Equals
			);
			if (enumerable is not null)
				propertyType = enumerable.TypeArguments[0];
		}

		propertyType = TypeHelpers.UnwrapNullableType(propertyType);
		return propertyType is INamedTypeSymbol nested && externalSchemas.IsSchemaOwnedByThisCompilation(nested);
	}

	static EquatableArray<TypeDeclarationOptions> GetContainingTypes(INamedTypeSymbol typeSymbol)
	{
		var chain = ImmutableArray.CreateBuilder<TypeDeclarationOptions>();
		var current = typeSymbol.ContainingType;
		while (current is not null)
		{
			// We don't own the generated container class, so don't include the
			// generated attributes on it, otherwise if that itself is also generated,
			// it will have duplicate attributes.
			chain.Add(
				TypeHelpers.CreatePartialTypeDeclarationOptions(current) with
				{
					IncludeGeneratedAttributes = false,
				}
			);
			current = current.ContainingType;
		}

		chain.Reverse();
		return chain.ToImmutable();
	}

	static EquatableArray<GeneratorResult<ZodSchemaDescriptor>> Deduplicate(
		ImmutableArray<GeneratorResult<SchemaSet>> sets
	)
	{
		var results = ImmutableArray.CreateBuilder<GeneratorResult<ZodSchemaDescriptor>>();
		HashSet<TypeIdentity> seen = [];

		// A type can be reached twice: once from its own [ZodSchema] attribute (primary) and once as a
		// nested property of another schema (secondary). The attribute contexts are processed in an
		// unspecified order, so keeping whichever arrives first lets a secondary copy displace the primary.
		// That drops the primary-only members — the DI adapter ({Type}SchemaValidator) and its assembly
		// registration — for any type that happens to be referenced by a type processed earlier. Collect
		// the primaries first, then fill in the secondaries, so the primary always wins.
		foreach (var set in sets)
		{
			if (!set.ShouldProcess)
				continue;

			foreach (var schema in set.Value.Schemas)
			{
				if (schema.IsPrimary && seen.Add(schema.TargetType))
					results.Add(schema);
			}
		}

		foreach (var set in sets)
		{
			if (!set.ShouldProcess)
				continue;

			foreach (var schema in set.Value.Schemas)
			{
				if (seen.Add(schema.TargetType))
					results.Add(schema);
			}
		}

		return new(results.ToImmutable());
	}

	static EquatableArray<GeneratorResult<ZodPropertyDescriptor>> GetZodProperties(
		INamedTypeSymbol symbol,
		ExternalSchemaResolver externalSchemas,
		Compilation compilation,
		bool? typeAllowNulls
	)
	{
		var properties = symbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(property =>
				property.DeclaredAccessibility == Accessibility.Public && !property.IsStatic && !property.IsIndexer
			)
			.Select(property =>
				GetValidatablePropertyDescriptor(property, externalSchemas, compilation, typeAllowNulls)
			)
			.ToImmutableArray();

		return new(properties);
	}

	/// <summary>
	/// Gets the validation attributes that apply to <paramref name="property"/>, merging in the attributes of
	/// the primary-constructor parameter a positional record property is synthesized from. See
	/// <see cref="PropertyAttributeResolver"/> for why the parameter's attributes are not on the property.
	/// </summary>
	internal static ImmutableArray<AttributeData> GetValidationAttributes(
		IPropertySymbol property,
		Compilation compilation
	) => PropertyAttributeResolver.Resolve(property, compilation);

	/// <summary>
	/// Adds the schema property for an automatic scalar value object
	/// (<c>[Scalar&lt;TValue&gt;]</c> / <c>[Scalar(typeof(TValue))]</c>), whose underlying member is
	/// emitted by the value-object generator and is therefore invisible here. The property is synthesized
	/// from the attribute so the schema reads <c>value.{PropertyName}</c>, which resolves once the
	/// value-object generator's partial is merged.
	/// </summary>
	/// <param name="symbol">The schema target type.</param>
	/// <param name="properties">The properties discovered from source.</param>
	/// <param name="externalSchemas">The resolver that decides schema ownership.</param>
	/// <returns>The properties with the synthesized scalar property prepended, when applicable.</returns>
	static EquatableArray<GeneratorResult<ZodPropertyDescriptor>> AddAutomaticScalarProperty(
		INamedTypeSymbol symbol,
		EquatableArray<GeneratorResult<ZodPropertyDescriptor>> properties,
		ExternalSchemaResolver externalSchemas
	)
	{
		if (!CustomRuleResolver.TryGetAutomaticScalar(symbol, out var propertyName, out var valueType))
			return properties;

		// A declared member means the value-object generator does not own the property (the manual form, or
		// a consumer that declared it by mistake); the discovered descriptor already covers it.
		foreach (var member in symbol.GetMembers(propertyName))
		{
			if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public })
				return properties;
		}

		var declaredType = CreateTypeIdentity(valueType);
		if (declaredType is null)
			return properties;

		var propertyType = declaredType.Value;
		var originalPropertyType = valueType;
		var isNullableValueType = false;
		if (
			originalPropertyType is INamedTypeSymbol
			{
				OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
			} nullableType
		)
		{
			var unwrappedType = CreateTypeIdentity(nullableType.TypeArguments[0]);
			if (unwrappedType is null)
				return properties;

			propertyType = unwrappedType.Value;
			originalPropertyType = nullableType.TypeArguments[0];
			isNullableValueType = true;
		}

		var validationKind = GetPropertyValidationKind(propertyType, originalPropertyType, externalSchemas);
		var enumType = TypeHelpers.UnwrapNullableType(originalPropertyType) as INamedTypeSymbol;
		var isEnum = enumType is { TypeKind: TypeKind.Enum };
		var isFlagsEnum = isEnum && HasFlagsAttribute(enumType!);

		ZodPropertyDescriptor descriptor = new(
			propertyType,
			propertyName,
			propertyName,
			TypeHelpers.CanBeNull(valueType),
			IsNullableProperty(valueType),
			isNullableValueType,
			isEnum,
			isFlagsEnum,
			isEnum && !isFlagsEnum ? GetIgnoredEnumMembers(enumType!) : default,
			validationKind,
			null,
			false,
			null,
			null,
			new(string.Empty, string.Empty, false),
			false,
			EmptyValidationAttributes,
			new([]),
			null,
			true,
			false,
			new([])
		);

		return new([GeneratorResult<ZodPropertyDescriptor>.Create(descriptor), .. properties]);
	}

	/// <summary>
	/// The empty <see cref="ValidationAttributes"/> for a synthesized property: the automatic scalar form
	/// has no source member, so it carries no DataAnnotations.
	/// </summary>
	static readonly ValidationAttributes EmptyValidationAttributes = new(
		GeneratorResult<RequiredAttributeData>.Empty,
		GeneratorResult<CompareAttributeData>.Empty,
		GeneratorResult<DisplayAttributeData>.Empty,
		GeneratorResult<EmailAddressAttributeData>.Empty,
		GeneratorResult<CreditCardAttributeData>.Empty,
		GeneratorResult<PhoneAttribute>.Empty,
		GeneratorResult<UrlAttribute>.Empty,
		GeneratorResult<StringLengthAttribute>.Empty,
		GeneratorResult<MinLengthAttributeData>.Empty,
		GeneratorResult<MaxLengthAttributeData>.Empty,
		GeneratorResult<RegularExpressionAttributeData>.Empty,
		GeneratorResult<Base64StringAttributeData>.Empty,
		GeneratorResult<DeniedValuesAttributeData>.Empty,
		GeneratorResult<AllowedValuesAttributeData>.Empty,
		GeneratorResult<LengthAttributeData>.Empty,
		GeneratorResult<RangeAttributeData>.Empty
	);

	internal static GeneratorResult<ZodPropertyDescriptor> GetValidatablePropertyDescriptor(
		IPropertySymbol property,
		ExternalSchemaResolver? externalSchemas,
		Compilation compilation,
		bool? typeAllowNulls
	)
	{
		var declaredType = CreateTypeIdentity(property.Type);

		// The property type cannot be represented (an unresolved/error type, a type parameter, dynamic, a
		// pointer, ...). The compiler already reports the underlying problem, so skip the property rather than
		// fail the whole generation pass and add a second, noisier error.
		if (declaredType is null)
			return GeneratorResult<ZodPropertyDescriptor>.Empty;

		var propertyType = declaredType.Value;
		var originalPropertyType = property.Type;

		// Two nullabilities are tracked. `CanBeNull` is whether the type can hold null at runtime (a
		// reference type or Nullable<T>), which governs whether [Required] is meaningful. `IsDeclaredNullable`
		// follows the nullable reference-type annotation (a non-nullable `string` is required and its rules
		// run unconditionally and reject null, while a `string?` stays nullable). A compilation with the
		// nullable context disabled reports NullableAnnotation.None, where a reference type is treated as
		// nullable.
		var propertyCanBeNull = TypeHelpers.CanBeNull(originalPropertyType);
		var propertyDeclaredNullable = IsNullableProperty(originalPropertyType);
		var isNullableValueType = false;
		if (
			originalPropertyType is INamedTypeSymbol
			{
				OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
			} nullableType
		)
		{
			var unwrappedType = CreateTypeIdentity(nullableType.TypeArguments[0]);
			if (unwrappedType is null)
				return GeneratorResult<ZodPropertyDescriptor>.Empty;

			propertyType = unwrappedType.Value;
			originalPropertyType = nullableType.TypeArguments[0];
			isNullableValueType = true;
		}

		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		var validationKind = GetPropertyValidationKind(propertyType, originalPropertyType, externalSchemas);
		var elementType =
			validationKind == PropertyValidationKind.Collection
				? GetCollectionElementTypeIdentity(originalPropertyType)
				: null;
		var elementTypeCanBeNull =
			validationKind == PropertyValidationKind.Collection
			&& elementType is not null
			&& TypeHelpers.CanBeNull(GetCollectionElementTypeSymbol(originalPropertyType) ?? originalPropertyType);
		var nestedSchemaType = GetNestedSchemaTypeIdentity(property, validationKind, externalSchemas);
		var lengthAccessor = ClassifyLengthAccessor(originalPropertyType);
		var validationAttributes = GetValidationAttributes(property, compilation);
		var allowNulls =
			validationKind == PropertyValidationKind.Collection
				? ResolveAllowNulls(originalPropertyType, validationAttributes, typeAllowNulls)
				: null;
		var displayName = GetDisplayName(validationAttributes, property.Name);

		var displayAttribute = DisplayAttributeData.FromAttributeData(validationAttributes);
		var requiredAttribute = RequiredAttributeData.FromAttributeData(validationAttributes);
		var compareAttribute = CompareAttributeData.FromAttributeData(validationAttributes);
		var emailAddressAttribute = EmailAddressAttributeData.FromAttributeData(validationAttributes);
		var creditCardAttribute = CreditCardAttributeData.FromAttributeData(validationAttributes);
		var phoneAttribute = PhoneAttribute.FromAttributeData(validationAttributes);
		var urlAttribute = UrlAttribute.FromAttributeData(validationAttributes);
		var stringLengthAttribute = StringLengthAttribute.FromAttributeData(validationAttributes);
		var minLengthAttribute = MinLengthAttributeData.FromAttributeData(validationAttributes);
		var maxLengthAttribute = MaxLengthAttributeData.FromAttributeData(validationAttributes);
		var regularExpressionAttribute = GeneratorResult<RegularExpressionAttributeData>.Empty;
		if (
			RegularExpressionAttributeData.TryFromAttributeData(
				validationAttributes,
				out var regexData,
				out var attribute
			)
		)
		{
			regularExpressionAttribute =
				attribute is not null && propertyType.SpecialType != SpecialType.System_String
					? GeneratorResult<RegularExpressionAttributeData>.Create(
						regexData,
						ReportableDiagnostic.Create(
							DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
							true,
							GetAttributeLocation(attribute),
							property.Name
						)
					)
					: GeneratorResult<RegularExpressionAttributeData>.Create(regexData);
		}

		var base64StringAttribute = Base64StringAttributeData.FromAttributeData(validationAttributes);
		var deniedValuesAttribute = DeniedValuesAttributeData.FromAttributeData(validationAttributes);
		var allowedValuesAttribute = AllowedValuesAttributeData.FromAttributeData(validationAttributes);

		AddUnsupportedDataAnnotationsDiagnostics(
			validationAttributes,
			property.Name,
			propertyType,
			originalPropertyType,
			urlAttribute,
			phoneAttribute,
			creditCardAttribute,
			base64StringAttribute,
			emailAddressAttribute,
			allowedValuesAttribute,
			deniedValuesAttribute,
			diagnostics
		);

		var lengthAttribute = GeneratorResult<LengthAttributeData>.Empty;
		if (LengthAttributeData.TryFromAttributeData(validationAttributes, out var lengthData, out attribute))
		{
			lengthAttribute = BuildLengthAttributeResult(
				property,
				lengthData,
				attribute,
				propertyType,
				originalPropertyType
			);
		}

		var rangeAttribute = RangeAttributeData.FromAttributeData(validationAttributes, out attribute);
		var rangeAttributeResult = TryBuildRangeBoundaryExpressions(
			originalPropertyType,
			rangeAttribute,
			out var minimumExpression,
			out var maximumExpression
		)
			? GeneratorResult<RangeAttributeData>.Create(
				rangeAttribute with
				{
					MinimumExpression = minimumExpression,
					MaximumExpression = maximumExpression,
				}
			)
			: GeneratorResult<RangeAttributeData>.Create(
				rangeAttribute,
				ReportableDiagnostic.Create(
					DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
					true,
					GetAttributeLocation(attribute),
					property.Name
				)
			);

		ValidateCompareProperty(property, validationAttributes, compareAttribute, diagnostics);

		ValidateErrorMessageResourceConfiguration(validationAttributes, property.Name, diagnostics);

		diagnostics.AddRange(regularExpressionAttribute.Diagnostics);
		diagnostics.AddRange(lengthAttribute.Diagnostics);
		if (rangeAttribute.Exists)
			diagnostics.AddRange(rangeAttributeResult.Diagnostics);

		var enumType = TypeHelpers.UnwrapNullableType(originalPropertyType) as INamedTypeSymbol;
		var isEnum = enumType is { TypeKind: TypeKind.Enum };
		var isFlagsEnum = isEnum && HasFlagsAttribute(enumType!);
		var ignoredEnumMembers = isEnum && !isFlagsEnum ? GetIgnoredEnumMembers(enumType!) : default;
		var enumSerializedAsString = isEnum && EnumJsonResolver.IsSerializedAsString(enumType!, property);
		var enumJsonNames =
			isEnum && !isFlagsEnum ? EnumJsonResolver.GetJsonNames(enumType!, validationAttributes) : new([]);

		var customRules = CustomRuleResolver.Resolve(validationAttributes, originalPropertyType, diagnostics);

		var compareViaCompareTo =
			validationKind == PropertyValidationKind.Comparable
			&& originalPropertyType is INamedTypeSymbol namedPropertyType
			&& IsVersion(namedPropertyType);

		return GeneratorResult<ZodPropertyDescriptor>.Create(
			new(
				propertyType,
				property.Name,
				displayName,
				propertyCanBeNull,
				propertyDeclaredNullable,
				isNullableValueType,
				isEnum,
				isFlagsEnum,
				ignoredEnumMembers,
				validationKind,
				elementType,
				elementTypeCanBeNull,
				allowNulls,
				nestedSchemaType,
				lengthAccessor,
				compareViaCompareTo,
				new(
					requiredAttribute,
					compareAttribute,
					displayAttribute,
					emailAddressAttribute,
					creditCardAttribute,
					phoneAttribute,
					urlAttribute,
					stringLengthAttribute,
					minLengthAttribute,
					maxLengthAttribute,
					regularExpressionAttribute,
					base64StringAttribute,
					deniedValuesAttribute,
					allowedValuesAttribute,
					lengthAttribute,
					rangeAttributeResult
				),
				customRules,
				GetJsonPropertyName(property),
				NestedSchemaExportsJsonSchema(property, validationKind, externalSchemas),
				enumSerializedAsString,
				enumJsonNames
			),
			diagnostics.ToImmutable()
		);
	}

	/// <summary>
	/// Determines whether the nested complex type a property references exports JSON Schema. A nested type
	/// can opt out with <c>[ZodSchema(GenerateJsonSchema = false)]</c>, in which case the parent's schema
	/// cannot reference it and falls back to an empty schema (reported as ZODSGEN045).
	/// </summary>
	/// <param name="property">The property to inspect.</param>
	/// <param name="validationKind">The property's validation kind.</param>
	/// <param name="externalSchemas">The resolver that decides schema ownership.</param>
	/// <returns><see langword="true"/> when the property has no nested schema, or the nested type exports one.</returns>
	static bool NestedSchemaExportsJsonSchema(
		IPropertySymbol property,
		PropertyValidationKind validationKind,
		ExternalSchemaResolver? externalSchemas
	)
	{
		if (validationKind is not (PropertyValidationKind.Complex or PropertyValidationKind.Collection))
			return true;

		var targetType =
			validationKind == PropertyValidationKind.Collection
				? GetCollectionElementTypeSymbol(property.Type)
				: TypeHelpers.UnwrapNullableType(property.Type);

		if (targetType is not INamedTypeSymbol namedType)
			return true;

		// Only a type owned by this compilation can opt out; an external schema's export is that assembly's
		// concern.
		if (externalSchemas is not null && !externalSchemas.IsSchemaOwnedByThisCompilation(namedType))
			return true;

		var attributeData = ZodSchemaAttributeData.FromAttributeData(namedType, out _);
		return !attributeData.Exists || attributeData.GenerateJsonSchema;
	}

	/// <summary>
	/// Reads an explicit <c>[System.Text.Json.Serialization.JsonPropertyName]</c> from a property, so the
	/// exported JSON Schema names the property exactly as System.Text.Json writes it.
	/// </summary>
	/// <param name="property">The property to inspect.</param>
	/// <returns>The declared JSON name, or <see langword="null"/> when none is declared.</returns>
	static string? GetJsonPropertyName(IPropertySymbol property)
	{
		foreach (var attribute in property.GetAttributes())
		{
			if (
				attribute.AttributeClass is not { Name: "JsonPropertyNameAttribute" } attributeClass
				|| attributeClass.ContainingNamespace.ToDisplayString() != "System.Text.Json.Serialization"
				|| attribute.ConstructorArguments.Length != 1
			)
			{
				continue;
			}

			if (attribute.ConstructorArguments[0].Value is string { Length: > 0 } name)
				return name;
		}

		return null;
	}

	/// <summary>
	/// Determines whether a property accepts null, from its nullable reference-type annotation or its
	/// <see cref="Nullable{T}"/> construction. A non-nullable reference type (for example <c>string</c>) is
	/// non-nullable, so its rules run unconditionally and reject null; a <c>string?</c> accepts null.
	/// </summary>
	/// <param name="typeSymbol">The declared property type.</param>
	/// <returns><see langword="true"/> when the property accepts null.</returns>
	/// <remarks>
	/// A compilation with the nullable context disabled reports <see cref="NullableAnnotation.None"/> for
	/// every reference type; there the annotation cannot distinguish nullable from non-nullable, so a
	/// reference type is treated as nullable (the pre-annotation behaviour).
	/// </remarks>
	static bool IsNullableProperty(ITypeSymbol typeSymbol) =>
		typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
		|| typeSymbol.NullableAnnotation == NullableAnnotation.Annotated
		|| (typeSymbol.NullableAnnotation == NullableAnnotation.None && typeSymbol.IsReferenceType);

	/// <summary>
	/// Creates the <c>TypeIdentity</c> for a symbol, returning <see langword="null"/> for symbols that
	/// cannot be represented (unresolved error types, type parameters, <c>dynamic</c>, pointers, ...).
	/// </summary>
	/// <remarks>
	/// Generator pipelines routinely encounter such symbols — an unresolved property type in a compilation that
	/// is already failing to build is the common case — so this must not throw. The caller skips the property
	/// instead, leaving the compiler's own error as the single, clear signal.
	/// </remarks>
	static TypeIdentity? CreateTypeIdentity(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is IArrayTypeSymbol arrayType)
		{
			var elementIdentity = CreateTypeIdentity(arrayType.ElementType);
			return elementIdentity is null
				? null
				: new TypeIdentity($"{elementIdentity.Value.Name}[]", elementIdentity.Value.Namespace);
		}

		return TypeIdentity.TryCreate(typeSymbol, out var identity) ? identity : null;
	}

	/// <summary>
	/// Determines whether an automatic scalar's underlying type can be represented as a schema property.
	/// Mirrors <see cref="CreateTypeIdentity"/> so the analyzer and the generator agree on which automatic
	/// scalar types can produce a schema.
	/// </summary>
	/// <param name="valueType">The scalar's declared underlying type.</param>
	/// <returns><see langword="true"/> when the type can be represented.</returns>
	internal static bool CanRepresentScalarValueType(ITypeSymbol valueType) =>
		CreateTypeIdentity(TypeHelpers.UnwrapNullableType(valueType)) is not null;

	static PropertyValidationKind GetPropertyValidationKind(
		TypeIdentity propertyType,
		ITypeSymbol originalType,
		ExternalSchemaResolver? externalSchemas
	)
	{
		if (propertyType.SpecialType == SpecialType.System_String)
			return PropertyValidationKind.String;

		if (TypeHelpers.IsNumericType(originalType))
			return PropertyValidationKind.Numeric;

		if (
			originalType is IArrayTypeSymbol
			|| TypeHelpers.IsOrImplements(originalType, TypeLibrary.System.Collections.IEnumerable)
			|| TypeHelpers.IsOrImplements(originalType, TypeLibrary.System.Collections.Generic.IEnumerable)
		)
		{
			return PropertyValidationKind.Collection;
		}

		// A type declared here can have a nested schema generated for it; a type declared by another
		// assembly is validated through the schema that assembly already generated for it.
		if (originalType is INamedTypeSymbol namedType && HasNestedSchema(namedType, externalSchemas))
			return PropertyValidationKind.Complex;

		// Comparable structs/classes (TimeSpan, DateTime, DateTimeOffset, DateOnly, TimeOnly, Version, ...)
		// can be range-validated without a nested schema.
		if (originalType is INamedTypeSymbol comparableType && IsComparableRangeType(comparableType))
			return PropertyValidationKind.Comparable;

		if (originalType is INamedTypeSymbol { TypeKind: TypeKind.Enum })
			return PropertyValidationKind.Enum;

		// The property type is not a string, numeric, comparable, collection, complex, or enum type that can be validated by the generator.
		return PropertyValidationKind.Unsupported;
	}

	/// <summary>
	/// Determines whether the enum type is declared with the <c>[Flags]</c> attribute. A flags combination is a
	/// valid value without being a defined member, so the automatic enum validation does not apply.
	/// </summary>
	static bool HasFlagsAttribute(INamedTypeSymbol enumType) =>
		enumType.GetAttributes().Any(static attribute => attribute.AttributeClass?.MetadataName == "FlagsAttribute");

	/// <summary>
	/// Collects the enum members marked with <c>[ZodIgnore]</c>, rendered as member expressions the generated
	/// validation passes to the enum rule as its disallowed set.
	/// </summary>
	/// <param name="enumType">The enum type to inspect.</param>
	/// <returns>The ignored member expressions; empty when the enum declares none.</returns>
	/// <remarks>
	/// Every field of an enum type is a member, so the constant-value check is what selects them: the
	/// compiler-generated backing field has no constant value.
	/// </remarks>
	static EquatableArray<string> GetIgnoredEnumMembers(INamedTypeSymbol enumType)
	{
		ImmutableArray<string>.Builder? ignored = null;
		var enumTypeReference = new TypeIdentity(enumType).RenderFullName;

		foreach (var member in enumType.GetMembers())
		{
			if (member is not IFieldSymbol { HasConstantValue: true } enumMember)
				continue;

			if (
				!enumMember
					.GetAttributes()
					.Any(attribute => TypeLibrary.ZodSharp.ZodIgnoreAttribute.Equals(attribute.AttributeClass))
			)
				continue;

			ignored ??= ImmutableArray.CreateBuilder<string>();
			ignored.Add($"{enumTypeReference}.{enumMember.Name}");
		}

		return ignored is null ? new(ImmutableArray<string>.Empty) : new(ignored.ToImmutable());
	}

	static TypeIdentity? GetCollectionElementTypeIdentity(ITypeSymbol propertyType)
	{
		if (propertyType is IArrayTypeSymbol arrayType)
			return CreateTypeIdentity(arrayType.ElementType);

		if (propertyType is not INamedTypeSymbol namedType)
			return null;

		foreach (var iface in namedType.AllInterfaces)
		{
			if (TypeHelpers.Implements(iface, TypeLibrary.System.Collections.Generic.IEnumerable))
				return CreateTypeIdentity(iface.TypeArguments[0]);
		}

		return
			namedType.IsGenericType
			&& TypeHelpers.Implements(namedType, TypeLibrary.System.Collections.Generic.IEnumerable)
			? CreateTypeIdentity(namedType.TypeArguments[0])
			: null;
	}

	static ITypeSymbol? GetCollectionElementTypeSymbol(ITypeSymbol propertyType)
	{
		if (propertyType is IArrayTypeSymbol arrayType)
			return arrayType.ElementType;

		if (propertyType is not INamedTypeSymbol namedType)
			return null;

		foreach (var iface in namedType.AllInterfaces)
		{
			if (TypeHelpers.Implements(iface, TypeLibrary.System.Collections.Generic.IEnumerable))
				return iface.TypeArguments[0];
		}

		return
			namedType.IsGenericType
			&& TypeHelpers.Implements(namedType, TypeLibrary.System.Collections.Generic.IEnumerable)
			? namedType.TypeArguments[0]
			: null;
	}

	/// <summary>
	/// Reads the optional <c>AllowNulls</c> option from a <c>[ZodSchema]</c> attribute, or
	/// <see langword="null"/> when the attribute is absent or leaves the option unset.
	/// </summary>
	/// <remarks>
	/// The option is read from the raw <see cref="AttributeData"/> rather than
	/// <see cref="ZodSchemaAttributeData"/>: the framework's attribute-data-model generator collapses a
	/// nullable value-type named argument to its underlying type, so an unset <c>bool?</c> would arrive as
	/// <c>false</c> and could not defer to the assembly default.
	/// </remarks>
	/// <param name="attribute">The raw attribute, when the type declares <c>[ZodSchema]</c>.</param>
	/// <returns>The declared value, or <see langword="null"/> when unset.</returns>
	internal static bool? ReadAllowNulls(AttributeData? attribute) =>
		attribute is not null
		&& attribute.TryGetNamedArgument<bool>(AttributeGenHelper.AllowNullsPropertyName, out var allow)
			? allow
			: null;

	/// <summary>
	/// Resolves the property/type-level override for whether a collection may contain null elements.
	/// </summary>
	/// <param name="collectionType">The collection type, with a property-level nullable wrapper removed.</param>
	/// <param name="attributes">The property's effective attributes (property plus positional parameter).</param>
	/// <param name="typeAllowNulls">The containing type's <c>[ZodSchema(AllowNulls = ...)]</c>, when set.</param>
	/// <returns>
	/// The override, or <see langword="null"/> when neither the property nor the type states one, so the
	/// assembly default applies.
	/// </returns>
	/// <remarks>
	/// Precedence, highest first: <c>[CollectionBehavior(AllowNulls = ...)]</c> on the property, an explicitly
	/// nullable element type (<c>T?</c>), then the containing type's <c>[ZodSchema]</c> option.
	/// </remarks>
	static bool? ResolveAllowNulls(
		ITypeSymbol collectionType,
		ImmutableArray<AttributeData> attributes,
		bool? typeAllowNulls
	)
	{
		foreach (var attribute in attributes)
		{
			if (!TypeLibrary.ZodSharp.CollectionBehaviorAttribute.Equals(attribute.AttributeClass))
				continue;

			if (attribute.TryGetNamedArgument<bool>(AttributeGenHelper.AllowNullsPropertyName, out var allow))
				return allow;

			break;
		}

		var elementType = GetCollectionElementTypeSymbol(collectionType);
		if (elementType is not null && IsExplicitlyNullableElement(elementType))
			return true;

		// The containing type's [ZodSchema] attribute is the last override, so if it is set, return it; otherwise
		// the assembly default applies.
		return typeAllowNulls;
	}

	/// <summary>
	/// Determines whether a collection element type is explicitly declared nullable — <c>T?</c> (an
	/// annotated reference type) or <c>Nullable&lt;T&gt;</c> — which opts the element out of the default
	/// "reject null items" behaviour.
	/// </summary>
	/// <param name="elementType">The collection's element type.</param>
	/// <returns><see langword="true"/> when the element type is explicitly nullable.</returns>
	/// <remarks>
	/// Only <see cref="NullableAnnotation.Annotated"/> counts. A reference type with no annotation (or
	/// <see cref="NullableAnnotation.None"/> in a nullable-disabled compilation) is not an explicit opt-in,
	/// so the default still rejects null elements there.
	/// </remarks>
	static bool IsExplicitlyNullableElement(ITypeSymbol elementType) =>
		elementType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
		|| elementType.NullableAnnotation == NullableAnnotation.Annotated;

	/// <summary>
	/// Determines whether a <c>[CollectionBehavior]</c> attribute can take effect on a property: it must be a
	/// collection whose elements can be null.
	/// </summary>
	/// <param name="property">The property to inspect.</param>
	/// <returns><see langword="true"/> when the attribute is meaningful for the property.</returns>
	internal static bool IsCollectionBehaviorApplicable(IPropertySymbol property)
	{
		var type = TypeHelpers.UnwrapNullableType(property.Type);
		if (
			type is not IArrayTypeSymbol
			&& !TypeHelpers.IsOrImplements(type, TypeLibrary.System.Collections.IEnumerable)
			&& !TypeHelpers.IsOrImplements(type, TypeLibrary.System.Collections.Generic.IEnumerable)
		)
		{
			return false;
		}

		var elementType = GetCollectionElementTypeSymbol(type);
		return elementType is not null && TypeHelpers.CanBeNull(elementType);
	}

	static TypeIdentity? GetNestedSchemaTypeIdentity(
		IPropertySymbol property,
		PropertyValidationKind validationKind,
		ExternalSchemaResolver? externalSchemas
	)
	{
		var targetType =
			validationKind == PropertyValidationKind.Collection
				? GetCollectionElementTypeSymbol(property.Type)
				: property.Type;

		if (targetType is not INamedTypeSymbol namedType)
			return null;

		if (externalSchemas is null)
			return IsSchemaDefinedInSource(namedType) ? SchemaIdentityFor(namedType) : null;

		if (externalSchemas.IsSchemaOwnedByThisCompilation(namedType))
			return SchemaIdentityFor(namedType);

		// The assembly declaring the type already generated this schema: reference it rather than
		// emitting a second copy with the same fully-qualified name.
		return externalSchemas.TryGetExistingSchema(namedType, out var existingSchema) ? existingSchema : null;
	}

	static TypeIdentity SchemaIdentityFor(INamedTypeSymbol type)
	{
		TypeIdentity identity = new(type);
		return identity with { Name = $"{identity.Name}Schema" };
	}

	static bool HasNestedSchema(INamedTypeSymbol type, ExternalSchemaResolver? externalSchemas) =>
		externalSchemas is null
			? IsSchemaDefinedInSource(type)
			: externalSchemas.IsSchemaOwnedByThisCompilation(type) || externalSchemas.TryGetExistingSchema(type, out _);

	static LengthAccessor ClassifyLengthAccessor(ITypeSymbol propertyType)
	{
		if (propertyType.SpecialType == SpecialType.System_String)
			return new("propertyValue.Length", "string", true);

		if (propertyType is IArrayTypeSymbol)
			return new("propertyValue.Length", "array", true);

		if (propertyType is INamedTypeSymbol namedType)
		{
			if (TypeHelpers.IsOrImplements(namedType, TypeLibrary.System.Collections.Generic.ICollection))
				return new("propertyValue.Count", "collection", true);

			if (TypeHelpers.IsOrImplements(namedType, TypeLibrary.System.Collections.IEnumerable))
				return new(
					"global::ZodSharp.Optimizations.CollectionCountHelper.GetCount(propertyValue)",
					"collection",
					true
				);
		}

		return new(string.Empty, string.Empty, false);
	}

	static string GetDisplayName(ImmutableArray<AttributeData> attributes, string propertyName)
	{
		var display = DisplayAttributeData.FromAttributeData(attributes);
		return display.Exists && !string.IsNullOrEmpty(display.Name) ? display.Name! : propertyName;
	}

	static Location GetAttributeLocation(AttributeData? attributeData) =>
		attributeData?.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

	static AttributeData? FindAttribute(ImmutableArray<AttributeData> attributes, string metadataName)
	{
		foreach (var attribute in attributes)
		{
			if (attribute.AttributeClass?.MetadataName == metadataName)
				return attribute;
		}

		return null;
	}

	static void AddUnsupportedDataAnnotationsUsage(
		string propertyName,
		AttributeData? attribute,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	) =>
		diagnostics.Add(
			ReportableDiagnostic.Create(
				DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
				true,
				GetAttributeLocation(attribute),
				propertyName
			)
		);

	static void AddUnsupportedDataAnnotationsUsage(
		ImmutableArray<AttributeData> attributes,
		string propertyName,
		string attributeMetadataName,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	) =>
		AddUnsupportedDataAnnotationsUsage(propertyName, FindAttribute(attributes, attributeMetadataName), diagnostics);

	static void AddUnsupportedDataAnnotationsDiagnostics(
		ImmutableArray<AttributeData> attributes,
		string propertyName,
		TypeIdentity propertyType,
		ITypeSymbol originalPropertyType,
		UrlAttribute urlAttribute,
		PhoneAttribute phoneAttribute,
		CreditCardAttributeData creditCardAttribute,
		Base64StringAttributeData base64StringAttribute,
		EmailAddressAttributeData emailAddressAttribute,
		AllowedValuesAttributeData allowedValuesAttribute,
		DeniedValuesAttributeData deniedValuesAttribute,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		var isString = propertyType.SpecialType == SpecialType.System_String;

		if (urlAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(attributes, propertyName, "UrlAttribute", diagnostics);
		if (phoneAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(attributes, propertyName, "PhoneAttribute", diagnostics);
		if (creditCardAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(attributes, propertyName, "CreditCardAttribute", diagnostics);
		if (base64StringAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(attributes, propertyName, "Base64StringAttribute", diagnostics);
		if (emailAddressAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(attributes, propertyName, "EmailAddressAttribute", diagnostics);

		if (
			(allowedValuesAttribute.Exists || deniedValuesAttribute.Exists)
			&& !IsValueSetSupportedType(originalPropertyType)
		)
		{
			var valuesAttribute =
				FindAttribute(attributes, "AllowedValuesAttribute")
				?? FindAttribute(attributes, "DeniedValuesAttribute");
			if (valuesAttribute is not null)
				AddUnsupportedDataAnnotationsUsage(propertyName, valuesAttribute, diagnostics);
		}
	}

	static GeneratorResult<LengthAttributeData> BuildLengthAttributeResult(
		IPropertySymbol property,
		LengthAttributeData lengthData,
		AttributeData? attribute,
		TypeIdentity propertyType,
		ITypeSymbol originalPropertyType
	)
	{
		var supportsLengthAttribute =
			propertyType.SpecialType == SpecialType.System_String
			|| originalPropertyType is IArrayTypeSymbol
			|| TypeHelpers.IsOrImplements(originalPropertyType, TypeLibrary.System.Collections.IEnumerable)
			|| TypeHelpers.IsOrImplements(originalPropertyType, TypeLibrary.System.Collections.Generic.IEnumerable);

		if (attribute is not null && !supportsLengthAttribute)
		{
			return GeneratorResult<LengthAttributeData>.Create(
				lengthData,
				ReportableDiagnostic.Create(
					DiagnosticLibrary.UnsupportedLengthAttributeTarget,
					true,
					GetAttributeLocation(attribute),
					property.Name
				)
			);
		}

		// If the minimum length is greater than the maximum length, this is an invalid configuration.
		return lengthData.MinimumLength > lengthData.MaximumLength
			? GeneratorResult<LengthAttributeData>.Create(
				lengthData,
				ReportableDiagnostic.Create(
					DiagnosticLibrary.InvalidLengthAttribute,
					true,
					GetAttributeLocation(attribute),
					property.Name
				)
			)
			: GeneratorResult<LengthAttributeData>.Create(lengthData);
	}

	static void ValidateCompareProperty(
		IPropertySymbol property,
		ImmutableArray<AttributeData> attributes,
		CompareAttributeData compareAttribute,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		if (!compareAttribute.Exists)
			return;

		var otherProperty = property
			.ContainingType?.GetMembers(compareAttribute.OtherProperty)
			.OfType<IPropertySymbol>()
			.FirstOrDefault();
		if (otherProperty is null)
		{
			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.ComparePropertyNotFound,
					true,
					GetAttributeLocation(FindAttribute(attributes, "CompareAttribute")),
					property.Name,
					compareAttribute.OtherProperty
				)
			);
		}
	}

	static bool IsValueSetSupportedType(ITypeSymbol propertyType)
	{
		var unwrapped = TypeHelpers.UnwrapNullableType(propertyType);
		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum })
			return true;

		// We support value sets for primitive types that can be compared for equality.
		return unwrapped.SpecialType
			is SpecialType.System_String
				or SpecialType.System_Char
				or SpecialType.System_Boolean
				or SpecialType.System_Byte
				or SpecialType.System_SByte
				or SpecialType.System_Int16
				or SpecialType.System_UInt16
				or SpecialType.System_Int32
				or SpecialType.System_UInt32
				or SpecialType.System_Int64
				or SpecialType.System_UInt64
				or SpecialType.System_Single
				or SpecialType.System_Double
				or SpecialType.System_Decimal;
	}

	static void ValidateErrorMessageResourceConfiguration(
		ImmutableArray<AttributeData> attributes,
		string propertyName,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		foreach (var attribute in attributes)
		{
			if (
				attribute.AttributeClass is null
				|| !TypeHelpers.InheritsFrom(
					attribute.AttributeClass,
					TypeLibrary.System.ComponentModel.DataAnnotations.ValidationAttribute
				)
			)
			{
				continue;
			}

			var hasResourceName = false;
			var hasResourceType = false;
			foreach (var namedArgument in attribute.NamedArguments)
			{
				if (
					namedArgument.Key == "ErrorMessageResourceName"
					&& namedArgument.Value.Value is string { Length: > 0 }
				)
					hasResourceName = true;
				else if (namedArgument.Key == "ErrorMessageResourceType" && namedArgument.Value.Value is not null)
					hasResourceType = true;
			}

			if (hasResourceName == hasResourceType)
				continue;

			diagnostics.Add(
				ReportableDiagnostic.Create(
					DiagnosticLibrary.InvalidDataAnnotationsErrorMessage,
					true,
					GetAttributeLocation(attribute),
					propertyName
				)
			);
		}
	}

	// Fallback used when no resolver is available (the analyzer call path): a type declared in source is
	// the type this compilation owns.
	static bool IsSchemaDefinedInSource(INamedTypeSymbol type) =>
		type.Locations.Any(static location => location.IsInSource)
		&& type.TypeKind is TypeKind.Class or TypeKind.Struct
		&& !type.IsAbstract
		&& !type.IsStatic;

	internal static bool TryGetNestedSchemaType(
		IPropertySymbol property,
		ExternalSchemaResolver externalSchemas,
		out INamedTypeSymbol nested
	)
	{
		var propertyType = TypeHelpers.UnwrapNullableType(property.Type);
		if (propertyType is IArrayTypeSymbol array)
			propertyType = array.ElementType;
		else if (propertyType is INamedTypeSymbol named)
		{
			var enumerable = named.AllInterfaces.FirstOrDefault(
				TypeLibrary.System.Collections.Generic.IEnumerable.Equals
			);
			if (enumerable is not null)
				propertyType = enumerable.TypeArguments[0];
		}

		propertyType = TypeHelpers.UnwrapNullableType(propertyType);
		nested = propertyType as INamedTypeSymbol ?? null!;
		return nested is not null && externalSchemas.IsSchemaOwnedByThisCompilation(nested);
	}

	static bool TryBuildRangeBoundaryExpressions(
		ITypeSymbol propertyType,
		RangeAttributeData rangeAttribute,
		out string minimumExpression,
		out string maximumExpression
	)
	{
		if (!rangeAttribute.Exists)
		{
			minimumExpression = string.Empty;
			maximumExpression = string.Empty;

			return false;
		}

		propertyType = TypeHelpers.UnwrapNullableType(propertyType);
		if (TypeHelpers.IsNumericType(propertyType))
			return TryBuildNumericRangeBoundaryExpressions(
				propertyType,
				rangeAttribute,
				out minimumExpression,
				out maximumExpression
			);

		if (
			rangeAttribute.Kind == RangeAttributeKind.Converted
			&& rangeAttribute.Minimum is string minimum
			&& rangeAttribute.Maximum is string maximum
			&& propertyType is INamedTypeSymbol comparableType
			&& IsComparableRangeType(comparableType)
			&& TryBuildComparableBoundaryExpressions(
				comparableType,
				minimum,
				maximum,
				rangeAttribute.ParseLimitsInInvariantCulture,
				out minimumExpression,
				out maximumExpression
			)
		)
		{
			return true;
		}

		minimumExpression = string.Empty;
		maximumExpression = string.Empty;

		return false;
	}

	static bool IsComparableRangeType(INamedTypeSymbol type)
	{
		// Explicit opt-in for comparable types that lack comparison operators or a
		// culture-aware Parse overload (e.g. System.Version).
		if (IsVersion(type))
			return true;

		// For other types, we require that they implement IComparable and have user-defined comparison operators.
		return ImplementsIComparable(type) && HasComparisonOperators(type);
	}

	static bool IsVersion(INamedTypeSymbol type) => TypeHelpers.IsNamedType(type, "System.Version");

	static bool ImplementsIComparable(INamedTypeSymbol type)
	{
		foreach (var iface in type.AllInterfaces)
		{
			if (iface.MetadataName is "IComparable" or "IComparable`1")
				return true;
		}

		return false;
	}

	static bool HasComparisonOperators(INamedTypeSymbol type) =>
		HasUserDefinedOperator(type, "op_LessThan") && HasUserDefinedOperator(type, "op_GreaterThan");

	static bool HasUserDefinedOperator(INamedTypeSymbol type, string name)
	{
		foreach (var member in type.GetMembers(name))
		{
			if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator })
				return true;
		}

		return false;
	}

	static bool TryBuildComparableBoundaryExpressions(
		INamedTypeSymbol propertyType,
		string minimum,
		string maximum,
		bool invariantCulture,
		out string minimumExpression,
		out string maximumExpression
	)
	{
		var typeName = propertyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var cultureExpression = invariantCulture
			? "global::System.Globalization.CultureInfo.InvariantCulture"
			: "global::System.Globalization.CultureInfo.CurrentCulture";

		// Prefer the culture-aware overload, then fall back to a culture-free single-argument
		// Parse (e.g. System.Version).
		string? invocationTemplate = null;
		foreach (var member in propertyType.GetMembers("Parse"))
		{
			if (
				member is IMethodSymbol { IsStatic: true } parseMethod
				&& parseMethod.Parameters.Length == 2
				&& parseMethod.Parameters[0].Type.SpecialType == SpecialType.System_String
				&& parseMethod.Parameters[1].Type is INamedTypeSymbol { MetadataName: "IFormatProvider" }
			)
			{
				invocationTemplate = $"{typeName}.Parse({{0}}, {cultureExpression})";
				break;
			}
		}

		if (invocationTemplate is null)
		{
			foreach (var member in propertyType.GetMembers("Parse"))
			{
				if (
					member is IMethodSymbol { IsStatic: true } parseMethod
					&& parseMethod.Parameters.Length == 1
					&& parseMethod.Parameters[0].Type.SpecialType == SpecialType.System_String
				)
				{
					invocationTemplate = $"{typeName}.Parse({{0}})";
					break;
				}
			}
		}

		if (invocationTemplate is null)
		{
			minimumExpression = string.Empty;
			maximumExpression = string.Empty;

			return false;
		}

		minimumExpression = string.Format(CultureInfo.InvariantCulture, invocationTemplate, minimum.StringLiteral());
		maximumExpression = string.Format(CultureInfo.InvariantCulture, invocationTemplate, maximum.StringLiteral());

		return true;
	}

	static bool TryBuildNumericRangeBoundaryExpressions(
		ITypeSymbol propertyType,
		RangeAttributeData rangeAttribute,
		out string minimumExpression,
		out string maximumExpression
	)
	{
		if (rangeAttribute.Kind == RangeAttributeKind.Int32)
		{
			minimumExpression = ConvertNumericLiteralExpression(propertyType, (int)rangeAttribute.Minimum!);
			maximumExpression = ConvertNumericLiteralExpression(propertyType, (int)rangeAttribute.Maximum!);
			return minimumExpression.Length > 0 && maximumExpression.Length > 0;
		}

		if (rangeAttribute.Kind == RangeAttributeKind.Double)
		{
			minimumExpression = ConvertNumericLiteralExpression(propertyType, (double)rangeAttribute.Minimum!);
			maximumExpression = ConvertNumericLiteralExpression(propertyType, (double)rangeAttribute.Maximum!);
			return minimumExpression.Length > 0 && maximumExpression.Length > 0;
		}

		if (
			rangeAttribute.Kind == RangeAttributeKind.Converted
			&& rangeAttribute.Minimum is string minimum
			&& rangeAttribute.Maximum is string maximum
		)
		{
			minimumExpression = BuildNumericParseExpression(
				propertyType,
				minimum,
				rangeAttribute.ParseLimitsInInvariantCulture
			);
			maximumExpression = BuildNumericParseExpression(
				propertyType,
				maximum,
				rangeAttribute.ParseLimitsInInvariantCulture
			);
			return minimumExpression.Length > 0 && maximumExpression.Length > 0;
		}

		minimumExpression = string.Empty;
		maximumExpression = string.Empty;
		return false;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static string ConvertNumericLiteralExpression(ITypeSymbol propertyType, int value) =>
		propertyType.SpecialType switch
		{
			SpecialType.System_Byte => $"(byte){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_SByte => $"(sbyte){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int16 => $"(short){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_UInt16 => $"(ushort){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int32 => value.ToString(CultureInfo.InvariantCulture),
			SpecialType.System_UInt32 => $"{value.ToString(CultureInfo.InvariantCulture)}U",
			SpecialType.System_Int64 => $"{value.ToString(CultureInfo.InvariantCulture)}L",
			SpecialType.System_UInt64 => $"{value.ToString(CultureInfo.InvariantCulture)}UL",
			SpecialType.System_Single => value.ToString(CultureInfo.InvariantCulture) + "F",
			SpecialType.System_Double => value.ToString(CultureInfo.InvariantCulture) + "D",
			SpecialType.System_Decimal => value.ToString(CultureInfo.InvariantCulture) + "M",
			_ => string.Empty,
		};

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static string ConvertNumericLiteralExpression(ITypeSymbol propertyType, double value) =>
		propertyType.SpecialType switch
		{
			SpecialType.System_Single => $"(float){value.ToString("R", CultureInfo.InvariantCulture)}D",
			SpecialType.System_Double => value.ToString("R", CultureInfo.InvariantCulture) + "D",
			SpecialType.System_Decimal => $"(decimal){value.ToString("R", CultureInfo.InvariantCulture)}D",
			_ => BuildNumericParseExpression(
				propertyType,
				value.ToString("R", CultureInfo.InvariantCulture),
				invariantCulture: true
			),
		};

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static string BuildNumericParseExpression(ITypeSymbol propertyType, string value, bool invariantCulture)
	{
		var cultureExpression = invariantCulture
			? "global::System.Globalization.CultureInfo.InvariantCulture"
			: "global::System.Globalization.CultureInfo.CurrentCulture";

		return propertyType.SpecialType switch
		{
			SpecialType.System_Byte =>
				$"global::System.Byte.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_SByte =>
				$"global::System.SByte.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_Int16 =>
				$"global::System.Int16.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_UInt16 =>
				$"global::System.UInt16.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_Int32 =>
				$"global::System.Int32.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_UInt32 =>
				$"global::System.UInt32.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_Int64 =>
				$"global::System.Int64.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_UInt64 =>
				$"global::System.UInt64.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Integer, {cultureExpression})",
			SpecialType.System_Single =>
				$"global::System.Single.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Float | global::System.Globalization.NumberStyles.AllowThousands, {cultureExpression})",
			SpecialType.System_Double =>
				$"global::System.Double.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Float | global::System.Globalization.NumberStyles.AllowThousands, {cultureExpression})",
			SpecialType.System_Decimal =>
				$"global::System.Decimal.Parse({value.StringLiteral()}, global::System.Globalization.NumberStyles.Number, {cultureExpression})",
			_ => string.Empty,
		};
	}
}

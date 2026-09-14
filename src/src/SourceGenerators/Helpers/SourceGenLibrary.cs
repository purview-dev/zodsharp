using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Helpers;

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
			static (provider, _) => ReadAutoDetectSettings(provider)
		);

		return generationContext
			.CollectWith(
				schemaSets,
				static (outputContext, sets, _) =>
					new SchemaGenerationModel(outputContext) { ZodSchemas = Deduplicate(sets) },
				"CollectZodSchemas"
			)
			.Combine(autoDetectSettings)
			.Select(static (pair, _) => ApplyIValidateOptionsAutoDetection(pair));
	}

	readonly record struct AutoDetectSettings(bool AutoGenerate, EquatableArray<string> Suffixes);

	static AutoDetectSettings ReadAutoDetectSettings(AnalyzerConfigOptionsProvider provider)
	{
		// Auto-detection is on by default; only an explicit "false" disables it.
		var autoGenerate =
			!provider.GlobalOptions.TryGetValue(
				$"build_property:{PropertyLibrary.AutoGenerateIValidateOptionsProperty}",
				out var autoGenerateValue
			)
			|| !bool.TryParse(autoGenerateValue, out var autoGenerateEnabled)
			|| autoGenerateEnabled;

		var suffixes = ParseAutoGenerateIValidateOptionsSuffixes(
			provider.GlobalOptions.TryGetValue(
				$"build_property:{PropertyLibrary.AutoGenerateIValidateOptionsSuffixesProperty}",
				out var suffixValue
			)
				? suffixValue
				: null
		);

		return new(autoGenerate, suffixes);
	}

	static EquatableArray<string> ParseAutoGenerateIValidateOptionsSuffixes(string? value)
	{
		var raw = string.IsNullOrWhiteSpace(value) ? "Options;Settings" : value!;
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var segment in raw.Split(';', ','))
		{
			var suffix = segment.Trim();
			if (suffix.Length > 0)
				builder.Add(suffix);
		}

		return new(builder.ToImmutable());
	}

	static SchemaGenerationModel ApplyIValidateOptionsAutoDetection(
		(SchemaGenerationModel Model, AutoDetectSettings Settings) input
	)
	{
		var (model, settings) = input;
		if (!settings.AutoGenerate || settings.Suffixes.Count == 0)
			return model;

		var builder = ImmutableArray.CreateBuilder<GeneratorResult<ZodSchemaDescriptor>>(model.ZodSchemas.Count);
		foreach (var result in model.ZodSchemas)
		{
			if (!result.ShouldProcess)
			{
				builder.Add(result);
				continue;
			}

			var schema = result.Value;
			if (
				schema.GenerateIValidateOptions is null
				&& schema.IsPrimary
				&& !schema.IsValueType
				&& settings.Suffixes.Any(suffix => schema.TargetType.Name.EndsWith(suffix, StringComparison.Ordinal))
			)
			{
				builder.Add(
					GeneratorResult<ZodSchemaDescriptor>.Create(schema with { GenerateIValidateOptions = true })
				);
			}
			else
			{
				builder.Add(result);
			}
		}

		return model with
		{
			ZodSchemas = new(builder.ToImmutable()),
		};
	}

	static GeneratorResult<SchemaSet> GetSchemasForGeneration(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode, cancellationToken) is not INamedTypeSymbol root)
			return default;

		var schemas = ImmutableArray.CreateBuilder<ZodSchemaDescriptor>();
		HashSet<TypeIdentity> seen = [];
		Queue<(INamedTypeSymbol Symbol, bool IsPrimary)> queue = new();
		queue.Enqueue((root, true));

		while (queue.Count > 0)
		{
			var (symbol, isPrimary) = queue.Dequeue();
			TypeIdentity target = new(symbol);
			if (!seen.Add(target))
				continue;

			var schema = target with { Name = $"{target.Name}Schema" };
			var targetCanBeNull = TypeHelpers.CanBeNull(symbol);
			var properties = GetZodProperties(symbol);
			var accessibility = symbol.ContainingType is null
				? symbol.DeclaredAccessibility == Accessibility.Public
					? TypeDeclarationAccessibility.Public
					: TypeDeclarationAccessibility.Internal
				: symbol.DeclaredAccessibility.ToTypeDeclarationAccessibility();
			var zodSchemaAttribute = ZodSchemaAttributeData.FromAttributeData(symbol, out var attribute);
			var customValidation = ResolveCustomValidationMethod(symbol, zodSchemaAttribute, attribute!);
			var syncValidation = ResolveSyncValidationMethod(symbol, zodSchemaAttribute, attribute!);
			var isValueType = symbol.TypeKind == TypeKind.Struct;
			bool? generateIValidateOptions =
				zodSchemaAttribute.GenerateIValidateOptions ? true
				: zodSchemaAttribute.SuppressIValidateOptions ? false
				: null;

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
					syncValidation,
					generateIValidateOptions,
					zodSchemaAttribute.EnableComposition,
					isPrimary
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
						&& (TypeHelpers.HasDataAnnotationAttribute(m) || IsPropertyWithNestedSchema(m))
					)
			)
			{
				if (TryGetNestedSchemaType(property, out var nested))
					queue.Enqueue((nested, false));
			}
		}

		return GeneratorResult<SchemaSet>.Create(new SchemaSet(schemas.ToImmutable()));
	}

	static bool IsPropertyWithNestedSchema(IPropertySymbol property)
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
		return propertyType is INamedTypeSymbol nested && IsSourceDefinedComplexType(nested);
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

	static EquatableArray<GeneratorResult<ZodPropertyDescriptor>> GetZodProperties(INamedTypeSymbol symbol)
	{
		var properties = symbol
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(property => property.DeclaredAccessibility == Accessibility.Public)
			.Select(static property => GetValidatablePropertyDescriptor(property))
			.ToImmutableArray();

		return new(properties);
	}

	internal static GeneratorResult<ZodPropertyDescriptor> GetValidatablePropertyDescriptor(IPropertySymbol property)
	{
		var propertyType = CreateTypeIdentity(property.Type);
		var originalPropertyType = property.Type;
		var propertyCanBeNull = TypeHelpers.CanBeNull(originalPropertyType);
		if (
			originalPropertyType is INamedTypeSymbol
			{
				OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
			} nullableType
		)
		{
			propertyType = new(nullableType.TypeArguments[0]);
			originalPropertyType = nullableType.TypeArguments[0];
		}

		var diagnostics = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		var validationKind = GetPropertyValidationKind(propertyType, originalPropertyType);
		var elementType =
			validationKind == PropertyValidationKind.Collection
				? GetCollectionElementTypeIdentity(originalPropertyType)
				: null;
		var elementTypeCanBeNull =
			validationKind == PropertyValidationKind.Collection
			&& elementType is not null
			&& TypeHelpers.CanBeNull(GetCollectionElementTypeSymbol(originalPropertyType) ?? originalPropertyType);
		var nestedSchemaType = GetNestedSchemaTypeIdentity(property, validationKind);
		var lengthAccessor = ClassifyLengthAccessor(originalPropertyType);
		var displayName = GetDisplayName(property);

		var displayAttribute = DisplayAttributeData.FromAttributeData(property);
		var requiredAttribute = RequiredAttributeData.FromAttributeData(property);
		var compareAttribute = CompareAttributeData.FromAttributeData(property);
		var emailAddressAttribute = EmailAddressAttributeData.FromAttributeData(property);
		var creditCardAttribute = CreditCardAttributeData.FromAttributeData(property);
		var phoneAttribute = PhoneAttribute.FromAttributeData(property);
		var urlAttribute = UrlAttribute.FromAttributeData(property);
		var stringLengthAttribute = StringLengthAttribute.FromAttributeData(property);
		var minLengthAttribute = MinLengthAttributeData.FromAttributeData(property);
		var maxLengthAttribute = MaxLengthAttributeData.FromAttributeData(property);
		var regularExpressionAttribute = GeneratorResult<RegularExpressionAttributeData>.Empty;
		if (RegularExpressionAttributeData.TryFromAttributeData(property, out var regexData, out var attribute))
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

		var base64StringAttribute = Base64StringAttributeData.FromAttributeData(property);
		var deniedValuesAttribute = DeniedValuesAttributeData.FromAttributeData(property);
		var allowedValuesAttribute = AllowedValuesAttributeData.FromAttributeData(property);

		AddUnsupportedDataAnnotationsDiagnostics(
			property,
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
		if (LengthAttributeData.TryFromAttributeData(property, out var lengthData, out attribute))
		{
			lengthAttribute = BuildLengthAttributeResult(
				property,
				lengthData,
				attribute,
				propertyType,
				originalPropertyType
			);
		}

		var rangeAttribute = RangeAttributeData.FromAttributeData(property.GetAttributes(), out attribute);
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

		ValidateCompareProperty(property, compareAttribute, diagnostics);

		ValidateErrorMessageResourceConfiguration(property, diagnostics);

		diagnostics.AddRange(regularExpressionAttribute.Diagnostics);
		diagnostics.AddRange(lengthAttribute.Diagnostics);
		if (rangeAttribute.Exists)
			diagnostics.AddRange(rangeAttributeResult.Diagnostics);

		var isEnum =
			TypeHelpers.UnwrapNullableType(originalPropertyType) is INamedTypeSymbol { TypeKind: TypeKind.Enum };

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
				isEnum,
				validationKind,
				elementType,
				elementTypeCanBeNull,
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
				)
			),
			diagnostics.ToImmutable()
		);
	}

	static TypeIdentity CreateTypeIdentity(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is IArrayTypeSymbol arrayType)
		{
			var elementIdentity = CreateTypeIdentity(arrayType.ElementType);
			return new TypeIdentity($"{elementIdentity.Name}[]", elementIdentity.Namespace);
		}

		return new TypeIdentity(typeSymbol);
	}

	static PropertyValidationKind GetPropertyValidationKind(TypeIdentity propertyType, ITypeSymbol originalType)
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

		// If the original type is a source-defined complex type, we can generate a nested schema for it.
		if (originalType is INamedTypeSymbol namedType && IsSourceDefinedComplexType(namedType))
			return PropertyValidationKind.Complex;

		// Comparable structs/classes (TimeSpan, DateTime, DateTimeOffset, DateOnly, TimeOnly, Version, ...)
		// can be range-validated without a nested schema.
		if (originalType is INamedTypeSymbol comparableType && IsComparableRangeType(comparableType))
			return PropertyValidationKind.Comparable;

		// If the original type is an enum, we can validate it against allowed/denied values.
		return PropertyValidationKind.Unsupported;
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
				return new TypeIdentity(iface.TypeArguments[0]);
		}

		return
			namedType.IsGenericType
			&& TypeHelpers.Implements(namedType, TypeLibrary.System.Collections.Generic.IEnumerable)
			? new TypeIdentity(namedType.TypeArguments[0])
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

	static TypeIdentity? GetNestedSchemaTypeIdentity(IPropertySymbol property, PropertyValidationKind validationKind)
	{
		var targetType =
			validationKind == PropertyValidationKind.Collection
				? GetCollectionElementTypeSymbol(property.Type)
				: property.Type;

		if (targetType is not INamedTypeSymbol namedType || !IsSourceDefinedComplexType(namedType))
			return null;

		TypeIdentity identity = new(namedType);
		return identity with { Name = $"{identity.Name}Schema" };
	}

	static LengthAccessor ClassifyLengthAccessor(ITypeSymbol propertyType)
	{
		if (propertyType.SpecialType == SpecialType.System_String || propertyType is IArrayTypeSymbol)
			return new("propertyValue.Length", "array", true);

		if (propertyType is INamedTypeSymbol namedType)
		{
			if (TypeHelpers.IsOrImplements(namedType, TypeLibrary.System.Collections.Generic.ICollection))
				return new("propertyValue.Count", "array", true);

			if (TypeHelpers.IsOrImplements(namedType, TypeLibrary.System.Collections.IEnumerable))
				return new(
					"global::ZodSharp.Optimizations.CollectionCountHelper.GetCount(propertyValue)",
					"array",
					true
				);
		}

		return new(string.Empty, string.Empty, false);
	}

	static string GetDisplayName(IPropertySymbol property)
	{
		var display = DisplayAttributeData.FromAttributeData(property);
		return display.Exists && !string.IsNullOrEmpty(display.Name) ? display.Name! : property.Name;
	}

	static Location GetAttributeLocation(AttributeData? attributeData) =>
		attributeData?.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

	static AttributeData? FindAttribute(IPropertySymbol property, string metadataName)
	{
		foreach (var attribute in property.GetAttributes())
		{
			if (attribute.AttributeClass?.MetadataName == metadataName)
				return attribute;
		}

		return null;
	}

	static void AddUnsupportedDataAnnotationsUsage(
		IPropertySymbol property,
		AttributeData? attribute,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	) =>
		diagnostics.Add(
			ReportableDiagnostic.Create(
				DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
				true,
				GetAttributeLocation(attribute),
				property.Name
			)
		);

	static void AddUnsupportedDataAnnotationsUsage(
		IPropertySymbol property,
		string attributeMetadataName,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	) => AddUnsupportedDataAnnotationsUsage(property, FindAttribute(property, attributeMetadataName), diagnostics);

	static void AddUnsupportedDataAnnotationsDiagnostics(
		IPropertySymbol property,
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
			AddUnsupportedDataAnnotationsUsage(property, "UrlAttribute", diagnostics);
		if (phoneAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(property, "PhoneAttribute", diagnostics);
		if (creditCardAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(property, "CreditCardAttribute", diagnostics);
		if (base64StringAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(property, "Base64StringAttribute", diagnostics);
		if (emailAddressAttribute.Exists && !isString)
			AddUnsupportedDataAnnotationsUsage(property, "EmailAddressAttribute", diagnostics);

		if (
			(allowedValuesAttribute.Exists || deniedValuesAttribute.Exists)
			&& !IsValueSetSupportedType(originalPropertyType)
		)
		{
			var valuesAttribute =
				FindAttribute(property, "AllowedValuesAttribute") ?? FindAttribute(property, "DeniedValuesAttribute");
			if (valuesAttribute is not null)
				AddUnsupportedDataAnnotationsUsage(property, valuesAttribute, diagnostics);
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
					GetAttributeLocation(FindAttribute(property, "CompareAttribute")),
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
		IPropertySymbol property,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		foreach (var attribute in property.GetAttributes())
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
					property.Name
				)
			);
		}
	}

	static bool IsSourceDefinedComplexType(INamedTypeSymbol type) =>
		type.Locations.Any(static location => location.IsInSource)
		&& type.TypeKind is TypeKind.Class or TypeKind.Struct
		&& !type.IsAbstract
		&& !type.IsStatic;

	static bool TryGetNestedSchemaType(IPropertySymbol property, out INamedTypeSymbol nested)
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
		return nested is not null && IsSourceDefinedComplexType(nested);
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

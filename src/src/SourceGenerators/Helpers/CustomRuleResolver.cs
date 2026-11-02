using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Resolves DataAnnotations-style validation attributes that carry <c>[ZodRule(typeof(...))]</c> into
/// <see cref="CustomRuleDescriptor"/> values the schema generator can emit.
/// </summary>
/// <remarks>
/// <para>
/// Only attributes derived from <c>System.ComponentModel.DataAnnotations.ValidationAttribute</c> are
/// considered, so custom rules participate in the same "a property is validated when it carries a data
/// annotation" discovery as the built-in attributes.
/// </para>
/// <para>
/// The applied attribute's constructor arguments are mapped positionally and its named arguments by name
/// (case-insensitive) to the rule's public constructor parameters. A parameter named <c>message</c> is
/// supplied from the attribute's <c>ErrorMessage</c> when one is set.
/// </para>
/// </remarks>
static class CustomRuleResolver
{
	/// <summary>
	/// Resolves the custom rules declared by <paramref name="symbol"/>, discarding the diagnostics a failed
	/// resolution produces.
	/// </summary>
	/// <param name="symbol">The symbol whose rule attributes are resolved.</param>
	/// <param name="ruleTargetType">The type the rules are applied to.</param>
	/// <param name="compilation">The compilation, used to construct a <see cref="Nullable{T}"/> value type.</param>
	/// <param name="isTypeLevel">
	/// Whether the rules are applied to <paramref name="ruleTargetType"/> itself rather than to a member. A
	/// type-level rule on a scalar reports an empty path, so its default message names the scalar.
	/// </param>
	/// <returns>The resolved rule descriptors.</returns>
	/// <remarks>
	/// Used by the generator, which only needs the descriptors: the same resolution runs in
	/// <c>ZodSchemaAnalyzer</c>, which reports the diagnostics, so a rule that resolves to nothing is still
	/// visible in the build.
	/// </remarks>
	public static EquatableArray<CustomRuleDescriptor> Resolve(
		ISymbol symbol,
		ITypeSymbol ruleTargetType,
		Compilation compilation,
		bool isTypeLevel = false
	) =>
		Resolve(symbol, ruleTargetType, compilation, ImmutableArray.CreateBuilder<ReportableDiagnostic>(), isTypeLevel);

	public static EquatableArray<CustomRuleDescriptor> Resolve(
		ISymbol symbol,
		ITypeSymbol ruleTargetType,
		Compilation compilation,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics,
		bool isTypeLevel = false
	) => Resolve(symbol.GetAttributes(), ruleTargetType, compilation, diagnostics, isTypeLevel);

	/// <summary>
	/// Resolves the custom rules declared by <paramref name="attributes"/>. The attributes are supplied by the
	/// caller rather than read from a symbol so a positional record property can contribute the attributes of
	/// the primary-constructor parameter it is synthesized from, which the compiler does not copy onto the
	/// property when the attribute is valid on both targets.
	/// </summary>
	/// <param name="attributes">The validation attributes to resolve.</param>
	/// <param name="ruleTargetType">The type the rules are applied to.</param>
	/// <param name="compilation">The compilation, used to construct a <see cref="Nullable{T}"/> value type.</param>
	/// <param name="diagnostics">The diagnostics a failed resolution reports into.</param>
	/// <param name="isTypeLevel">
	/// Whether the rules are applied to <paramref name="ruleTargetType"/> itself rather than to a member. A
	/// type-level rule on a scalar reports an empty path, so its default message names the scalar.
	/// </param>
	/// <returns>The resolved rule descriptors.</returns>
	public static EquatableArray<CustomRuleDescriptor> Resolve(
		ImmutableArray<AttributeData> attributes,
		ITypeSymbol ruleTargetType,
		Compilation compilation,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics,
		bool isTypeLevel = false
	)
	{
		ImmutableArray<CustomRuleDescriptor>.Builder? builder = null;

		// A rule applied at the type level of a scalar value object reports an empty path, so the generic
		// subject word in its message ("Value") is replaced with the scalar's name. The substitution only
		// applies when the attribute supplies no message of its own.
		var scalarSubject =
			isTypeLevel && ruleTargetType is INamedTypeSymbol owner && TryGetScalarAttribute(owner, out _)
				? owner.Name
				: null;

		foreach (var attribute in attributes)
		{
			if (attribute.AttributeClass is not INamedTypeSymbol attributeClass)
				continue;

			// The built-in DataAnnotations attributes have dedicated emitters.
			if (IsBuiltInDataAnnotation(attributeClass))
				continue;

			// So do the collection-count attributes on a collection: they are emitted through the [MinLength]/
			// [MaxLength]/[Length] collection-size path (SourceGenLibrary.ApplyCollectionCountAttributes).
			if (IsCollectionCountAttribute(attributeClass) && IsCountableCollectionType(ruleTargetType))
				continue;

			if (!TryGetRuleMapping(attributeClass, out var mapping))
				continue;

			if (
				!TryResolveRule(
					mapping.RuleType,
					ruleTargetType,
					compilation,
					out var ruleType,
					out var scalarTarget,
					out var scalarValue,
					out var guardedScalarPropertyName
				)
			)
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.UnsupportedCustomRuleTarget,
						true,
						GetAttributeLocation(attribute),
						mapping.RuleType.Name,
						attributeClass.Name,
						ruleTargetType.ToDisplayString()
					)
				);
				continue;
			}

			// An adapted rule is a ScalarRuleAdapter the value-object generator emits into this compilation,
			// which this generator cannot see. Its contract is the underlying rule validating the scalar's
			// value, already verified during resolution, so only the direct path is checked here.
			if (scalarTarget is null && !ImplementsRuleFor(ruleType, ruleTargetType))
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.UnsupportedCustomRuleTarget,
						true,
						GetAttributeLocation(attribute),
						ruleType.Name,
						attributeClass.Name,
						ruleTargetType.ToDisplayString()
					)
				);
				continue;
			}

			// The subject-substituted default message, used only when the attribute supplies no message of its
			// own. It is derived from the resolved rule's own MessageFormat so the wording stays the rule's.
			var subjectMessage =
				scalarSubject is not null && GetRuleMessageFormat(ruleType) is { } messageFormat
					? SubstituteSubject(messageFormat, scalarSubject)
					: null;

			if (
				!TryBuildArguments(
					attribute,
					ruleType,
					subjectMessage,
					out var arguments,
					out var unmappedParameterName
				)
			)
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.UnmappableCustomRuleArgument,
						true,
						GetAttributeLocation(attribute),
						unmappedParameterName!,
						ruleType.Name,
						attributeClass.Name
					)
				);
				continue;
			}

			// A guarded rule is resolved against the scalar's unwrapped value type, so it is instantiated
			// directly and receives that value only when it is present. An adapted rule is wrapped in the
			// ScalarRuleAdapter so it can validate the value object as a unit.
			var isGuardedScalarValue = guardedScalarPropertyName is not null;
			var isAdapted = scalarTarget is not null && !isGuardedScalarValue;

			builder ??= ImmutableArray.CreateBuilder<CustomRuleDescriptor>();
			builder.Add(
				new CustomRuleDescriptor(
					isAdapted
						? BuildScalarAdapterIdentity(scalarTarget!, scalarValue!, ruleType)
						: new TypeIdentity(ruleType),
					isAdapted ? new TypeIdentity(ruleType) : null,
					GetAttributeString(attribute, "Code") ?? mapping.Code,
					GetAttributeString(attribute, "Origin") ?? mapping.Origin,
					ValidationAttributeData.FromAttributeData(attribute),
					arguments,
					IsZodRule(ruleType),
					TypeHelpers.IsOrImplements(ruleType, TypeLibrary.ZodSharp.Core.IRequiredRule),
					guardedScalarPropertyName
				)
			);

			foreach (var unusedArgument in GetUnusedNamedArguments(attribute, attributeClass, ruleType))
			{
				diagnostics.Add(
					ReportableDiagnostic.Create(
						DiagnosticLibrary.UnusedRuleAttributeArgument,
						false,
						GetAttributeLocation(attribute),
						unusedArgument,
						attributeClass.Name,
						ruleType.Name
					)
				);
			}
		}

		return builder is null ? new(ImmutableArray<CustomRuleDescriptor>.Empty) : new(builder.ToImmutable());
	}

	static bool IsBuiltInDataAnnotation(INamedTypeSymbol attributeClass) =>
		attributeClass.ContainingNamespace.ToDisplayString() == TypeLibraryGenerator.SystemDataAnnotations;

	/// <summary>
	/// Determines whether <paramref name="attributeClass"/> is one of the ZodSharp collection-count attributes
	/// (<c>[MinCount]</c>, <c>[MaxCount]</c>, <c>[CountRange]</c>) generated from the built-in count rules.
	/// </summary>
	/// <param name="attributeClass">The attribute type to test.</param>
	/// <returns><see langword="true"/> for a collection-count attribute.</returns>
	internal static bool IsCollectionCountAttribute(INamedTypeSymbol attributeClass) =>
		attributeClass.Name is "MinCountAttribute" or "MaxCountAttribute" or "CountRangeAttribute"
		&& attributeClass.ContainingNamespace.ToDisplayString() == TypeLibraryGenerator.ZodSharpRulesNamespace;

	/// <summary>
	/// Determines whether <paramref name="type"/> is collection-shaped: an array or a non-string type that
	/// implements <c>IEnumerable</c>, so its element count is what a size rule constrains.
	/// </summary>
	/// <param name="type">The member type, optionally <see cref="Nullable{T}"/>.</param>
	/// <returns><see langword="true"/> for a collection-shaped type.</returns>
	internal static bool IsCountableCollectionType(ITypeSymbol type)
	{
		type = TypeHelpers.UnwrapNullableType(type);

		return type is IArrayTypeSymbol
			|| (
				type.SpecialType != SpecialType.System_String
				&& (
					TypeHelpers.IsOrImplements(type, TypeLibrary.System.Collections.IEnumerable)
					|| TypeHelpers.IsOrImplements(type, TypeLibrary.System.Collections.Generic.IEnumerable)
				)
			);
	}

	/// <summary>
	/// Determines whether <paramref name="attributeClass"/> is mapped to a validation rule through
	/// <c>[ZodRule(typeof(...))]</c>. Used by the analyzer to flag rule attributes that can never run.
	/// </summary>
	/// <param name="attributeClass">The attribute type to test.</param>
	/// <returns><see langword="true"/> when the attribute is mapped to a rule.</returns>
	internal static bool IsRuleMapped(INamedTypeSymbol attributeClass) => TryGetRuleMapping(attributeClass, out _);

	/// <summary>
	/// Determines whether <paramref name="ruleType"/> carries the parameterless <c>[ZodRule]</c> marker that
	/// asks the generator to emit a matching validation attribute.
	/// </summary>
	/// <param name="ruleType">The rule type to test.</param>
	/// <returns><see langword="true"/> when the rule is marked for validation-attribute generation.</returns>
	internal static bool IsRuleMarker(INamedTypeSymbol ruleType)
	{
		foreach (var attribute in ruleType.GetAttributes())
		{
			if (IsRuleMarkerAttribute(attribute))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Determines whether <paramref name="attribute"/> is the parameterless <c>[ZodRule]</c> marker (the form
	/// that marks a rule) rather than the mapping form that carries a rule type.
	/// </summary>
	/// <param name="attribute">The attribute to test.</param>
	/// <returns><see langword="true"/> when the attribute marks a rule for attribute generation.</returns>
	internal static bool IsRuleMarkerAttribute(AttributeData attribute) =>
		attribute.AttributeClass is not null
		&& attribute.AttributeClass.ToDisplayString()
			== $"{TypeLibraryGenerator.ZodSharpCoreNamespace}.ZodRuleAttribute"
		&& attribute.ConstructorArguments.Length == 0;

	/// <summary>
	/// Resolves the rule to instantiate for <paramref name="targetType"/>. A scalar value object is
	/// validated through its underlying value, so a rule that validates that value is adapted to the value
	/// object; a rule written against the value object itself keeps the direct path.
	/// </summary>
	/// <param name="ruleType">The rule type declared by the mapping.</param>
	/// <param name="targetType">The type the rule is applied to.</param>
	/// <param name="compilation">The compilation, used to construct a <see cref="Nullable{T}"/> value type.</param>
	/// <param name="instantiated">The rule type to instantiate (the wrapped rule when adapted).</param>
	/// <param name="scalarTarget">The scalar value object to adapt for, or <see langword="null"/>.</param>
	/// <param name="scalarValue">The scalar's underlying value type, or <see langword="null"/>.</param>
	/// <param name="guardedScalarPropertyName">
	/// When the underlying value is a <see cref="Nullable{T}"/>, the name of the scalar property holding it.
	/// The rule is then instantiated directly against the unwrapped value type and emitted under a non-null
	/// guard, rather than adapted to the value object.
	/// </param>
	/// <returns><see langword="true"/> when a usable rule was resolved.</returns>
	/// <remarks>
	/// A nullable value scalar is validated the same way a <c>Nullable&lt;T&gt;</c> property is: the rule is
	/// closed over the non-nullable value type and skipped when the value is absent. This is what lets rules
	/// with value-type constraints (for example <c>MinValueRule&lt;T&gt;</c>, where
	/// <c>T : IComparable&lt;T&gt;</c>) resolve, because <c>Nullable&lt;T&gt;</c> does not implement them.
	/// A rule written explicitly against the nullable type still takes the adapted path.
	/// </remarks>
	static bool TryResolveRule(
		INamedTypeSymbol ruleType,
		ITypeSymbol targetType,
		Compilation compilation,
		out INamedTypeSymbol instantiated,
		out INamedTypeSymbol? scalarTarget,
		out ITypeSymbol? scalarValue,
		out string? guardedScalarPropertyName
	)
	{
		scalarTarget = null;
		scalarValue = null;
		guardedScalarPropertyName = null;

		if (TryGetScalarUnderlyingValue(targetType, compilation, out var valueType, out var scalarPropertyName))
		{
			var unwrappedValueType = TypeHelpers.UnwrapNullableType(valueType);
			if (
				valueType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
				&& TryResolveUnderlyingRule(ruleType, unwrappedValueType, out var guardedRule)
			)
			{
				instantiated = guardedRule;
				scalarTarget = (INamedTypeSymbol)targetType;
				scalarValue = unwrappedValueType;
				guardedScalarPropertyName = scalarPropertyName;
				return true;
			}

			if (TryResolveUnderlyingRule(ruleType, valueType, out var underlyingRule))
			{
				instantiated = underlyingRule;
				scalarTarget = (INamedTypeSymbol)targetType;
				scalarValue = valueType;
				return true;
			}
		}

		return TryResolveRuleType(ruleType, targetType, out instantiated);
	}

	/// <summary>
	/// Resolves <paramref name="ruleType"/> against a scalar's underlying value: an unbound generic is closed
	/// with the value type and a plain rule is used as-is, in both cases only when the result validates that
	/// value.
	/// </summary>
	/// <param name="ruleType">The rule type declared by the mapping.</param>
	/// <param name="valueType">The scalar's underlying value type.</param>
	/// <param name="resolved">The rule type that validates the value.</param>
	/// <returns><see langword="true"/> when a usable rule was resolved.</returns>
	static bool TryResolveUnderlyingRule(
		INamedTypeSymbol ruleType,
		ITypeSymbol valueType,
		out INamedTypeSymbol resolved
	)
	{
		resolved = ruleType;

		if (IsOpenGeneric(ruleType))
		{
			var definition = ruleType.OriginalDefinition;
			if (definition is null || definition.Arity != 1 || !SatisfiesConstraints(definition, valueType))
				return false;

			var closed = definition.Construct(valueType);
			if (!ImplementsRuleFor(closed, valueType))
				return false;

			resolved = closed;
			return true;
		}

		return ImplementsRuleFor(ruleType, valueType);
	}

	/// <summary>
	/// Describes the Purview.ValueObjects <c>[Scalar]</c> attribute applied to a type: the manual form
	/// (the author declares the underlying property) or one of the automatic forms
	/// (<c>[Scalar&lt;TValue&gt;]</c> or <c>[Scalar(typeof(TValue))]</c>, where the value-object generator
	/// declares it and ZodSharp cannot see the member).
	/// </summary>
	/// <param name="PropertyName">The name of the property holding the underlying value (default <c>Value</c>).</param>
	/// <param name="IsAutomatic">Whether the value-object generator owns the underlying property.</param>
	/// <param name="ValueType">The declared underlying type for an automatic form; otherwise <see langword="null"/>.</param>
	/// <param name="Nullable">
	/// Whether the automatic form declares the property as nullable: a nullable reference annotation for a
	/// reference type, or a <see cref="Nullable{T}"/> wrapper for a value type.
	/// </param>
	internal readonly record struct ScalarAttributeInfo(
		string PropertyName,
		bool IsAutomatic,
		ITypeSymbol? ValueType,
		bool Nullable
	);

	/// <summary>
	/// Finds the Purview.ValueObjects <c>[Scalar]</c> attribute applied to <paramref name="type"/>, generic
	/// or non-generic. Detection is by metadata name, so it does not require a reference to the value-object
	/// runtime.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <param name="info">The parsed attribute, when one is applied.</param>
	/// <returns><see langword="true"/> when the type is a scalar value object.</returns>
	/// <remarks>
	/// The underlying type comes from the generic type argument (<c>[Scalar&lt;TValue&gt;]</c>) or from the
	/// <c>typeof(...)</c> constructor argument (<c>[Scalar(typeof(TValue))]</c>); neither selects the manual
	/// form. <c>Nullable</c> is declared on the shared <c>ScalarOptionsAttribute</c> base, so it is read by
	/// name and applies to both automatic forms.
	/// </remarks>
	internal static bool TryGetScalarAttribute(INamedTypeSymbol type, out ScalarAttributeInfo info)
	{
		foreach (var attribute in type.GetAttributes())
		{
			if (
				attribute.AttributeClass
				is not { Name: TypeLibraryGenerator.ScalarAttributeName, Arity: <= 1 } attributeClass
			)
			{
				continue;
			}

			if (
				attributeClass.ContainingNamespace.ToDisplayString()
				!= TypeLibraryGenerator.ValueObjectsSerializationNamespace
			)
			{
				continue;
			}

			var propertyName = "Value";
			ITypeSymbol? valueType = null;
			var parameters = attribute.AttributeConstructor?.Parameters ?? default;

			for (var index = 0; index < attribute.ConstructorArguments.Length; index++)
			{
				var argument = attribute.ConstructorArguments[index];

				if (argument.Kind == TypedConstantKind.Type && argument.Value is ITypeSymbol declaredType)
				{
					valueType = declaredType;
					continue;
				}

				// The property name is mapped by the constructor parameter's name, because the automatic
				// `[Scalar(typeof(T), "Name")]` form carries the type as the first argument. When the
				// constructor (and therefore the parameter names) is unavailable, the first string argument is
				// treated as the property name.
				if (argument.Value is string name && !string.IsNullOrWhiteSpace(name))
				{
					var parameterName =
						!parameters.IsDefaultOrEmpty && index < parameters.Length ? parameters[index].Name : null;

					if (parameterName is null || string.Equals(parameterName, "propertyName", StringComparison.Ordinal))
					{
						propertyName = name;
					}
				}
			}

			// The generic form carries the underlying type as its type argument.
			if (valueType is null && attributeClass.Arity == 1)
				valueType = attributeClass.TypeArguments[0];

			var nullable = false;
			foreach (var pair in attribute.NamedArguments)
			{
				if (pair.Key == "Nullable" && pair.Value.Value is bool isNullable)
				{
					nullable = isNullable;
					break;
				}
			}

			info = new(propertyName, valueType is not null, valueType, nullable);
			return true;
		}

		info = default;
		return false;
	}

	/// <summary>
	/// Gets the underlying value type of a scalar value object. The manual form exposes a public property
	/// for the value; the automatic forms declare the type in the attribute and the value-object generator
	/// owns the property, so the type is read from the attribute instead.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <param name="compilation">The compilation, used to construct a <see cref="Nullable{T}"/> value type.</param>
	/// <param name="valueType">The scalar's underlying value type, when it is a scalar.</param>
	/// <param name="propertyName">The name of the property holding the scalar's underlying value.</param>
	/// <returns><see langword="true"/> when <paramref name="type"/> is a scalar value object.</returns>
	internal static bool TryGetScalarUnderlyingValue(
		ITypeSymbol type,
		Compilation compilation,
		out ITypeSymbol valueType,
		out string propertyName
	)
	{
		valueType = null!;
		propertyName = string.Empty;

		if (type is not INamedTypeSymbol named || !TryGetScalarAttribute(named, out var scalar))
			return false;

		propertyName = scalar.PropertyName;

		foreach (var member in named.GetMembers(scalar.PropertyName))
		{
			if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property)
			{
				valueType = property.Type;
				return true;
			}
		}

		if (scalar.ValueType is null)
			return false;

		valueType = AnnotateScalarValueType(scalar, compilation);
		return true;
	}

	/// <summary>
	/// Gets the underlying type and property name of an automatic scalar value object
	/// (<c>[Scalar&lt;TValue&gt;]</c> or <c>[Scalar(typeof(TValue))]</c>), where the value-object generator
	/// declares the property. Used to synthesize the schema's property without the member being visible.
	/// </summary>
	/// <param name="type">The type to inspect.</param>
	/// <param name="compilation">The compilation, used to construct a <see cref="Nullable{T}"/> value type.</param>
	/// <param name="propertyName">The property the value-object generator declares.</param>
	/// <param name="valueType">The declared underlying type, with the nullable option applied.</param>
	/// <returns><see langword="true"/> when <paramref name="type"/> uses an automatic scalar form.</returns>
	internal static bool TryGetAutomaticScalar(
		ITypeSymbol type,
		Compilation compilation,
		out string propertyName,
		out ITypeSymbol valueType
	)
	{
		propertyName = string.Empty;
		valueType = null!;

		if (
			type is not INamedTypeSymbol named
			|| !TryGetScalarAttribute(named, out var scalar)
			|| scalar.ValueType is null
		)
		{
			return false;
		}

		propertyName = scalar.PropertyName;
		valueType = AnnotateScalarValueType(scalar, compilation);
		return true;
	}

	/// <summary>
	/// Applies the automatic form's <c>Nullable</c> option, so the resolved rule and the synthesized schema
	/// property match the value-object generator's <c>IScalarValueObject&lt;TSelf, TValue&gt;</c> construction.
	/// A reference type gains the nullable annotation; a non-nullable value type is wrapped in
	/// <see cref="Nullable{T}"/>. An already-nullable value type is left alone so
	/// <c>[Scalar&lt;int?&gt;(Nullable = true)]</c> does not double-wrap, and a ref-like struct is left alone
	/// because <see cref="Nullable{T}"/> cannot be constructed over it.
	/// </summary>
	static ITypeSymbol AnnotateScalarValueType(ScalarAttributeInfo scalar, Compilation compilation)
	{
		if (!scalar.Nullable)
			return scalar.ValueType!;

		if (scalar.ValueType is { IsReferenceType: true } referenceType)
			return referenceType.WithNullableAnnotation(NullableAnnotation.Annotated);

		if (
			scalar.ValueType is not { IsValueType: true } valueType
			|| valueType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
			|| valueType is INamedTypeSymbol { IsRefLikeType: true }
		)
		{
			return scalar.ValueType!;
		}

		// The value type is non-nullable and not ref-like, so wrap it in Nullable<T> to match the value-object
		// generator's construction of IScalarValueObject<TSelf, TValue> for a nullable scalar.
		return compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(valueType);
	}

	/// <summary>
	/// Builds the <c>Purview.ValueObjects.ScalarRuleAdapter&lt;TSelf, TValue, TRule&gt;</c> identity the
	/// adapted rule is emitted as. The value argument is carried as a <c>TypeReference</c> rather than a
	/// <c>TypeIdentity</c> because an identity drops nullable reference annotations: a
	/// scalar backed by <c>string?</c> would otherwise be closed over <c>NonSentinelRule&lt;string?&gt;</c>
	/// but constrained by <c>IValidationRule&lt;string&gt;</c>, which the invariant contract rejects
	/// (CS8631).
	/// </summary>
	static TypeIdentity BuildScalarAdapterIdentity(
		INamedTypeSymbol scalar,
		ITypeSymbol valueType,
		INamedTypeSymbol underlyingRule
	) =>
		new TypeIdentity(
			TypeLibraryGenerator.ScalarRuleAdapterName,
			TypeLibraryGenerator.ValueObjectsNamespace,
			3
		).MakeGeneric(
			new TypeIdentity(scalar).AsTypeReference(),
			TypeReference.Create(valueType),
			new TypeIdentity(underlyingRule).AsTypeReference()
		);

	/// <summary>
	/// Resolves the rule type to instantiate: a plain type is used as-is, an unbound generic
	/// (<c>[ZodRule(typeof(NotEmptyRule&lt;&gt;))]</c>) is closed with the property type, and a rule that cannot
	/// validate the property type falls back to another member of its family (the same base name declared
	/// alongside it) so a single attribute can serve both primitive members and scalar value objects.
	/// </summary>
	/// <param name="ruleType">The rule type declared by the mapping.</param>
	/// <param name="propertyType">The property type used to close an unbound generic rule.</param>
	/// <param name="resolved">The rule type to instantiate.</param>
	/// <returns><see langword="true"/> when a usable rule type was resolved.</returns>
	static bool TryResolveRuleType(INamedTypeSymbol ruleType, ITypeSymbol propertyType, out INamedTypeSymbol resolved)
	{
		resolved = ruleType;

		if (IsOpenGeneric(ruleType))
		{
			var definition = ruleType.OriginalDefinition;
			if (definition is null)
				return false;

			if (definition.Arity == 1 && SatisfiesConstraints(definition, propertyType))
			{
				resolved = definition.Construct(propertyType);
				return true;
			}
		}
		else if (ImplementsRuleFor(ruleType, propertyType))
		{
			return true;
		}

		// The declared rule cannot validate the property type. Prefer another member of the rule family under
		// the same base name: NonWhiteSpaceStringRule<TSelf> applied to a string member resolves to
		// NonWhiteSpaceStringRule, and NonWhiteSpaceStringRule applied to a scalar value object resolves to
		// NonWhiteSpaceStringRule<TScalar>. The caller still verifies the resolved rule implements
		// IValidationRule<TProperty>.
		return TryResolveFamilyMember(ruleType.OriginalDefinition ?? ruleType, propertyType, out resolved);
	}

	/// <summary>
	/// Finds the rule family member that can validate <paramref name="propertyType"/>: the non-generic rule
	/// (for example <c>NotEmptyRule</c>) or an arity-1 generic rule closed with the property type (for example
	/// <c>NotEmptyRule&lt;TScalar&gt;</c>).
	/// </summary>
	/// <param name="definition">The rule declared by the mapping.</param>
	/// <param name="propertyType">The property type the rule must validate.</param>
	/// <param name="resolved">The family member that validates the property type, when one exists.</param>
	/// <returns><see langword="true"/> when a usable family member was found.</returns>
	static bool TryResolveFamilyMember(
		INamedTypeSymbol definition,
		ITypeSymbol propertyType,
		out INamedTypeSymbol resolved
	)
	{
		foreach (var candidate in definition.ContainingNamespace.GetTypeMembers(definition.Name))
		{
			if (candidate.IsGenericType)
			{
				if (candidate.Arity != 1 || !SatisfiesConstraints(candidate, propertyType))
					continue;

				var closed = candidate.Construct(propertyType);
				if (!ImplementsRuleFor(closed, propertyType))
					continue;

				resolved = closed;
				return true;
			}

			if (!ImplementsRuleFor(candidate, propertyType))
				continue;

			resolved = candidate;
			return true;
		}

		resolved = definition;
		return false;
	}

	static bool IsOpenGeneric(INamedTypeSymbol type) =>
		type.IsGenericType
		&& (
			type.IsUnboundGenericType
			|| type.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter)
		);

	static bool SatisfiesConstraints(INamedTypeSymbol definition, ITypeSymbol type)
	{
		foreach (var parameter in definition.TypeParameters)
		{
			if (parameter.HasValueTypeConstraint && !type.IsValueType)
				return false;

			if (parameter.HasReferenceTypeConstraint && !type.IsReferenceType)
				return false;

			if (parameter.HasUnmanagedTypeConstraint && !type.IsUnmanagedType)
				return false;

			foreach (var constraint in parameter.ConstraintTypes)
			{
				// Substitute the candidate type into the constraint so self-referential constraints (for
				// example where TSelf : IScalarValueObject<TSelf, string>) are checked rather than assumed.
				// Assuming them lets an unsatisfiable rule reach emission, and the consumer then fails to
				// compile the generated validator (CS0311) instead of receiving a diagnostic.
				var substituted = SubstituteConstraint(constraint, parameter, type);

				// A constraint that references a type parameter which cannot be replaced from the definition
				// alone is treated as satisfied rather than rejecting a usable rule.
				if (substituted is null)
					continue;

				if (IsOrImplements(type, substituted))
					continue;

				// A class-type constraint (for example `where T : struct, Enum`) is satisfied by a type the
				// interface check above does not cover: an enum implements no interface named Enum.
				if (SatisfiesBaseTypeConstraint(type, substituted))
					continue;

				// A constraint can be satisfied by a declaration this generator cannot see: a type declared
				// partial in this compilation may receive the interface implementation from another generator
				// (a scalar value object gets IScalarValueObject<TSelf, TValue> from the value object
				// generator, which does not run in this compilation's view). Defer those to the compiler
				// instead of dropping a rule that the consumer's compilation can satisfy.
				if (ContainsTypeParameter(constraint) && CanReceiveGeneratedMembers(type))
					continue;

				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Determines whether <paramref name="type"/> satisfies a base-class constraint the interface check does not
	/// cover. Only the constrained base types a rule can declare are recognised.
	/// </summary>
	/// <param name="type">The candidate type the rule is closed with.</param>
	/// <param name="constraint">The constraint type.</param>
	/// <returns><see langword="true"/> when the type satisfies the constraint.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static bool SatisfiesBaseTypeConstraint(ITypeSymbol type, ITypeSymbol constraint) =>
		constraint.SpecialType switch
		{
			SpecialType.System_Enum => type.TypeKind == TypeKind.Enum,
			SpecialType.System_ValueType => type.IsValueType,
			SpecialType.System_Delegate => type.TypeKind == TypeKind.Delegate,
			SpecialType.System_Object => true,
			_ => false,
		};

	/// <summary>
	/// Determines whether <paramref name="type"/> can be completed by another source generator, which means
	/// it can end up satisfying a constraint this compilation does not yet show.
	/// </summary>
	/// <param name="type">The candidate type to close the rule with.</param>
	/// <returns><see langword="true"/> when the type is declared <c>partial</c> in this compilation.</returns>
	/// <remarks>
	/// Only a type declared in the compilation being generated for can receive further declarations, and
	/// only a <c>partial</c> declaration can be extended. A primitive or a type from a referenced assembly
	/// can never gain the members a rule's constraint asks for, so it keeps the strict check.
	/// </remarks>
	static bool CanReceiveGeneratedMembers(ITypeSymbol type)
	{
		if (type is not INamedTypeSymbol named)
			return false;

		foreach (var reference in named.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax() is TypeDeclarationSyntax declaration
				&& declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Replaces every occurrence of <paramref name="parameter"/> inside <paramref name="constraint"/> with
	/// <paramref name="type"/>.
	/// </summary>
	/// <param name="constraint">The declared constraint type.</param>
	/// <param name="parameter">The rule type parameter being closed.</param>
	/// <param name="type">The candidate type used to close the rule.</param>
	/// <returns>
	/// The substituted constraint, or <see langword="null"/> when it references a type parameter that is not
	/// <paramref name="parameter"/> and therefore cannot be evaluated from the definition alone.
	/// </returns>
	static ITypeSymbol? SubstituteConstraint(ITypeSymbol constraint, ITypeParameterSymbol parameter, ITypeSymbol type)
	{
		if (constraint.TypeKind == TypeKind.TypeParameter)
			return SymbolEqualityComparer.Default.Equals(constraint, parameter) ? type : null;

		if (constraint is not INamedTypeSymbol named)
			return constraint;

		var arguments = named.TypeArguments;
		if (arguments.Length == 0)
			return constraint;

		// An unbound generic constraint (for example IEquatable<>) carries no argument to substitute.
		if (named.IsUnboundGenericType)
			return null;

		var substituted = new ITypeSymbol[arguments.Length];
		var changed = false;

		for (var index = 0; index < arguments.Length; index++)
		{
			var argument = arguments[index];
			if (!ContainsTypeParameter(argument))
			{
				substituted[index] = argument;
				continue;
			}

			var replacement = SubstituteConstraint(argument, parameter, type);
			if (replacement is null)
				return null;

			substituted[index] = replacement;
			changed = true;
		}

		// A constructed constraint must be re-created from its definition; constructing from an already
		// constructed type (for example IScalarValueObject<TSelf, string>) is rejected by Roslyn.
		return changed ? named.OriginalDefinition.Construct(substituted) : named;
	}

	/// <summary>
	/// Determines whether <paramref name="type"/> is, implements or inherits <paramref name="contract"/>,
	/// including the contract's generic arguments.
	/// </summary>
	/// <remarks>
	/// Unlike <c>TypeHelpers.IsOrImplements</c>, which matches on the metadata name alone, this compares the
	/// constructed contract so
	/// <c>IScalarValueObject&lt;T, string&gt;</c> is not satisfied by a different construction.
	/// </remarks>
	static bool IsOrImplements(ITypeSymbol type, ITypeSymbol contract)
	{
		if (contract.TypeKind == TypeKind.TypeParameter)
			return true;

		if (SymbolEqualityComparer.Default.Equals(type, contract))
			return true;

		for (var baseType = (type as INamedTypeSymbol)?.BaseType; baseType is not null; baseType = baseType.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(baseType, contract))
				return true;
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (SymbolEqualityComparer.Default.Equals(@interface, contract))
				return true;
		}

		return false;
	}

	static bool ContainsTypeParameter(ITypeSymbol type) =>
		type.TypeKind == TypeKind.TypeParameter
		|| (type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter));

	/// <summary>
	/// Determines whether <paramref name="ruleType"/> owns its error identity by implementing
	/// <c>ZodSharp.Core.IZodRule</c>.
	/// </summary>
	/// <param name="ruleType">The resolved rule type.</param>
	/// <returns><see langword="true"/> when the rule supplies <c>Code</c>/<c>Origin</c> itself.</returns>
	internal static bool IsZodRule(INamedTypeSymbol ruleType) =>
		TypeHelpers.IsOrImplements(ruleType, TypeLibrary.ZodSharp.Core.IZodRule);

	/// <summary>
	/// Finds an identity constructor parameter (<c>code</c> or <c>origin</c>) declared by
	/// <paramref name="ruleType"/>.
	/// </summary>
	/// <param name="ruleType">The rule type to inspect.</param>
	/// <param name="parameterName">The first identity parameter found.</param>
	/// <returns><see langword="true"/> when the rule declares an identity constructor parameter.</returns>
	/// <remarks>
	/// Such a parameter is only surfaced when the rule implements <c>IZodRule</c>; otherwise the generated
	/// validation supplies the value and never reads it back, which the generator reports as ZODSGEN039.
	/// </remarks>
	internal static bool TryGetIdentityParameter(INamedTypeSymbol ruleType, out string parameterName)
	{
		foreach (var parameter in MappedParameters(ruleType))
		{
			if (IsIdentityParameter(parameter))
			{
				parameterName = parameter.Name;
				return true;
			}
		}

		parameterName = string.Empty;
		return false;
	}

	/// <summary>
	/// Determines whether the parameter declares the rule's error identity (<c>code</c>/<c>origin</c>).
	/// </summary>
	/// <param name="parameter">The rule constructor parameter to test.</param>
	/// <returns><see langword="true"/> when the parameter is an identity parameter.</returns>
	internal static bool IsIdentityParameter(IParameterSymbol parameter) =>
		string.Equals(parameter.Name, "code", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(parameter.Name, "origin", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	/// Gets the named arguments an applied attribute supplies that the resolved rule never consumes: the
	/// argument matches no public constructor parameter and is not part of the reported error identity, so the
	/// value has no effect on validation.
	/// </summary>
	/// <param name="attribute">The applied attribute.</param>
	/// <param name="attributeClass">The attribute type.</param>
	/// <param name="ruleType">The rule the attribute resolves to.</param>
	/// <returns>The names of the arguments that have no effect, in declaration order.</returns>
	static ImmutableArray<string> GetUnusedNamedArguments(
		AttributeData attribute,
		INamedTypeSymbol attributeClass,
		INamedTypeSymbol ruleType
	)
	{
		if (attribute.NamedArguments.IsDefaultOrEmpty)
			return [];

		HashSet<string> consumed = [with(StringComparer.OrdinalIgnoreCase)];

		foreach (var parameter in MappedParameters(ruleType))
			consumed.Add(parameter.Name);

		// The inherited ValidationAttribute error-message members feed the emitted message directly.
		consumed.Add("ErrorMessage");
		consumed.Add("ErrorMessageResourceName");
		consumed.Add("ErrorMessageResourceType");

		// Identity properties are read from the applied attribute rather than passed to the rule.
		foreach (var identityProperty in GetIdentityPropertyNames(attributeClass))
			consumed.Add(identityProperty);

		ImmutableArray<string>.Builder? unused = null;
		foreach (var pair in attribute.NamedArguments)
		{
			if (consumed.Contains(pair.Key))
				continue;

			unused ??= ImmutableArray.CreateBuilder<string>();
			unused.Add(pair.Key);
		}

		return unused is null ? [] : unused.ToImmutable();
	}

	/// <summary>
	/// Gets the error-identity property names the generator reads from an applied attribute.
	/// </summary>
	/// <param name="attributeClass">The attribute type, when known.</param>
	/// <returns>The identity property names.</returns>
	/// <remarks>
	/// The contract is declared by <c>ZodSharp.Core.IZodRuleAttribute</c>: an attribute that implements it
	/// contributes its members, so a member added to the interface is treated as identity rather than as an
	/// unconsumed argument. Attributes that do not implement the interface are still read through the
	/// documented <c>Code</c>/<c>Origin</c> names so existing declarations keep working.
	/// </remarks>
	static ImmutableArray<string> GetIdentityPropertyNames(INamedTypeSymbol? attributeClass)
	{
		if (attributeClass is not null)
		{
			foreach (var @interface in attributeClass.AllInterfaces)
			{
				var definition = @interface.OriginalDefinition;
				if (
					definition.Name != IdentityAttributeInterfaceName
					|| definition.Arity != 0
					|| definition.ContainingNamespace.ToDisplayString() != TypeLibraryGenerator.ZodSharpCoreNamespace
				)
				{
					continue;
				}

				var names = @interface
					.GetMembers()
					.OfType<IPropertySymbol>()
					.Select(static property => property.Name)
					.ToImmutableArray();
				if (names.Length > 0)
					return names;
			}
		}

		return DocumentedIdentityPropertyNames;
	}

	/// <summary>
	/// Gets every public constructor parameter used to build the rule's arguments across all attribute-addressable
	/// constructors, matching the rule instantiation contract. Used to decide whether an applied named argument
	/// is consumed by any overload.
	/// </summary>
	/// <param name="ruleType">The resolved rule type.</param>
	/// <returns>The mapped constructor parameters, de-duplicated by name.</returns>
	static ImmutableArray<IParameterSymbol> MappedParameters(INamedTypeSymbol ruleType)
	{
		HashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];
		var parameters = ImmutableArray.CreateBuilder<IParameterSymbol>();

		foreach (var constructor in SelectAttributeConstructors(ruleType))
		{
			foreach (var parameter in constructor.Parameters)
			{
				if (seen.Add(parameter.Name))
					parameters.Add(parameter);
			}
		}

		return parameters.ToImmutable();
	}

	/// <summary>
	/// Selects the public constructors a rule attribute mirrors and the generated validation instantiates: every
	/// overload whose parameters are all representable as attribute properties (a <c>message</c> or
	/// <c>CancellationToken</c> parameter is consumed rather than mirrored), widest first. A rule that overloads
	/// its constructor for a non-attribute-argument type (for example <see cref="System.Text.RegularExpressions.Regex"/>)
	/// only surfaces the compatible overload; a rule whose overloads are all addressable (for example the
	/// versioned and versionless UUID rules) surfaces one attribute constructor per overload so each stays
	/// reachable.
	/// </summary>
	/// <param name="ruleType">The rule type to inspect.</param>
	/// <returns>The selected constructors, widest first, or an empty array when the rule declares none.</returns>
	internal static ImmutableArray<IMethodSymbol> SelectAttributeConstructors(INamedTypeSymbol ruleType)
	{
		var candidates = ruleType
			.InstanceConstructors.Where(static c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
			.ToArray();

		// A struct's implicit parameterless constructor would otherwise satisfy "every parameter is
		// attribute-mappable" vacuously and silently drop the rule's real constructor arguments. It is only a
		// candidate when the rule declares no constructor of its own (for example a parameterless rule).
		var considered = candidates.Any(static c => !c.IsImplicitlyDeclared)
			? [.. candidates.Where(static c => !c.IsImplicitlyDeclared)]
			: candidates;

		var mappable = considered
			.Where(static c => c.Parameters.All(IsAttributeMappableParameter))
			.OrderByDescending(static c => c.Parameters.Length)
			.ToImmutableArray();

		if (!mappable.IsEmpty)
			return mappable;

		// No overload is addressable; keep the widest so the caller can still report the offending parameter.
		return [.. considered.OrderByDescending(static c => c.Parameters.Length).Take(1)];
	}

	static bool IsAttributeMappableParameter(IParameterSymbol parameter) =>
		IsMessageParameter(parameter)
		|| IsCancellationToken(parameter.Type)
		|| IsSupportedAttributePropertyType(parameter.Type);

	internal static bool IsMessageParameter(IParameterSymbol parameter) =>
		string.Equals(parameter.Name, "message", StringComparison.OrdinalIgnoreCase);

	internal static bool IsCancellationToken(ITypeSymbol type) =>
		type.ToDisplayString() == "System.Threading.CancellationToken";

	internal static bool IsSupportedAttributePropertyType(ITypeSymbol type)
	{
		var unwrapped = TypeHelpers.UnwrapNullableType(type);

		// A type parameter is surfaced as a double on the generated attribute (see the attribute builder), so
		// it is addressable even though the parameter itself is not an attribute-argument type.
		if (unwrapped.TypeKind == TypeKind.TypeParameter)
			return true;

		// A one-dimensional array of a supported type is a legal attribute argument. An array of a type
		// parameter is surfaced as object[] by the attribute builder, so this also covers AllowedValuesRule<T>.
		if (unwrapped is IArrayTypeSymbol { Rank: 1 } array)
			return IsSupportedAttributePropertyType(array.ElementType);

		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum })
			return true;

		if (unwrapped.ToDisplayString() == "System.Type")
			return true;

		// Only primitive types, object and string are supported as attribute properties.
		return unwrapped.SpecialType
			is SpecialType.System_Boolean
				or SpecialType.System_Byte
				or SpecialType.System_SByte
				or SpecialType.System_Char
				or SpecialType.System_Int16
				or SpecialType.System_UInt16
				or SpecialType.System_Int32
				or SpecialType.System_UInt32
				or SpecialType.System_Int64
				or SpecialType.System_UInt64
				or SpecialType.System_Single
				or SpecialType.System_Double
				or SpecialType.System_Object
				or SpecialType.System_String;
	}

	/// <summary>
	/// The attribute contract that declares the error-identity properties, matched by name so the generator
	/// does not take a package reference on the public API.
	/// </summary>
	const string IdentityAttributeInterfaceName = "IZodRuleAttribute";

	/// <summary>
	/// The identity property names read from attributes that do not implement
	/// <c>ZodSharp.Core.IZodRuleAttribute</c>: the members that interface declares.
	/// </summary>
	static readonly ImmutableArray<string> DocumentedIdentityPropertyNames = ["Code", "Origin"];

	static string? GetAttributeString(AttributeData attribute, string name)
	{
		foreach (var pair in attribute.NamedArguments)
		{
			if (pair.Key == name)
				return pair.Value.Value as string;
		}

		return null;
	}

	/// <summary>
	/// Reads a rule's <c>public const string MessageFormat</c>, walking base types so a rule that inherits
	/// its error identity (for example from a shared abstract base) is still described.
	/// </summary>
	/// <param name="ruleType">The resolved rule type.</param>
	/// <returns>The constant's value, or <see langword="null"/> when no such constant exists.</returns>
	static string? GetRuleMessageFormat(INamedTypeSymbol ruleType)
	{
		for (var current = (INamedTypeSymbol?)ruleType; current is not null; current = current.BaseType)
		{
			foreach (var member in current.GetMembers("MessageFormat"))
			{
				if (member is IFieldSymbol { IsConst: true, ConstantValue: string value })
					return value;
			}
		}

		return null;
	}

	/// <summary>
	/// Replaces the first whole-word subject token (<c>Value</c>) in a rule's message format with
	/// <paramref name="subject"/>, so a type-level rule on a scalar names the scalar instead of the generic
	/// subject. A format that does not reference the subject token is returned unchanged.
	/// </summary>
	/// <param name="messageFormat">The rule's message format.</param>
	/// <param name="subject">The name to substitute for the subject token.</param>
	/// <returns>The message format with the subject token replaced.</returns>
	static string SubstituteSubject(string messageFormat, string subject)
	{
		const string token = "Value";

		for (var index = 0; index <= messageFormat.Length - token.Length; index++)
		{
			if (string.CompareOrdinal(messageFormat, index, token, 0, token.Length) != 0)
				continue;

			var afterIndex = index + token.Length;
			var before = index == 0 ? '\0' : messageFormat[index - 1];
			var after = afterIndex >= messageFormat.Length ? '\0' : messageFormat[afterIndex];

			// Only replace a whole word, so a format that mentions the subject inside another word is left alone.
			if (IsWordCharacter(before) || IsWordCharacter(after))
				continue;

			return messageFormat.Substring(0, index) + subject + messageFormat.Substring(afterIndex);
		}

		return messageFormat;
	}

	static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

	static bool TryGetRuleMapping(INamedTypeSymbol attributeClass, out RuleMapping mapping)
	{
		for (var current = (INamedTypeSymbol?)attributeClass; current is not null; current = current.BaseType)
		{
			foreach (var attribute in current.GetAttributes())
			{
				if (
					attribute.AttributeClass is null
					|| attribute.AttributeClass.ToDisplayString()
						!= $"{TypeLibraryGenerator.ZodSharpCoreNamespace}.ZodRuleAttribute"
				)
				{
					continue;
				}

				if (attribute.ConstructorArguments.Length == 0)
					continue;

				if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol ruleType)
					continue;

				string? code = null;
				string? origin = null;
				foreach (var pair in attribute.NamedArguments)
				{
					switch (pair.Key)
					{
						case "Code":
							code = pair.Value.Value as string;
							break;
						case "Origin":
							origin = pair.Value.Value as string;
							break;
						default:
							break;
					}
				}

				mapping = new RuleMapping(ruleType, code, origin);
				return true;
			}
		}

		mapping = default;
		return false;
	}

	static bool ImplementsRuleFor(INamedTypeSymbol ruleType, ITypeSymbol propertyType)
	{
		// A rule that is still open (for example NotEmptyRule<T>) cannot be closed from an attribute alone.
		if (IsOpenGeneric(ruleType))
			return false;

		foreach (var iface in ruleType.AllInterfaces)
		{
			if (iface.TypeArguments.Length != 1)
				continue;

			var definition = iface.OriginalDefinition;
			if (definition.Name != "IValidationRule" || definition.Arity != 1)
				continue;

			if (definition.ContainingNamespace.ToDisplayString() != TypeLibraryGenerator.ZodSharpCoreNamespace)
				continue;

			if (TypeHelpers.IsSameType(iface.TypeArguments[0], propertyType))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Builds the rule constructor arguments for an applied attribute, choosing the rule overload the attribute
	/// addresses. A rule that overloads its constructor (for example the versioned and versionless UUID rules)
	/// exposes one attribute constructor per overload; the applied attribute's constructor selects the matching
	/// rule overload, so a bare <c>[Uuid]</c> does not try to satisfy the versioned overload's required version.
	/// </summary>
	/// <param name="attribute">The applied attribute.</param>
	/// <param name="ruleType">The resolved rule type.</param>
	/// <param name="subjectMessage">
	/// The subject-substituted default message for a type-level rule on a scalar, or <see langword="null"/>.
	/// It is used only when the attribute supplies no message of its own.
	/// </param>
	/// <param name="arguments">The argument expressions, in the selected constructor's parameter order.</param>
	/// <param name="unmappedParameterName">
	/// The parameter that could not be mapped when no overload could be satisfied.
	/// </param>
	/// <returns><see langword="true"/> when an overload was satisfied.</returns>
	static bool TryBuildArguments(
		AttributeData attribute,
		INamedTypeSymbol ruleType,
		string? subjectMessage,
		out EquatableArray<string> arguments,
		out string? unmappedParameterName
	)
	{
		arguments = new([]);
		unmappedParameterName = null;

		var candidates = SelectAttributeConstructors(ruleType);
		if (candidates.IsDefaultOrEmpty)
			return false;

		// Prefer an overload that consumes every value the attribute supplies, so a named/positional value that
		// only the wider overload declares (for example `Version`) is not silently dropped by a narrower one.
		foreach (var candidate in candidates)
		{
			if (!ConsumesSuppliedArguments(attribute, candidate))
				continue;

			if (TryBuildArgumentsFor(attribute, candidate, subjectMessage, out arguments, out _))
				return true;
		}

		// Fall back to any overload that can be built: hand-authored attributes map positionally and may name
		// their parameters differently from the rule's.
		foreach (var candidate in candidates)
		{
			if (TryBuildArgumentsFor(attribute, candidate, subjectMessage, out arguments, out _))
				return true;
		}

		// Report the failure against the widest overload for a useful message.
		TryBuildArgumentsFor(attribute, candidates[0], subjectMessage, out _, out unmappedParameterName);
		return false;
	}

	/// <summary>
	/// Determines whether <paramref name="candidate"/> has a parameter for every value the applied attribute
	/// supplies: each named argument is either a constructor parameter or part of the error identity, and each
	/// positional argument maps by the applied constructor's parameter name or by position.
	/// </summary>
	static bool ConsumesSuppliedArguments(AttributeData attribute, IMethodSymbol candidate)
	{
		HashSet<string> parameterNames = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var parameter in candidate.Parameters)
			parameterNames.Add(parameter.Name);

		foreach (var pair in attribute.NamedArguments)
		{
			if (IsIdentityOrValidationName(pair.Key))
				continue;

			if (!parameterNames.Contains(pair.Key))
				return false;
		}

		var positional = attribute.ConstructorArguments;
		if (positional.IsDefaultOrEmpty)
			return true;

		var appliedParameters = attribute.AttributeConstructor is { } appliedConstructor
			? appliedConstructor.Parameters
			: default;

		for (var i = 0; i < positional.Length; i++)
		{
			var appliedName =
				!appliedParameters.IsDefaultOrEmpty && i < appliedParameters.Length ? appliedParameters[i].Name : null;

			if (appliedName is not null && parameterNames.Contains(appliedName))
				continue;

			if (i < candidate.Parameters.Length)
				continue;

			return false;
		}

		return true;
	}

	/// <summary>
	/// Determines whether <paramref name="name"/> is consumed by every rule overload: the error identity
	/// (<c>Code</c>/<c>Origin</c>) and the inherited <c>ValidationAttribute</c> message members are read from the
	/// applied attribute rather than passed to the rule, so no overload needs a matching parameter.
	/// </summary>
	static bool IsIdentityOrValidationName(string name) =>
		name.Equals("Code", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("Origin", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("ErrorMessage", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("ErrorMessageResourceName", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("ErrorMessageResourceType", StringComparison.OrdinalIgnoreCase);

	static bool TryBuildArgumentsFor(
		AttributeData attribute,
		IMethodSymbol constructor,
		string? subjectMessage,
		out EquatableArray<string> arguments,
		out string? unmappedParameterName
	)
	{
		arguments = new([]);
		unmappedParameterName = null;

		var validation = ValidationAttributeData.FromAttributeData(attribute);
		var positional = attribute.ConstructorArguments;
		Dictionary<string, TypedConstant> named = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var pair in attribute.NamedArguments)
			named[pair.Key] = pair.Value;

		// The applied attribute's positional arguments are matched by its own constructor parameter names
		// first, so a generated constructor that omits the rule's message/identity parameters keeps the rule
		// parameters aligned. The raw index remains the fallback for hand-authored attributes whose parameter
		// names differ from the rule's.
		Dictionary<string, TypedConstant> positionalByName = [with(StringComparer.OrdinalIgnoreCase)];
		if (attribute.AttributeConstructor is { } appliedConstructor)
		{
			for (var i = 0; i < appliedConstructor.Parameters.Length && i < positional.Length; i++)
			{
				var appliedName = appliedConstructor.Parameters[i].Name;
				if (!positionalByName.ContainsKey(appliedName))
					positionalByName.Add(appliedName, positional[i]);
			}
		}

		var expressions = ImmutableArray.CreateBuilder<string>(constructor.Parameters.Length);

		for (var i = 0; i < constructor.Parameters.Length; i++)
		{
			var parameter = constructor.Parameters[i];

			if (named.TryGetValue(parameter.Name, out var namedValue))
			{
				if (!TryConvertConstant(namedValue, parameter.Type, out var namedExpression))
				{
					unmappedParameterName = parameter.Name;
					return false;
				}

				expressions.Add(namedExpression);
				continue;
			}

			if (positionalByName.TryGetValue(parameter.Name, out var positionalValue))
			{
				if (!TryConvertConstant(positionalValue, parameter.Type, out var namedPositionalExpression))
				{
					unmappedParameterName = parameter.Name;
					return false;
				}

				expressions.Add(namedPositionalExpression);
				continue;
			}

			if (i < positional.Length)
			{
				if (!TryConvertConstant(positional[i], parameter.Type, out var positionalExpression))
				{
					unmappedParameterName = parameter.Name;
					return false;
				}

				expressions.Add(positionalExpression);
				continue;
			}

			if (IsMessageParameter(parameter) && validation.Exists && !string.IsNullOrEmpty(validation.ErrorMessage))
			{
				expressions.Add(validation.ErrorMessage.StringLiteral());
				continue;
			}

			// A type-level rule on a scalar reports an empty path, so its default message names the scalar
			// instead of the generic subject. An explicit message (named/positional/ErrorMessage) has already
			// been consumed above, so this only replaces the rule's own default.
			if (subjectMessage is not null && IsMessageParameter(parameter))
			{
				expressions.Add(subjectMessage.StringLiteral());
				continue;
			}

			if (parameter.HasExplicitDefaultValue)
			{
				if (!TryConvertValue(parameter.ExplicitDefaultValue, parameter.Type, out var defaultExpression))
				{
					unmappedParameterName = parameter.Name;
					return false;
				}

				expressions.Add(parameter.ExplicitDefaultValue is null ? "null!" : defaultExpression);
				continue;
			}

			if (CanPassNull(parameter.Type))
			{
				expressions.Add("null");
				continue;
			}

			unmappedParameterName = parameter.Name;
			return false;
		}

		arguments = new(expressions.ToImmutable());
		return true;
	}

	/// <summary>
	/// Determines whether a bare <see langword="null"/> literal may be emitted for a value of
	/// <paramref name="type"/> without producing CS8625. A non-nullable reference type is excluded even though
	/// it can hold <see langword="null"/> at runtime, so such a value is reported as unmappable instead.
	/// </summary>
	static bool CanPassNull(ITypeSymbol type) =>
		type.NullableAnnotation == NullableAnnotation.Annotated
		|| type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
		|| (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.None);

	internal static bool TryConvertConstant(TypedConstant constant, ITypeSymbol targetType, out string expression)
	{
		if (constant.IsNull)
		{
			expression = "null";
			return CanPassNull(targetType);
		}

		if (constant.Kind == TypedConstantKind.Array)
			return TryConvertArrayConstant(constant, targetType, out expression);

		if (constant.Kind == TypedConstantKind.Type)
		{
			if (constant.Value is ITypeSymbol typeSymbol)
			{
				expression = $"typeof({new TypeIdentity(typeSymbol).RenderFullName})";
				return true;
			}

			expression = string.Empty;
			return false;
		}

		return TryConvertValue(constant.Value, targetType, out expression);
	}

	/// <summary>
	/// Converts an array attribute argument (for example the allowed/denied values of
	/// <c>AllowedValuesRule&lt;T&gt;</c>) into a typed array expression, converting each element to the target
	/// array's element type so an <c>object[]</c> attribute argument can be closed with the member type.
	/// </summary>
	/// <param name="constant">The array constant supplied by the applied attribute.</param>
	/// <param name="targetType">The rule constructor parameter type the array is passed to.</param>
	/// <param name="expression">The generated array expression when the conversion succeeds.</param>
	/// <returns><see langword="true"/> when every element converts to the target element type.</returns>
	static bool TryConvertArrayConstant(TypedConstant constant, ITypeSymbol targetType, out string expression)
	{
		expression = string.Empty;

		if (TypeHelpers.UnwrapNullableType(targetType) is not IArrayTypeSymbol { Rank: 1 } arrayType)
			return false;

		if (!TypeReference.TryCreate(arrayType.ElementType, out var elementReference))
			return false;

		var elements = constant.Values;
		if (elements.IsDefaultOrEmpty)
		{
			expression = $"new {elementReference.RenderFullName}[0]";
			return true;
		}

		var converted = ImmutableArray.CreateBuilder<string>(elements.Length);
		foreach (var element in elements)
		{
			if (!TryConvertConstant(element, arrayType.ElementType, out var elementExpression))
				return false;

			converted.Add(elementExpression);
		}

		expression = $"new {elementReference.RenderFullName}[] {{ {string.Join(", ", converted)} }}";
		return true;
	}

	internal static bool TryConvertValue(object? value, ITypeSymbol targetType, out string expression)
	{
		if (value is null)
		{
			expression = "null";
			return CanPassNull(targetType);
		}

		var unwrapped = TypeHelpers.UnwrapNullableType(targetType);

		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
		{
			expression =
				$"({new TypeIdentity(enumType).RenderFullName}){Convert.ToString(value, CultureInfo.InvariantCulture)}";
			return true;
		}

		var specialType = unwrapped.SpecialType;

#pragma warning disable IDE0072 // Add missing cases
		expression = specialType switch
		{
			SpecialType.System_String when value is string text => text.StringLiteral(),
			SpecialType.System_Char when value is char character => CodeGenHelpers.QuoteChar(character),
			SpecialType.System_Boolean when value is bool boolean => boolean ? "true" : "false",
			SpecialType.System_Object when value is string text => text.StringLiteral(),
			_ => string.Empty,
		};
#pragma warning restore IDE0072 // Add missing cases

		if (expression.Length > 0)
			return true;

		if (TryFormatNumericLiteral(value, specialType, out expression))
			return true;

		// A value whose runtime type differs from the target is converted numerically. This is what lets the
		// double bound of a generated bound-rule attribute (for example `[MinValue(MinValue = 3)]`) be closed
		// with an `int`, `long`, or `decimal` member.
		return TryConvertNumeric(value, specialType, out expression);
	}

	/// <summary>
	/// Formats a numeric value that already matches <paramref name="specialType"/> as a target-typed literal.
	/// </summary>
	static bool TryFormatNumericLiteral(object value, SpecialType specialType, out string expression)
	{
#pragma warning disable IDE0072 // Add missing cases
		expression = specialType switch
		{
			SpecialType.System_Byte when value is byte number => number.ToString(CultureInfo.InvariantCulture),
			SpecialType.System_SByte when value is sbyte number =>
				$"(sbyte){number.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int16 when value is short number =>
				$"(short){number.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_UInt16 when value is ushort number =>
				$"(ushort){number.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int32 when value is int number => number.ToString(CultureInfo.InvariantCulture),
			SpecialType.System_UInt32 when value is uint number => $"{number.ToString(CultureInfo.InvariantCulture)}U",
			SpecialType.System_Int64 when value is long number => $"{number.ToString(CultureInfo.InvariantCulture)}L",
			SpecialType.System_UInt64 when value is ulong number =>
				$"{number.ToString(CultureInfo.InvariantCulture)}UL",
			SpecialType.System_Single when value is float number =>
				$"{number.ToString("R", CultureInfo.InvariantCulture)}F",
			SpecialType.System_Double when value is double number =>
				$"{number.ToString("R", CultureInfo.InvariantCulture)}D",
			SpecialType.System_Decimal when value is decimal number =>
				$"{number.ToString(CultureInfo.InvariantCulture)}M",
			_ => string.Empty,
		};
#pragma warning restore IDE0072 // Add missing cases

		return expression.Length > 0;
	}

	/// <summary>
	/// Converts a numeric value to a different numeric target type and formats the result as a literal.
	/// </summary>
	static bool TryConvertNumeric(object value, SpecialType targetSpecialType, out string expression)
	{
		expression = string.Empty;

		var targetClrType = GetNumericClrType(targetSpecialType);
		if (targetClrType is null || value is string || value is bool || value is char)
			return false;

		var valueClrType = value.GetType();
		if (GetNumericClrType(valueClrType) is null || valueClrType == targetClrType)
			return false;

		try
		{
			var converted = Convert.ChangeType(value, targetClrType, CultureInfo.InvariantCulture);
			return converted is not null && TryFormatNumericLiteral(converted, targetSpecialType, out expression);
		}
		catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
		{
			return false;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static Type? GetNumericClrType(SpecialType specialType) =>
		specialType switch
		{
			SpecialType.System_Byte => typeof(byte),
			SpecialType.System_SByte => typeof(sbyte),
			SpecialType.System_Int16 => typeof(short),
			SpecialType.System_UInt16 => typeof(ushort),
			SpecialType.System_Int32 => typeof(int),
			SpecialType.System_UInt32 => typeof(uint),
			SpecialType.System_Int64 => typeof(long),
			SpecialType.System_UInt64 => typeof(ulong),
			SpecialType.System_Single => typeof(float),
			SpecialType.System_Double => typeof(double),
			SpecialType.System_Decimal => typeof(decimal),
			_ => null,
		};

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static Type? GetNumericClrType(Type type) =>
		Type.GetTypeCode(type) switch
		{
			TypeCode.Byte => typeof(byte),
			TypeCode.SByte => typeof(sbyte),
			TypeCode.Int16 => typeof(short),
			TypeCode.UInt16 => typeof(ushort),
			TypeCode.Int32 => typeof(int),
			TypeCode.UInt32 => typeof(uint),
			TypeCode.Int64 => typeof(long),
			TypeCode.UInt64 => typeof(ulong),
			TypeCode.Single => typeof(float),
			TypeCode.Double => typeof(double),
			TypeCode.Decimal => typeof(decimal),
			_ => null,
		};

	static Location GetAttributeLocation(AttributeData attributeData) =>
		attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

	readonly record struct RuleMapping(INamedTypeSymbol RuleType, string? Code, string? Origin);
}

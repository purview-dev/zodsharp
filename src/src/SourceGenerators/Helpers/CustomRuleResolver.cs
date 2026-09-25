using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
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
	public static EquatableArray<CustomRuleDescriptor> Resolve(
		ISymbol symbol,
		ITypeSymbol ruleTargetType,
		ImmutableArray<ReportableDiagnostic>.Builder diagnostics
	)
	{
		ImmutableArray<CustomRuleDescriptor>.Builder? builder = null;

		foreach (var attribute in symbol.GetAttributes())
		{
			if (attribute.AttributeClass is not INamedTypeSymbol attributeClass)
				continue;

			// The built-in DataAnnotations attributes have dedicated emitters.
			if (IsBuiltInDataAnnotation(attributeClass))
				continue;

			if (!TryGetRuleMapping(attributeClass, out var mapping))
				continue;

			if (!TryResolveRuleType(mapping.RuleType, ruleTargetType, out var ruleType))
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

			if (!ImplementsRuleFor(ruleType, ruleTargetType))
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

			if (!TryBuildArguments(attribute, ruleType, out var arguments, out var unmappedParameterName))
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

			builder ??= ImmutableArray.CreateBuilder<CustomRuleDescriptor>();
			builder.Add(
				new CustomRuleDescriptor(
					new TypeIdentity(ruleType),
					GetAttributeString(attribute, "Code") ?? mapping.Code,
					GetAttributeString(attribute, "Origin") ?? mapping.Origin,
					ValidationAttributeData.FromAttributeData(attribute),
					arguments,
					IsZodRule(ruleType)
				)
			);
		}

		return builder is null ? new(ImmutableArray<CustomRuleDescriptor>.Empty) : new(builder.ToImmutable());
	}

	static bool IsBuiltInDataAnnotation(INamedTypeSymbol attributeClass) =>
		attributeClass.ContainingNamespace.ToDisplayString() == TypeLibraryGenerator.SystemDataAnnotations;

	/// <summary>
	/// Determines whether <paramref name="attributeClass"/> is mapped to a validation rule through
	/// <c>[ZodRule(typeof(...))]</c>. Used by the analyzer to flag rule attributes that can never run.
	/// </summary>
	/// <param name="attributeClass">The attribute type to test.</param>
	/// <returns><see langword="true"/> when the attribute is mapped to a rule.</returns>
	internal static bool IsRuleMapped(INamedTypeSymbol attributeClass) => TryGetRuleMapping(attributeClass, out _);

	/// <summary>
	/// Resolves the rule type to instantiate: a plain type is used as-is, an unbound generic
	/// (<c>[ZodRule(typeof(NotEmptyRule&lt;&gt;))]</c>) is closed with the property type.
	/// </summary>
	/// <param name="ruleType">The rule type declared by the mapping.</param>
	/// <param name="propertyType">The property type used to close an unbound generic rule.</param>
	/// <param name="resolved">The rule type to instantiate.</param>
	/// <returns><see langword="true"/> when a usable rule type was resolved.</returns>
	static bool TryResolveRuleType(INamedTypeSymbol ruleType, ITypeSymbol propertyType, out INamedTypeSymbol resolved)
	{
		resolved = ruleType;

		if (!IsOpenGeneric(ruleType))
			return true;

		var definition = ruleType.OriginalDefinition;
		if (definition is null || definition.Arity != 1)
			return false;

		if (!SatisfiesConstraints(definition, propertyType))
			return false;

		resolved = definition.Construct(propertyType);
		return true;
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
				// Self-referential constraints (for example where T : IEquatable<T>) cannot be evaluated
				// from the definition alone; a closed type satisfies them in practice.
				if (ContainsTypeParameter(constraint))
					continue;

				if (!TypeHelpers.IsOrImplements(type, new TypeIdentity(constraint)))
					return false;
			}
		}

		return true;
	}

	static bool ContainsTypeParameter(ITypeSymbol type) =>
		type.TypeKind == TypeKind.TypeParameter
		|| (type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter));

	static bool IsZodRule(INamedTypeSymbol ruleType) =>
		TypeHelpers.IsOrImplements(ruleType, TypeLibrary.ZodSharp.Core.IZodRule);

	static string? GetAttributeString(AttributeData attribute, string name)
	{
		foreach (var pair in attribute.NamedArguments)
		{
			if (pair.Key == name)
				return pair.Value.Value as string;
		}

		return null;
	}

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

	static bool TryBuildArguments(
		AttributeData attribute,
		INamedTypeSymbol ruleType,
		out EquatableArray<string> arguments,
		out string? unmappedParameterName
	)
	{
		arguments = new(ImmutableArray<string>.Empty);
		unmappedParameterName = null;

		var constructor = ruleType
			.InstanceConstructors.Where(static c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
			.OrderByDescending(static c => c.Parameters.Length)
			.FirstOrDefault();

		if (constructor is null)
			return false;

		var validation = ValidationAttributeData.FromAttributeData(attribute);
		var positional = attribute.ConstructorArguments;
		Dictionary<string, TypedConstant> named = new(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in attribute.NamedArguments)
			named[pair.Key] = pair.Value;

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

			if (parameter.HasExplicitDefaultValue)
			{
				if (!TryConvertValue(parameter.ExplicitDefaultValue, parameter.Type, out var defaultExpression))
				{
					unmappedParameterName = parameter.Name;
					return false;
				}

				expressions.Add(defaultExpression);
				continue;
			}

			if (TypeHelpers.CanBeNull(parameter.Type))
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

	static bool IsMessageParameter(IParameterSymbol parameter) =>
		string.Equals(parameter.Name, "message", StringComparison.OrdinalIgnoreCase);

	static bool TryConvertConstant(TypedConstant constant, ITypeSymbol targetType, out string expression)
	{
		if (constant.IsNull)
		{
			expression = "null";
			return targetType.IsReferenceType || TypeHelpers.CanBeNull(targetType);
		}

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

	internal static bool TryConvertValue(object? value, ITypeSymbol targetType, out string expression)
	{
		if (value is null)
		{
			expression = "null";
			return targetType.IsReferenceType || TypeHelpers.CanBeNull(targetType);
		}

		var unwrapped = TypeHelpers.UnwrapNullableType(targetType);

		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
		{
			expression =
				$"({new TypeIdentity(enumType).RenderFullName}){Convert.ToString(value, CultureInfo.InvariantCulture)}";
			return true;
		}

#pragma warning disable IDE0072 // Add missing cases
		expression = unwrapped.SpecialType switch
		{
			SpecialType.System_String when value is string text => text.StringLiteral(),
			SpecialType.System_Char when value is char character => CodeGenHelpers.QuoteChar(character),
			SpecialType.System_Boolean when value is bool boolean => boolean ? "true" : "false",
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
			SpecialType.System_Object when value is string text => text.StringLiteral(),
			_ => string.Empty,
		};
#pragma warning restore IDE0072 // Add missing cases

		return expression.Length > 0;
	}

	static Location GetAttributeLocation(AttributeData attributeData) =>
		attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

	readonly record struct RuleMapping(INamedTypeSymbol RuleType, string? Code, string? Origin);
}

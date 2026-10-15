using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Resolves how an enum is serialized (numbers or strings) and, for strings, the JSON name of each member.
/// The generator uses this to make the exported JSON Schema match the System.Text.Json wire form instead of
/// always assuming the numeric default.
/// </summary>
static class EnumJsonResolver
{
	/// <summary>
	/// Determines whether a property's enum is serialized as a string, from a per-type or per-property
	/// <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c> (or its generic form). The assembly-level
	/// convention is applied separately, because a global converter is not visible to the generator.
	/// </summary>
	/// <param name="enumType">The enum type.</param>
	/// <param name="property">The property that references it.</param>
	/// <returns><see langword="true"/> when the enum is serialized as a string.</returns>
	public static bool IsSerializedAsString(INamedTypeSymbol enumType, IPropertySymbol property) =>
		HasStringEnumConverter(enumType) || HasStringEnumConverter(property);

	static bool HasStringEnumConverter(ISymbol symbol)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (
				attribute.AttributeClass is not { Name: "JsonConverterAttribute" } attributeClass
				|| attributeClass.ContainingNamespace.ToDisplayString() != "System.Text.Json.Serialization"
				|| attribute.ConstructorArguments.Length != 1
				|| attribute.ConstructorArguments[0].Value is not ITypeSymbol converterType
			)
			{
				continue;
			}

			// Both JsonStringEnumConverter and JsonStringEnumConverter<TEnum> share this name (the generic
			// form carries an arity but the same metadata name).
			if (
				converterType.Name == "JsonStringEnumConverter"
				&& converterType.ContainingNamespace.ToDisplayString() == "System.Text.Json.Serialization"
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the JSON names of a non-flags enum's allowed members, in declaration order. A member marked
	/// <c>[ZodIgnore]</c> or listed by the property's <c>[DeniedValues]</c> is excluded; the name is the
	/// member's <c>[JsonStringEnumMemberName]</c> value when declared, otherwise the member name.
	/// </summary>
	/// <param name="enumType">The enum type.</param>
	/// <param name="validationAttributes">The property's DataAnnotations.</param>
	/// <returns>The allowed members' JSON names.</returns>
	public static EquatableArray<string> GetJsonNames(
		INamedTypeSymbol enumType,
		ImmutableArray<AttributeData> validationAttributes
	)
	{
		var denied = DeniedValuesAttributeData.FromAttributeData(validationAttributes);
		var builder = ImmutableArray.CreateBuilder<string>();

		foreach (var member in enumType.GetMembers())
		{
			if (member is not IFieldSymbol { HasConstantValue: true } field)
				continue;

			if (
				field
					.GetAttributes()
					.Any(attribute => TypeLibrary.ZodSharp.ZodIgnoreAttribute.Equals(attribute.AttributeClass))
			)
			{
				continue;
			}

			if (denied.Exists && IsDenied(field, denied.Values))
				continue;

			builder.Add(GetJsonStringEnumMemberName(field) ?? field.Name);
		}

		return new(builder.ToImmutable());
	}

	static bool IsDenied(IFieldSymbol field, EquatableArray<TypedConstant> deniedValues)
	{
		for (var i = 0; i < deniedValues.Count; i++)
		{
			var value = deniedValues[i];
			if (!value.IsNull && Equals(value.Value, field.ConstantValue))
				return true;
		}

		return false;
	}

	static string? GetJsonStringEnumMemberName(IFieldSymbol field)
	{
		foreach (var attribute in field.GetAttributes())
		{
			if (
				attribute.AttributeClass is not { Name: "JsonStringEnumMemberNameAttribute" } attributeClass
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
	/// Applies the assembly-level enum naming policy to a member name, matching
	/// <c>System.Text.Json</c>'s <c>JsonNamingPolicy.CamelCase</c>.
	/// </summary>
	/// <param name="name">The member name.</param>
	/// <param name="policy">The naming policy.</param>
	/// <returns>The transformed name.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	public static string ApplyNamingPolicy(string name, EnumJsonNamingPolicy policy) =>
		policy switch
		{
			EnumJsonNamingPolicy.CamelCase => ToCamelCase(name),
			_ => name,
		};

	/// <summary>
	/// Mirrors <c>System.Text.Json.JsonNamingPolicy.CamelCase</c>: the leading run of uppercase characters is
	/// lowercased, except the last one when it is followed by a lowercase character (so <c>IOStream</c>
	/// becomes <c>ioStream</c>).
	/// </summary>
	static string ToCamelCase(string name)
	{
		if (name.Length == 0 || !char.IsUpper(name[0]))
			return name;

		var chars = name.ToCharArray();
		for (var i = 0; i < chars.Length; i++)
		{
			if (i == 1 && !char.IsUpper(chars[i]))
				break;

			var hasNext = i + 1 < chars.Length;
			if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
			{
				if (chars[i + 1] == ' ')
					chars[i] = char.ToLowerInvariant(chars[i]);
				break;
			}

			chars[i] = char.ToLowerInvariant(chars[i]);
		}

		return new string(chars);
	}
}

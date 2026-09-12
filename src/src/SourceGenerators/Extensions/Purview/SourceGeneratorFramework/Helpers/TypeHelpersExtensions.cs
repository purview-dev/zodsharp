using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;

namespace Purview.SourceGeneratorFramework.Helpers;

[EditorBrowsable(EditorBrowsableState.Never)]
static class TypeHelpersExtensions
{
	extension(TypeHelpers)
	{
		public static LengthAccessKind GetLengthAccessKind(ITypeSymbol propertyType) =>
			propertyType.SpecialType == SpecialType.System_String || propertyType is IArrayTypeSymbol
				? LengthAccessKind.Length
			: HasAccessibleCountProperty(propertyType) ? LengthAccessKind.Count
			: LengthAccessKind.Enumerable;

		public static bool HasAccessibleCountProperty(ITypeSymbol propertyType) =>
			propertyType
				.GetMembers("Count")
				.OfType<IPropertySymbol>()
				.Any(static p =>
					!p.IsStatic
					&& p.DeclaredAccessibility == Accessibility.Public
					&& p.Parameters.Length == 0
					&& p.Type.SpecialType == SpecialType.System_Int32
				);

		[System.Diagnostics.CodeAnalysis.SuppressMessage(
			"Style",
			"IDE0072:Add missing cases",
			Justification = "Internal is the valid default"
		)]
		public static string GetLimitedAccessibilityKeyword(INamedTypeSymbol symbol)
		{
			return symbol.DeclaredAccessibility switch
			{
				Accessibility.Public => "public",
				_ => "internal",
			};
		}

		public static string GetFullSchemaTypeName(INamedTypeSymbol typeSymbol)
		{
			StringBuilder sb = new();
			sb.Append("global::");

			var namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();
			if (!string.IsNullOrEmpty(namespaceName))
			{
				sb.Append(namespaceName);
				sb.Append('.');
			}

			List<INamedTypeSymbol> containingTypes = [];
			var current = typeSymbol.ContainingType;
			while (current is not null)
			{
				containingTypes.Add(current);
				current = current.ContainingType;
			}

			containingTypes.Reverse();

			foreach (var containingType in containingTypes)
			{
				sb.Append(containingType.Name);
				sb.Append('.');
			}

			sb.Append(typeSymbol.Name);
			sb.Append("Schema");
			return sb.ToString();
		}

		public static bool HasZodSchemaAttribute(ITypeSymbol typeSymbol)
		{
			var unwrapped = UnwrapNullableType(typeSymbol);
			if (unwrapped is not INamedTypeSymbol namedType)
				return false;

			// Check if the type has the ZodSchema attribute
			return namedType.GetAttributes().Any(a => TypeLibrary.ZodSharp.ZodSchemaAttribute.Equals(a.AttributeClass));
		}

		public static bool HasDataAnnotationAttribute(IPropertySymbol propertySymbol)
		{
			// Check if the property has the DataAnnotation attribute
			return propertySymbol
				.GetAttributes()
				.Any(a =>
					a.AttributeClass is not null
					&& TypeHelpers.InheritsFrom(
						a.AttributeClass,
						TypeLibrary.System.ComponentModel.DataAnnotations.ValidationAttribute
					)
				);
		}

#pragma warning disable format
		public static bool CanBeNull(ITypeSymbol typeSymbol) =>
			typeSymbol.IsReferenceType
			|| typeSymbol.NullableAnnotation == NullableAnnotation.Annotated
			|| typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
#pragma warning restore format

		public static bool IsNumericType(ITypeSymbol type) =>
			type.SpecialType
				is SpecialType.System_Byte
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

#pragma warning disable format
		public static ITypeSymbol UnwrapNullableType(ITypeSymbol type) =>
			type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType
				? nullableType.TypeArguments[0]
				: type;

		public static ITypeSymbol StripNullableAnnotations(ITypeSymbol type) =>
			UnwrapNullableType(type).WithNullableAnnotation(NullableAnnotation.None);
#pragma warning restore format

		public static bool IsSameType(ITypeSymbol left, ITypeSymbol? right, SymbolEqualityComparer? comparer = null)
		{
			if (right is null)
				return false;

			comparer ??= SymbolEqualityComparer.Default;
			return comparer.Equals(UnwrapNullableType(left), UnwrapNullableType(right));
		}

		public static bool IsNamedType(ITypeSymbol type, string fullyQualifiedMetadataName)
		{
			type = UnwrapNullableType(type);
			return type is INamedTypeSymbol namedType
				&& string.Equals(namedType.ToDisplayString(), fullyQualifiedMetadataName, StringComparison.Ordinal);
		}

		public static bool HasAttribute(IEnumerable<AttributeData> attributes, TypeIdentity attribute) =>
			attributes.Any(attribute.Equals);

		public static bool IsOrImplements(ITypeSymbol type, TypeIdentity interfaceType)
		{
			var unwrapped = StripNullableAnnotations(type);
			return TypeHelpers.IsNamedType(unwrapped, interfaceType.MetadataFullName)
				|| TypeHelpers.Implements(unwrapped, interfaceType);
		}
	}
}

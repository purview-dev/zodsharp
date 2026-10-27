using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateValueSetValidations(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		bool emitEnumRule = false
	)
	{
		// The enum rule owns the value set: the values a [DeniedValues] attribute lists are absorbed into the
		// rule's disallowed set, so the standalone validation is skipped to avoid reporting the value twice.
		if (emitEnumRule)
			return;

		var allowedValues = property.ValidationAttributes.AllowedValues;
		var deniedValues = property.ValidationAttributes.DeniedValues;
		if (
			(!allowedValues.ShouldProcess || !allowedValues.Value.Exists)
			&& (!deniedValues.ShouldProcess || !deniedValues.Value.Exists)
		)
		{
			return;
		}

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		writer.Assignment("var", propertyValueName, $"value.{propertyName}");

		GenerateAllowedValuesValidation(writer, property);
		GenerateDeniedValuesValidation(writer, property);
	}

	static void GenerateAllowedValuesValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var allowedValues = property.ValidationAttributes.AllowedValues;
		if (!allowedValues.ShouldProcess || !allowedValues.Value.Exists)
			return;

		if (
			!TryBuildValueSetComparison(
				writer,
				property,
				allowedValues.Value.Values,
				out var comparisonExpression,
				out var displayValues
			)
		)
		{
			return;
		}

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var message = BuildMessageTemplate(
			allowedValues.Value.ValidationAttribute,
			"Field '{displayName}' must be one of the following values: {options}.",
			"options"
		);

		writer.IfBlock(
			$"!({comparisonExpression.Replace("propertyValue", propertyValueName)})",
			ifBody => WriteAddError(ifBody, ValueSetError(property, message, displayValues))
		);

		writer.NewLine();
	}

	static void GenerateDeniedValuesValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var deniedValues = property.ValidationAttributes.DeniedValues;
		if (!deniedValues.ShouldProcess || !deniedValues.Value.Exists)
			return;

		if (
			!TryBuildValueSetComparison(
				writer,
				property,
				deniedValues.Value.Values,
				out var comparisonExpression,
				out var displayValues
			)
		)
		{
			return;
		}

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var message = BuildMessageTemplate(
			deniedValues.Value.ValidationAttribute,
			"Field '{displayName}' contains a denied value. Disallowed values: {options}.",
			"options"
		);

		writer.IfBlock(
			$"{comparisonExpression.Replace("propertyValue", propertyValueName)}",
			ifBody => WriteAddError(ifBody, ValueSetError(property, message, displayValues))
		);

		writer.NewLine();
	}

	/// <summary>
	/// The <c>invalid_value</c> error for an allowed/denied value set, carrying the listed values as the
	/// <c>options</c> parameter and the value's Zod type as its origin.
	/// </summary>
	static string ValueSetError(ZodPropertyDescriptor property, MessageTemplate message, string displayValues) =>
		ErrorExpression(
			"\"invalid_value\"",
			message,
			PathOf(property),
			MemberOf(property),
			origin: $"global::ZodSharp.Core.ZodTypeNames.Of(typeof({property.PropertyType.AsTypeReference()}))",
			category: Category("InvalidValue"),
			valuesExpression: ValuesExpression(("options", displayValues.StringLiteral()))
		);

	static bool TryBuildValueSetComparison(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		EquatableArray<TypedConstant> values,
		out string comparisonExpression,
		out string displayValues
	)
	{
		List<string> comparisons = [with(values.Count)];
		var propertyTypeReference = property.PropertyType.AsTypeReference();
		var propertyTypeForComparer = property.CanBeNull
			? propertyTypeReference.Nullable(writer)
			: propertyTypeReference;

		for (var i = 0; i < values.Count; i++)
			if (TryBuildTypedConstantExpression(property, values[i], out var expression))
				comparisons.Add(
					BuildEqualityComparisonExpression(propertyTypeForComparer, "propertyValue", expression)
				);

		comparisonExpression = comparisons.Count == 0 ? "false" : string.Join(" || ", comparisons);
		displayValues = BuildValueListDisplay(values);
		return comparisons.Count > 0;
	}

	static string BuildEqualityComparisonExpression(
		TypeReference propertyType,
		string propertyValueExpression,
		string constantExpression
	)
	{
		return $"global::System.Collections.Generic.EqualityComparer<{propertyType}>.Default.Equals({propertyValueExpression}, {constantExpression})";
	}

	static string BuildValueListDisplay(ImmutableArray<TypedConstant> values)
	{
		if (values.IsDefaultOrEmpty)
			return string.Empty;

		List<string> parts = [with(values.Length)];
		for (var i = 0; i < values.Length; i++)
		{
			var value = values[i];
			if (value.IsNull)
			{
				parts.Add("null");
				continue;
			}

			parts.Add(
				value.Value is string text ? text
				: TryGetEnumMemberName(value, out var memberName) ? memberName
				: Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty
			);
		}

		return string.Join(", ", parts);
	}

	/// <summary>
	/// Resolves the member name of an enum constant, so a value set lists <c>Active</c> rather than <c>1</c>.
	/// </summary>
	static bool TryGetEnumMemberName(TypedConstant constant, out string memberName)
	{
		memberName = string.Empty;
		if (constant.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType || constant.Value is null)
			return false;

		foreach (var member in enumType.GetMembers())
		{
			if (member is IFieldSymbol { HasConstantValue: true } field && Equals(field.ConstantValue, constant.Value))
			{
				memberName = field.Name;
				return true;
			}
		}

		return false;
	}

	static bool TryBuildTypedConstantExpression(
		ZodPropertyDescriptor property,
		TypedConstant constant,
		out string expression
	)
	{
		if (constant.IsNull)
		{
			if (!property.CanBeNull)
			{
				expression = string.Empty;
				return false;
			}

			expression = "null";
			return true;
		}

		if (property.IsEnum)
		{
			expression =
				$"({property.PropertyType.AsTypeReference()}){Convert.ToString(constant.Value, CultureInfo.InvariantCulture)}";
			return true;
		}

#pragma warning disable IDE0072 // Add missing cases
		expression = property.PropertyType.SpecialType switch
		{
			SpecialType.System_String when constant.Value is string value => value.StringLiteral(),
			SpecialType.System_Char when constant.Value is char value => CodeGenHelpers.QuoteChar(value),
			SpecialType.System_Boolean when constant.Value is bool value => value ? "true" : "false",
			SpecialType.System_Byte when constant.Value is byte value => value.ToString(CultureInfo.InvariantCulture),
			SpecialType.System_SByte when constant.Value is sbyte value =>
				$"(sbyte){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int16 when constant.Value is short value =>
				$"(short){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_UInt16 when constant.Value is ushort value =>
				$"(ushort){value.ToString(CultureInfo.InvariantCulture)}",
			SpecialType.System_Int32 when constant.Value is int value => value.ToString(CultureInfo.InvariantCulture),
			SpecialType.System_UInt32 when constant.Value is uint value =>
				$"{value.ToString(CultureInfo.InvariantCulture)}U",
			SpecialType.System_Int64 when constant.Value is long value =>
				$"{value.ToString(CultureInfo.InvariantCulture)}L",
			SpecialType.System_UInt64 when constant.Value is ulong value =>
				$"{value.ToString(CultureInfo.InvariantCulture)}UL",
			SpecialType.System_Single when constant.Value is float value => value.ToString(
				"R",
				CultureInfo.InvariantCulture
			) + "F",
			SpecialType.System_Double when constant.Value is double value => value.ToString(
				"R",
				CultureInfo.InvariantCulture
			) + "D",
			SpecialType.System_Decimal when constant.Value is decimal value => value.ToString(
				CultureInfo.InvariantCulture
			) + "M",
			_ => string.Empty,
		};
#pragma warning restore IDE0072 // Add missing cases

		return expression.Length > 0;
	}
}

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateValueSetValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
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
		var displayName = property.DisplayName;
		var messageExpression = BuildErrorMessageExpression(
			allowedValues.Value.ValidationAttribute,
			"Field '{0}' must be one of the following values: {1}.",
			displayName.StringLiteral(),
			displayValues.StringLiteral()
		);

		writer.IfBlock(
			$"!({comparisonExpression.Replace("propertyValue", propertyValueName)})",
			ifBody =>
				WriteValidationError(
					ifBody,
					"invalid_value",
					messageExpression,
					CodeGenHelpers.GetPathFieldName(propertyName)
				)
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
		var displayName = property.DisplayName;
		var messageExpression = BuildErrorMessageExpression(
			deniedValues.Value.ValidationAttribute,
			"Field '{0}' contains a denied value. Disallowed values: {1}.",
			displayName.StringLiteral(),
			displayValues.StringLiteral()
		);

		writer.IfBlock(
			$"{comparisonExpression.Replace("propertyValue", propertyValueName)}",
			ifBody =>
				WriteValidationError(
					ifBody,
					"invalid_value",
					messageExpression,
					CodeGenHelpers.GetPathFieldName(propertyName)
				)
		);

		writer.NewLine();
	}

	static bool TryBuildValueSetComparison(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		EquatableArray<TypedConstant> values,
		out string comparisonExpression,
		out string displayValues
	)
	{
		List<string> comparisons = new(values.Count);
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

		List<string> parts = new(values.Length);
		for (var i = 0; i < values.Length; i++)
		{
			var value = values[i];
			if (value.IsNull)
			{
				parts.Add("null");
				continue;
			}

			parts.Add(
				value.Value is string text
					? text
					: Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty
			);
		}

		return string.Join(", ", parts);
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

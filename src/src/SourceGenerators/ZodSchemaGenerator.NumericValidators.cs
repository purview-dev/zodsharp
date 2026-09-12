using System.Globalization;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateRangeValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var rangeAttribute = property.ValidationAttributes.Range;
		if (!rangeAttribute.ShouldProcess || !rangeAttribute.Value.Exists)
			return;

		var range = rangeAttribute.Value;
		if (string.IsNullOrEmpty(range.MinimumExpression) || string.IsNullOrEmpty(range.MaximumExpression))
			return;

		var propertyName = property.Name;
		var displayName = property.DisplayName;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var minComparison = range.MinimumIsExclusive ? "<=" : "<";
		var maxComparison = range.MaximumIsExclusive ? ">=" : ">";
		var minimumDescription = range.MinimumIsExclusive ? "greater than" : "greater than or equal to";
		var maximumDescription = range.MaximumIsExclusive ? "less than" : "less than or equal to";
		var minimumDisplay = Convert.ToString(range.Minimum, CultureInfo.InvariantCulture) ?? string.Empty;
		var maximumDisplay = Convert.ToString(range.Maximum, CultureInfo.InvariantCulture) ?? string.Empty;
		var messageExpression = BuildMessageExpression(
			range.ValidationAttribute,
			$"Field '{displayName}' must be {minimumDescription} {minimumDisplay} and {maximumDescription} {maximumDisplay}.".StringLiteral(),
			displayName.StringLiteral(),
			minimumDisplay.StringLiteral(),
			maximumDisplay.StringLiteral()
		);

		var comparisonExpression = property.CompareViaCompareTo
			? $"{propertyValueName}.CompareTo({GetRangeMinimumFieldName(propertyName)}) {minComparison} 0 || {propertyValueName}.CompareTo({GetRangeMaximumFieldName(propertyName)}) {maxComparison} 0"
			: $"{propertyValueName} {minComparison} {GetRangeMinimumFieldName(propertyName)} || {propertyValueName} {maxComparison} {GetRangeMaximumFieldName(propertyName)}";

		using (writer.OpenBlockScope())
		{
			writer.Assignment("var", propertyValueName, $"value.{propertyName}");
			using (writer.IfBlockScope(comparisonExpression))
			{
				WriteValidationError(
					writer,
					"invalid_range",
					messageExpression,
					CodeGenHelpers.GetPathFieldName(propertyName)
				);
			}
		}

		writer.NewLine();
	}
}

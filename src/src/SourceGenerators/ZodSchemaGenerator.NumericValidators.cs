using System.Globalization;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	/// <summary>
	/// Emits the <c>[Range]</c> check. A failure reports the runtime <c>RangeRule</c>'s code
	/// (<c>invalid_range</c>) with the category by direction (<c>too_small</c>/<c>too_big</c>), the origin of the
	/// value's Zod type, and both bounds with their exclusivity (<c>MinimumIsExclusive</c>,
	/// <c>MaximumIsExclusive</c>).
	/// </summary>
	static void GenerateRangeValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var rangeAttribute = property.ValidationAttributes.Range;
		if (!rangeAttribute.ShouldProcess || !rangeAttribute.Value.Exists)
			return;

		var range = rangeAttribute.Value;
		if (string.IsNullOrEmpty(range.MinimumExpression) || string.IsNullOrEmpty(range.MaximumExpression))
			return;

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var minimumField = GetRangeMinimumFieldName(propertyName);
		var maximumField = GetRangeMaximumFieldName(propertyName);
		var minComparison = range.MinimumIsExclusive ? "<=" : "<";
		var maxComparison = range.MaximumIsExclusive ? ">=" : ">";
		var minimumDescription = range.MinimumIsExclusive ? "greater than" : "greater than or equal to";
		var maximumDescription = range.MaximumIsExclusive ? "less than" : "less than or equal to";

		// The bounds are reported as typed values (Lower/Upper). A number range also reports its minimum/maximum
		// parameters as numbers (so ProblemDetails writes JSON numbers), which render in the message exactly as
		// declared. A converted non-number range (a date or time string) keeps the attribute's literal text, so its
		// message reads as declared rather than in the type's invariant rendering.
		var message = BuildMessageTemplate(
			range.ValidationAttribute,
			$"Field '{{displayName}}' must be {minimumDescription} {{minimum}} and {maximumDescription} {{maximum}}.",
			"minimum",
			"maximum"
		);
		var values =
			property.ValidationKind == PropertyValidationKind.Numeric
				? ValuesExpression(("minimum", minimumField), ("maximum", maximumField))
				: ValuesExpression(
					(
						"minimum",
						(Convert.ToString(range.Minimum, CultureInfo.InvariantCulture) ?? string.Empty).StringLiteral()
					),
					(
						"maximum",
						(Convert.ToString(range.Maximum, CultureInfo.InvariantCulture) ?? string.Empty).StringLiteral()
					)
				);
		var lower = Bound(minimumField, !range.MinimumIsExclusive);
		var upper = Bound(maximumField, !range.MaximumIsExclusive);
		var origin = $"global::ZodSharp.Core.ZodTypeNames.Of(typeof({property.PropertyType.AsTypeReference()}))";

		var tooSmallCondition = property.CompareViaCompareTo
			? $"{propertyValueName}.CompareTo({minimumField}) {minComparison} 0"
			: $"{propertyValueName} {minComparison} {minimumField}";
		var tooBigCondition = property.CompareViaCompareTo
			? $"{propertyValueName}.CompareTo({maximumField}) {maxComparison} 0"
			: $"{propertyValueName} {maxComparison} {maximumField}";

		using (writer.OpenBlockScope())
		{
			writer.Assignment("var", propertyValueName, $"value.{propertyName}");
			writer
				.IfBlock(tooSmallCondition, w =>
					WriteAddError(
						w,
						ErrorExpression(
							"global::ZodSharp.Rules.RangeRule<int>.ErrorCode",
							message,
							PathOf(property),
							MemberOf(property),
							origin: origin,
							category: Category("TooSmall"),
							lowerExpression: lower,
							upperExpression: upper,
							valuesExpression: values
						)
					)
				)
				.ElseIf(tooBigCondition, w =>
					WriteAddError(
						w,
						ErrorExpression(
							"global::ZodSharp.Rules.RangeRule<int>.ErrorCode",
							message,
							PathOf(property),
							MemberOf(property),
							origin: origin,
							category: Category("TooBig"),
							lowerExpression: lower,
							upperExpression: upper,
							valuesExpression: values
						)
					)
				);
		}

		writer.NewLine();
	}
}

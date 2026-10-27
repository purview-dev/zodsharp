using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	/// <summary>
	/// Determines whether the automatic enum validation is emitted for the property. A flags combination is a
	/// valid value without being a defined member, and an explicit allow-list governs the property itself, so
	/// neither is validated by the enum rule.
	/// </summary>
	/// <param name="property">The property to validate.</param>
	/// <param name="validateEnumValues">Whether the schema asked for the automatic enum validation.</param>
	/// <returns><see langword="true"/> when the enum rule is emitted.</returns>
	static bool IsEnumValidationEmitted(ZodPropertyDescriptor property, bool validateEnumValues)
	{
		if (!validateEnumValues || !property.IsEnum || property.IsFlagsEnum)
			return false;

		var allowedValues = property.ValidationAttributes.AllowedValues;
		return !(allowedValues.ShouldProcess && allowedValues.Value.Exists);
	}

	/// <summary>
	/// Emits the automatic enum validation: an <c>EnumRule&lt;TEnum&gt;</c> instance the property value must
	/// satisfy. The rule owns the reported error identity, so a failure surfaces as the rule's
	/// <c>invalid_enum_value</c> code and message.
	/// </summary>
	/// <param name="writer">The writer positioned inside the generated <c>Validate</c> method.</param>
	/// <param name="property">The enum property the rule validates.</param>
	static void GenerateEnumValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var propertyName = property.Name;
		var ruleVariable = CodeGenHelpers.GetLocalIdentifier(propertyName, "EnumRule");
		var valueVariable = CodeGenHelpers.GetLocalIdentifier(propertyName, "EnumRuleValue");
		var ruleType = GetEnumRuleType(property);

		// The rule is closed with the underlying enum type, so a nullable property is unwrapped. The
		// validation always runs inside the non-null guard, so the unwrap cannot throw.
		var valueExpression = property.IsNullableValueType ? $"value.{propertyName}!.Value" : $"value.{propertyName}";
		writer.Assignment("var", valueVariable, valueExpression);
		writer.Assignment("var", ruleVariable, $"new {ruleType}({GetDisallowedEnumValuesExpression(property)})");

		// The rule implements IZodRule, so it owns its error identity (code, origin, category, options); a
		// [DeniedValues] attribute that configures its own message replaces only the message.
		var message = GetEnumValidationMessage(property);
		var ruleError =
			$"global::ZodSharp.Core.ZodRuleErrors.Create({ruleVariable}, {valueVariable}, {PathOf(property)}, \"enum\")";
		var errorExpression = message is { } template
			? $"{GeneratedValidation}.ForMember({ruleError}, {MemberOf(property)}, messageFormat: {template.FormatExpression}{(template.OrdinalNamesExpression is null ? string.Empty : $", ordinalNames: {template.OrdinalNamesExpression}")})"
			: MemberError(ruleError, MemberOf(property));

		writer.IfBlock($"!{ruleVariable}.IsValid({valueVariable})", ifBody => WriteAddError(ifBody, errorExpression));

		writer.NewLine();
	}

	static string GetEnumRuleType(ZodPropertyDescriptor property) =>
		$"{TypeLibrary.ZodSharp.Rules.EnumRule.MakeGeneric(property.PropertyType)}";

	/// <summary>
	/// Builds the disallowed-member argument: the enum members marked <c>[ZodIgnore]</c> plus the values the
	/// property's <c>[DeniedValues]</c> attribute lists. Empty when every defined member is allowed.
	/// </summary>
	/// <param name="property">The enum property the rule validates.</param>
	/// <returns>The array-creation expression, or <see cref="string.Empty"/> for none.</returns>
	static string GetDisallowedEnumValuesExpression(ZodPropertyDescriptor property)
	{
		var deniedValues = property.ValidationAttributes.DeniedValues;
		if (property.IgnoredEnumMembers.Count == 0 && !(deniedValues.ShouldProcess && deniedValues.Value.Exists))
			return string.Empty;

		List<string> expressions = [.. property.IgnoredEnumMembers];
		var values = deniedValues.Value.Values;
		for (var i = 0; i < values.Count; i++)
			if (TryBuildTypedConstantExpression(property, values[i], out var expression))
				expressions.Add(expression);

		return $"new {property.PropertyType}[] {{{string.Join(", ", expressions)}}}";
	}

	/// <summary>
	/// Builds the replacement message template when a <c>[DeniedValues]</c> attribute configures its own
	/// message; otherwise the rule's own message is used.
	/// </summary>
	/// <param name="property">The enum property the rule validates.</param>
	/// <returns>The template, or <see langword="null"/> to keep the rule's message.</returns>
	static MessageTemplate? GetEnumValidationMessage(ZodPropertyDescriptor property)
	{
		var deniedValues = property.ValidationAttributes.DeniedValues;
		var validationAttribute = deniedValues.Value.ValidationAttribute;

		return deniedValues.ShouldProcess && deniedValues.Value.Exists && HasConfiguredMessage(validationAttribute)
			? BuildMessageTemplate(validationAttribute, "Field '{displayName}' is invalid.")
			: null;
	}

	static bool HasConfiguredMessage(ValidationAttributeData validationAttribute) =>
		!string.IsNullOrEmpty(validationAttribute.ErrorMessage)
		|| (
			!string.IsNullOrEmpty(validationAttribute.ErrorMessageResourceName)
			&& validationAttribute.ErrorMessageResourceType is not null
		);
}

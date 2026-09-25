using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	/// <summary>
	/// Emits validation for rules bound to the property through DataAnnotations-style attributes that
	/// carry <c>[ZodRule(typeof(...))]</c>. The emitted call reuses the rule's own
	/// <c>IsValid</c>/<c>GetErrorMessage</c> contract, so a custom rule behaves exactly like a built-in one.
	/// </summary>
	static void GenerateCustomRuleValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		if (property.CustomRules.Count == 0)
			return;

		GenerateRuleValidations(
			writer,
			property.CustomRules,
			property.Name,
			$"value.{property.Name}",
			CodeGenHelpers.GetPathFieldName(property.Name),
			property.DisplayName,
			declareValueLocal: true
		);
	}

	/// <summary>
	/// Emits type-level rules: <c>[ZodRule]</c>-mapped attributes applied to the target type itself. They
	/// validate the whole value (the value object as a unit) and report an empty path.
	/// </summary>
	static void GenerateTypeRuleValidations(CodeWriter writer, ZodSchemaDescriptor schema)
	{
		if (schema.TypeRules.Count == 0)
			return;

		GenerateRuleValidations(
			writer,
			schema.TypeRules,
			schema.TargetType.Name,
			"value",
			"EmptyPath",
			schema.TargetType.Name,
			declareValueLocal: false
		);
	}

	static void GenerateRuleValidations(
		CodeWriter writer,
		EquatableArray<CustomRuleDescriptor> rules,
		string localPrefix,
		string valueExpression,
		string pathExpression,
		string displayName,
		bool declareValueLocal
	)
	{
		for (var i = 0; i < rules.Count; i++)
		{
			var rule = rules[i];
			var ruleVariable = CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRule{i}");
			var valueVariable = declareValueLocal
				? CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRuleValue{i}")
				: valueExpression;
			var arguments = rule.Arguments.Count == 0 ? string.Empty : $"({string.Join(", ", rule.Arguments)})";

			if (declareValueLocal)
				writer.Assignment("var", valueVariable, valueExpression);

			writer.Assignment("var", ruleVariable, $"new {rule.RuleType.AsTypeReference().RenderFullName}{arguments}");

			var codeFallback = rule.Code is { Length: > 0 } customCode ? customCode : "validation_failed";
			var zodRuleInterface = TypeLibrary.ZodSharp.Core.IZodRule.AsTypeReference().RenderFullName;

			// A rule that implements IZodRule owns its error identity; the attribute-mapped value is only a
			// fallback. The cast is required because the interface may be implemented explicitly.
			var codeExpression = rule.RuleOwnsIdentity
				? $"(({zodRuleInterface}){ruleVariable}).Code ?? {codeFallback.Surround()}"
				: codeFallback.Surround();
			var originFallback = rule.Origin is { Length: > 0 } customOrigin ? customOrigin.Surround() : "null";
			var originExpression = rule.RuleOwnsIdentity
				? $"(({zodRuleInterface}){ruleVariable}).Origin ?? {originFallback}"
				: originFallback;

			var message = !string.IsNullOrEmpty(rule.Message.ErrorMessage)
				? BuildErrorMessageExpression(rule.Message, "Field '{0}' is invalid.", displayName.StringLiteral())
				: $"{ruleVariable}.GetErrorMessage({valueVariable})";

			writer.IfBlock(
				$"!{ruleVariable}.IsValid({valueVariable})",
				ifBody =>
				{
					ifBody.IfBlock(
						"errors is null",
						errorsBody =>
							errorsBody.Assignment(
								"errors",
								"new global::System.Collections.Generic.List<global::ZodSharp.Core.ValidationError>()"
							)
					);
					ifBody.MethodCallOn(
						"errors",
						"Add",
						$"{TypeLibrary.ZodSharp.Core.ValidationError}.Create({codeExpression}, {message}, {pathExpression}, origin: {originExpression})"
					);
				}
			);

			writer.NewLine();
		}
	}
}

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
	/// <param name="writer">The writer positioned inside the validation method.</param>
	/// <param name="property">The property being validated.</param>
	/// <param name="skipRequiredRule">
	/// Whether a rule that rejects an absent value (<c>IRequiredRule</c>) has already been emitted outside the
	/// non-null guard and must not be emitted again here.
	/// </param>
	static void GenerateCustomRuleValidations(CodeWriter writer, ZodPropertyDescriptor property, bool skipRequiredRule)
	{
		if (property.CustomRules.Count == 0)
			return;

		// The resolver closes a generic rule over the property's underlying type, so a nullable value type is
		// unwrapped to match. The validation runs inside the non-null guard, so the unwrap cannot throw.
		var valueExpression = property.IsNullableValueType ? $"value.{property.Name}!.Value" : $"value.{property.Name}";

		for (var i = 0; i < property.CustomRules.Count; i++)
		{
			var rule = property.CustomRules[i];

			// A required rule is emitted outside the guard by GenerateRequiredRuleValidation; emitting it here
			// would duplicate it and hide a missing value behind the guard.
			if (skipRequiredRule && rule.IsRequired)
				continue;

			EmitCustomRule(
				writer,
				rule,
				property.Name,
				i,
				valueExpression,
				PathOf(property),
				MemberOf(property),
				declareValueLocal: true,
				fallbackOrigin: GetFallbackOrigin(property)
			);
		}
	}

	/// <summary>
	/// Emits a rule that rejects an absent value (<c>IRequiredRule</c>) before the non-null guard, so a
	/// missing value fails exactly as <c>[Required]</c> fails instead of being skipped. The remaining
	/// validations run only when this rule passes.
	/// </summary>
	/// <param name="writer">The writer positioned inside the validation method.</param>
	/// <param name="property">The property being validated.</param>
	/// <param name="requiredRule">The resolved required rule.</param>
	static void GenerateRequiredRuleValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		CustomRuleDescriptor requiredRule
	)
	{
		var propertyName = property.Name;
		var ruleVariable = CodeGenHelpers.GetLocalIdentifier(propertyName, "RequiredRule");
		var arguments = $"({string.Join(", ", requiredRule.Arguments)})";

		var identityVariable = EmitRuleInstance(
			writer,
			requiredRule,
			CodeGenHelpers.GetLocalIdentifier(propertyName, "RequiredRuleInner"),
			ruleVariable,
			arguments
		);

		if (requiredRule.GuardedScalarPropertyName is { Length: > 0 } scalarPropertyName)
		{
			// The rule is closed over the scalar's unwrapped underlying value, so both an absent property and a
			// present scalar with an absent underlying value are missing.
			var scalarExpression = property.IsNullableValueType
				? $"value.{propertyName}.Value"
				: $"value.{propertyName}";

			WriteRuleFailure(
				writer,
				requiredRule,
				ruleVariable,
				identityVariable,
				"default!",
				PathOf(property),
				MemberOf(property),
				GetFallbackOrigin(property),
				invalidCondition: $"value.{propertyName} == null || {scalarExpression}.{scalarPropertyName} == null"
			);
			return;
		}

		if (property.IsNullableValueType)
		{
			// The rule is closed over the underlying value, so it cannot be handed null. An absent value is
			// reported directly, matching how [Required] handles a Nullable<T> member.
			WriteRuleFailure(
				writer,
				requiredRule,
				ruleVariable,
				identityVariable,
				"default!",
				PathOf(property),
				MemberOf(property),
				GetFallbackOrigin(property),
				invalidCondition: $"value.{propertyName} == null"
			);
			return;
		}

		WriteRuleFailure(
			writer,
			requiredRule,
			ruleVariable,
			identityVariable,
			$"value.{propertyName}",
			PathOf(property),
			MemberOf(property),
			GetFallbackOrigin(property)
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
			TypeMemberFieldName,
			declareValueLocal: false
		);
	}

	static void GenerateRuleValidations(
		CodeWriter writer,
		EquatableArray<CustomRuleDescriptor> rules,
		string localPrefix,
		string valueExpression,
		string pathExpression,
		string memberExpression,
		bool declareValueLocal
	)
	{
		for (var i = 0; i < rules.Count; i++)
		{
			EmitCustomRule(
				writer,
				rules[i],
				localPrefix,
				i,
				valueExpression,
				pathExpression,
				memberExpression,
				declareValueLocal,
				fallbackOrigin: null
			);
		}
	}

	static void EmitCustomRule(
		CodeWriter writer,
		CustomRuleDescriptor rule,
		string localPrefix,
		int index,
		string valueExpression,
		string pathExpression,
		string memberExpression,
		bool declareValueLocal,
		string? fallbackOrigin
	)
	{
		if (rule.GuardedScalarPropertyName is { Length: > 0 } scalarPropertyName)
		{
			EmitGuardedScalarRule(
				writer,
				rule,
				localPrefix,
				index,
				valueExpression,
				scalarPropertyName,
				pathExpression,
				memberExpression,
				fallbackOrigin
			);
			return;
		}

		var ruleVariable = CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRule{index}");
		var valueVariable = declareValueLocal
			? CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRuleValue{index}")
			: valueExpression;
		var arguments = $"({string.Join(", ", rule.Arguments)})";

		if (declareValueLocal)
			writer.Assignment("var", valueVariable, valueExpression);

		var identityVariable = EmitRuleInstance(
			writer,
			rule,
			CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRuleInner{index}"),
			ruleVariable,
			arguments
		);

		WriteRuleFailure(
			writer,
			rule,
			ruleVariable,
			identityVariable,
			valueVariable,
			pathExpression,
			memberExpression,
			fallbackOrigin
		);

		writer.NewLine();
	}

	/// <summary>
	/// Emits a rule that targets a nullable value scalar's underlying value. The rule is closed over the
	/// non-nullable value type, so it is only invoked when the value is present; a required rule instead
	/// reports the absent value itself. This mirrors how a <c>Nullable&lt;T&gt;</c> property is validated.
	/// </summary>
	/// <param name="writer">The writer positioned inside the validation method.</param>
	/// <param name="rule">The resolved guarded rule descriptor.</param>
	/// <param name="localPrefix">The prefix for generated local variable names.</param>
	/// <param name="index">The rule's index within its owner's rule list.</param>
	/// <param name="scalarExpression">The expression holding the scalar value object.</param>
	/// <param name="scalarPropertyName">The scalar property that holds the nullable underlying value.</param>
	/// <param name="pathExpression">The error path expression.</param>
	/// <param name="memberExpression">The member-info expression for the failing member.</param>
	/// <param name="fallbackOrigin">The origin reported when neither the rule nor the attribute supplies one.</param>
	static void EmitGuardedScalarRule(
		CodeWriter writer,
		CustomRuleDescriptor rule,
		string localPrefix,
		int index,
		string scalarExpression,
		string scalarPropertyName,
		string pathExpression,
		string memberExpression,
		string? fallbackOrigin
	)
	{
		var ruleVariable = CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRule{index}");
		var identityVariable = CodeGenHelpers.GetLocalIdentifier(localPrefix, $"CustomRuleInner{index}");
		var arguments = $"({string.Join(", ", rule.Arguments)})";
		var underlyingExpression = $"{scalarExpression}.{scalarPropertyName}";

		if (rule.IsRequired)
		{
			// A required rule rejects an absent underlying value; it does not need to run over a present one.
			var requiredIdentity = EmitRuleInstance(writer, rule, identityVariable, ruleVariable, arguments);
			WriteRuleFailure(
				writer,
				rule,
				ruleVariable,
				requiredIdentity,
				"default!",
				pathExpression,
				memberExpression,
				fallbackOrigin,
				invalidCondition: $"{underlyingExpression} == null"
			);
			writer.NewLine();
			return;
		}

		writer.IfBlock($"{underlyingExpression} is {{ }} scalarValue", body =>
		{
			var identity = EmitRuleInstance(body, rule, identityVariable, ruleVariable, arguments);
			WriteRuleFailure(
				body,
				rule,
				ruleVariable,
				identity,
				"scalarValue",
				pathExpression,
				memberExpression,
				fallbackOrigin
			);
		});

		writer.NewLine();
	}

	/// <summary>
	/// Emits the rule instance. A scalar rule adapter wraps a rule written against the scalar's underlying
	/// value: the wrapped rule is constructed first so it can supply the error identity (the adapter itself
	/// has none).
	/// </summary>
	/// <param name="writer">The writer positioned inside the validation method.</param>
	/// <param name="rule">The resolved rule descriptor.</param>
	/// <param name="innerVariable">The local name for the wrapped rule when the rule is adapted.</param>
	/// <param name="ruleVariable">The local name for the rule instance.</param>
	/// <param name="arguments">The rendered rule constructor arguments.</param>
	/// <returns>The local name holding the rule that owns the error identity.</returns>
	static string EmitRuleInstance(
		CodeWriter writer,
		CustomRuleDescriptor rule,
		string innerVariable,
		string ruleVariable,
		string arguments
	)
	{
		if (rule.AdaptedFrom is { } adaptedRuleType)
		{
			writer.Assignment(
				"var",
				innerVariable,
				$"new {adaptedRuleType.AsTypeReference().RenderFullName}{arguments}"
			);
			writer.Assignment(
				"var",
				ruleVariable,
				$"new {rule.RuleType.AsTypeReference().RenderFullName}({innerVariable})"
			);
			return innerVariable;
		}

		writer.Assignment("var", ruleVariable, $"new {rule.RuleType.AsTypeReference().RenderFullName}{arguments}");
		return ruleVariable;
	}

	/// <summary>
	/// Emits the failure branch that records a rule's error when it does not validate the value.
	/// </summary>
	/// <param name="writer">The writer positioned inside the validation method.</param>
	/// <param name="rule">The resolved rule descriptor.</param>
	/// <param name="ruleVariable">The local name holding the rule instance.</param>
	/// <param name="identityVariable">The local name holding the rule that owns the error identity.</param>
	/// <param name="valueVariable">The expression passed to the rule for the message.</param>
	/// <param name="pathExpression">The error path expression.</param>
	/// <param name="memberExpression">The member-info expression for the failing member.</param>
	/// <param name="fallbackOrigin">The origin reported when neither the rule nor the attribute supplies one.</param>
	/// <param name="invalidCondition">
	/// The condition that reports the failure. Defaults to the rule's own <c>!IsValid(...)</c> contract; a
	/// required rule closed over a value type supplies an explicit null comparison instead.
	/// </param>
	static void WriteRuleFailure(
		CodeWriter writer,
		CustomRuleDescriptor rule,
		string ruleVariable,
		string identityVariable,
		string valueVariable,
		string pathExpression,
		string memberExpression,
		string? fallbackOrigin,
		string? invalidCondition = null
	)
	{
		var codeFallback = rule.Code is { Length: > 0 } customCode ? customCode : "validation_failed";
		var zodRuleInterface = TypeLibrary.ZodSharp.Core.IZodRule.AsTypeReference().RenderFullName;

		// A rule that implements IZodRule owns its error identity; the attribute-mapped value is only a
		// fallback. The cast is required because the interface may be implemented explicitly.
		var codeExpression = rule.RuleOwnsIdentity
			? $"(({zodRuleInterface}){identityVariable}).Code ?? {codeFallback.Surround()}"
			: codeFallback.Surround();
		var originExpression =
			rule.Origin is { Length: > 0 } customOrigin ? customOrigin.Surround()
			: fallbackOrigin is null ? "null"
			: fallbackOrigin;

		// The rule's structured identity (origin, category, bounds, parameters, its failure description) is
		// copied by ZodRuleErrors, so a generated failure matches the runtime schema's failure for the same
		// rule. An adapted rule has no identity of its own, so the wrapped rule supplies it.
		var originArgument = originExpression == "null" ? string.Empty : $", fallbackOrigin: {originExpression}";
		var ruleError =
			identityVariable == ruleVariable && valueVariable != "default!"
				? $"global::ZodSharp.Core.ZodRuleErrors.Create({ruleVariable}, {valueVariable}, {pathExpression}{originArgument}, code: {codeExpression})"
				: $"global::ZodSharp.Core.ZodRuleErrors.Build({codeExpression}, {ruleVariable}.GetErrorMessage({valueVariable}), {pathExpression}, (object){identityVariable} as {zodRuleInterface}{originArgument})";

		var errorExpression =
			!string.IsNullOrEmpty(rule.Message.ErrorMessage)
			|| (
				!string.IsNullOrEmpty(rule.Message.ErrorMessageResourceName)
				&& rule.Message.ErrorMessageResourceType is not null
			)
				? ForMemberWithMessage(
					ruleError,
					memberExpression,
					BuildMessageTemplate(rule.Message, "Field '{displayName}' is invalid.")
				)
				: $"{GeneratedValidation}.ForMember({ruleError}, {memberExpression}, fallbackCategory: {Category("Custom")})";

		writer.IfBlock(invalidCondition ?? $"!{ruleVariable}.IsValid({valueVariable})", ifBody =>
			WriteAddError(ifBody, errorExpression)
		);
	}

	static string ForMemberWithMessage(string errorExpression, string memberExpression, MessageTemplate message) =>
		message.OrdinalNamesExpression is null
			? $"{GeneratedValidation}.ForMember({errorExpression}, {memberExpression}, fallbackCategory: {Category("Custom")}, messageFormat: {message.FormatExpression})"
			: $"{GeneratedValidation}.ForMember({errorExpression}, {memberExpression}, fallbackCategory: {Category("Custom")}, messageFormat: {message.FormatExpression}, ordinalNames: {message.OrdinalNamesExpression})";

	/// <summary>
	/// The origin reported for a custom rule that supplies none: the member's Zod type for the scalar kinds the
	/// generator recognizes, so a custom string rule reports <c>string</c> like the built-in string checks.
	/// </summary>
	static string? GetFallbackOrigin(ZodPropertyDescriptor property) =>
		property.ValidationKind switch
		{
			PropertyValidationKind.String => StringOrigin,
			PropertyValidationKind.Numeric or PropertyValidationKind.Comparable or PropertyValidationKind.Enum =>
				$"global::ZodSharp.Core.ZodTypeNames.Of(typeof({property.PropertyType.AsTypeReference()}))",
			PropertyValidationKind.Collection or PropertyValidationKind.Complex or PropertyValidationKind.Unsupported =>
				null,
			_ => null,
		};
}

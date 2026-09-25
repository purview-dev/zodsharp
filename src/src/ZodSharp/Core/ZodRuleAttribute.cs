namespace ZodSharp.Core;

/// <summary>
/// Connects a <see cref="System.ComponentModel.DataAnnotations.ValidationAttribute"/> to the ZodSharp
/// validation rule that performs the validation, or marks a rule for generation of a matching
/// DataAnnotations-style attribute.
/// </summary>
/// <remarks>
/// <para>
/// The attribute has two roles:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Applied to a validation attribute (for example <c>NoWhitespaceAttribute</c>) it declares which rule
/// the attribute maps to: <c>[ZodRule(typeof(NoWhitespaceRule))]</c>. The <c>[ZodSchema]</c> generator
/// then emits rule-based validation for properties annotated with that attribute, exactly as it does for
/// the built-in DataAnnotations attributes.
/// </description>
/// </item>
/// <item>
/// <description>
/// Applied to a rule (for example <c>NoWhitespaceRule</c>) without a rule type it asks the generator to
/// emit a matching DataAnnotations-style attribute (<c>NoWhitespaceAttribute</c>) whose properties mirror
/// the rule's constructor parameters.
/// </description>
/// </item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ZodRuleAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ZodRuleAttribute"/> class that marks a rule for
	/// validation-attribute generation.
	/// </summary>
	public ZodRuleAttribute() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodRuleAttribute"/> class that maps a validation
	/// attribute to <paramref name="ruleType"/>.
	/// </summary>
	/// <param name="ruleType">The <c>IValidationRule&lt;T&gt;</c> implementation the attribute maps to.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="ruleType"/> is null.</exception>
	public ZodRuleAttribute(Type ruleType) => RuleType = ruleType ?? throw new ArgumentNullException(nameof(ruleType));

	/// <summary>
	/// Gets the rule type the attribute maps to, or <see langword="null"/> when the attribute marks a rule
	/// for validation-attribute generation.
	/// </summary>
	public Type? RuleType { get; }

	/// <summary>
	/// Gets the error code reported when the rule fails. When omitted, the generated validation reports
	/// <c>"validation_failed"</c>.
	/// </summary>
	public string? Code { get; init; }

	/// <summary>
	/// Gets the <see cref="ValidationError.Origin"/> reported when the rule fails.</summary>
	public string? Origin { get; init; }

	/// <summary>
	/// Gets the name of the validation attribute to generate when marking a rule. When omitted, the name is
	/// derived from the rule name (a trailing <c>Rule</c> is replaced with <c>Attribute</c>).
	/// </summary>
	public string? AttributeName { get; init; }
}

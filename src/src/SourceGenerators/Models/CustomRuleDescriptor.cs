using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// Describes a validation rule that is bound to a property through a DataAnnotations-style attribute
/// carrying <c>[ZodRule(typeof(...))]</c>.
/// </summary>
/// <param name="RuleType">The rule type to instantiate.</param>
/// <param name="Code">The error code to report when the rule fails; defaults to <c>validation_failed</c>.</param>
/// <param name="Origin">The structured <c>Origin</c> to report when the rule fails.</param>
/// <param name="Message">The error-message configuration taken from the attribute.</param>
/// <param name="Arguments">The rule constructor argument expressions, in constructor parameter order.</param>
/// <param name="RuleOwnsIdentity">
/// Whether the rule implements <c>IZodRule</c> and therefore supplies its own code/origin at runtime.
/// </param>
readonly record struct CustomRuleDescriptor(
	TypeIdentity RuleType,
	string? Code,
	string? Origin,
	ValidationAttributeData Message,
	EquatableArray<string> Arguments,
	bool RuleOwnsIdentity
);

using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// Describes a validation rule that is bound to a property through a DataAnnotations-style attribute
/// carrying <c>[ZodRule(typeof(...))]</c>.
/// </summary>
/// <param name="RuleType">The rule type to instantiate.</param>
/// <param name="AdaptedFrom">
/// The rule wrapped by <paramref name="RuleType"/> when it is a scalar rule adapter, otherwise
/// <see langword="null"/>. A scalar value object is validated through its underlying value, so a rule
/// written against that value is constructed first and passed to the adapter.
/// </param>
/// <param name="Code">The error code to report when the rule fails; defaults to <c>validation_failed</c>.</param>
/// <param name="Origin">The structured <c>Origin</c> to report when the rule fails.</param>
/// <param name="Message">The error-message configuration taken from the attribute.</param>
/// <param name="Arguments">The rule constructor argument expressions, in constructor parameter order.</param>
/// <param name="RuleOwnsIdentity">
/// Whether the rule implements <c>IZodRule</c> and therefore supplies its own code/origin at runtime. When
/// the rule is adapted, this reflects the wrapped rule, which is what supplies the identity.
/// </param>
/// <param name="IsRequired">
/// Whether the rule implements <c>IRequiredRule</c> and therefore rejects an absent value. Such a rule is
/// emitted outside the generator's non-null guard so a missing value fails exactly as
/// <c>[Required]</c> fails.
/// </param>
/// <param name="GuardedScalarPropertyName">
/// When the rule targets a nullable value scalar's underlying value, the name of the scalar property that
/// holds it. The rule is then instantiated directly against the unwrapped value type and emitted under a
/// non-null guard (or as a required check), rather than wrapped in a scalar rule adapter. Null for every
/// other rule.
/// </param>
readonly record struct CustomRuleDescriptor(
	TypeIdentity RuleType,
	TypeIdentity? AdaptedFrom,
	string? Code,
	string? Origin,
	ValidationAttributeData Message,
	EquatableArray<string> Arguments,
	bool RuleOwnsIdentity,
	bool IsRequired,
	string? GuardedScalarPropertyName
);

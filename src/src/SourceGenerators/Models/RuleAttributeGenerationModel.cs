namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// Describes the DataAnnotations-style validation attribute generated for a rule marked with
/// <c>[ZodRule]</c> (the parameterless form).
/// </summary>
/// <param name="RuleType">The rule the attribute maps to.</param>
/// <param name="AttributeType">The attribute type to emit.</param>
/// <param name="Accessibility">The accessibility of the generated attribute.</param>
/// <param name="Code">The error code the mapping reports, when explicitly configured.</param>
/// <param name="Origin">The structured origin the mapping reports, when explicitly configured.</param>
/// <param name="Properties">The generated attribute properties, mirroring the rule constructor parameters.</param>
readonly record struct RuleAttributeGenerationModel(
	TypeIdentity RuleType,
	TypeIdentity AttributeType,
	TypeDeclarationAccessibility Accessibility,
	string? Code,
	string? Origin,
	EquatableArray<GeneratedAttributeProperty> Properties
);

/// <summary>
/// Describes a settable property on a generated validation attribute.
/// </summary>
/// <param name="Type">The property type.</param>
/// <param name="Name">The property name.</param>
/// <param name="Initializer">The optional initializer expression.</param>
/// <param name="IsNullable">Whether the property type is nullable.</param>
readonly record struct GeneratedAttributeProperty(TypeIdentity Type, string Name, string? Initializer, bool IsNullable);

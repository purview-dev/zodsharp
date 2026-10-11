using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators.Helpers;

static class DiagnosticLibrary
{
	const string Category = "ZodSharp.SourceGenerator";

	public static readonly DiagnosticDescriptor UnhandledException = new(
		id: "ZODSGEN001",
		title: "Unhandled exception",
		messageFormat: "Source generator failed for {0}: {1}",
		category: "ZodSharp.SourceGenerator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InvalidLengthAttribute = new(
		id: "ZODSGEN003",
		title: "Invalid LengthAttribute configuration",
		messageFormat: "Invalid configuration for the LengthAttribute on '{0}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedLengthAttributeTarget = new(
		id: "ZODSGEN004",
		title: "Unsupported LengthAttribute target",
		messageFormat: "Unsupported LengthAttribute target on '{0}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor InvalidDataAnnotationsErrorMessage = new(
		id: "ZODSGEN005",
		title: "Invalid DataAnnotations error message resource configuration",
		messageFormat: "Invalid Data Annotations error message resource configuration on '{0}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedDataAnnotationsUsage = new(
		id: "ZODSGEN006",
		title: "Unsupported DataAnnotations usage",
		messageFormat: "Unsupported Data Annotations type on '{0}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationMethodNotFound = new(
		id: "ZODSGEN007",
		title: "Custom validation method not found",
		messageFormat: "Custom validation method '{0}' was configured for schema type '{1}', but no matching method was found",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidReturnType = new(
		id: "ZODSGEN008",
		title: "Invalid custom validation method return type",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must return 'ValueTask<ValidationResult<{1}>>'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidParameterCount = new(
		id: "ZODSGEN009",
		title: "Invalid custom validation method parameter count",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must accept exactly two parameters: (T value, CancellationToken cancellationToken)",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidModelParameter = new(
		id: "ZODSGEN010",
		title: "Invalid custom validation method model parameter",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must have the schema model type as its first parameter",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidCancellationToken = new(
		id: "ZODSGEN011",
		title: "Invalid custom validation method cancellation token parameter",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must have 'CancellationToken' as its second parameter",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationGenericMethod = new(
		id: "ZODSGEN012",
		title: "Generic custom validation method unsupported",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must not be generic",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidStaticInstance = new(
		id: "ZODSGEN013",
		title: "Invalid custom validation method static/instance form",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must be static because the generated validator is a separate type",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInaccessible = new(
		id: "ZODSGEN014",
		title: "Inaccessible custom validation method",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' is not accessible from the generated validator",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationAmbiguousOverloads = new(
		id: "ZODSGEN015",
		title: "Ambiguous custom validation method overloads",
		messageFormat: "Multiple custom validation methods named '{0}' match the required signature for schema type '{1}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidMethodName = new(
		id: "ZODSGEN016",
		title: "Invalid custom validation method name",
		messageFormat: "Custom validation method name '{0}' configured for schema type '{1}' is not a valid C# identifier",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationAbstractMethod = new(
		id: "ZODSGEN017",
		title: "Abstract custom validation method unsupported",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must not be abstract",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationUnimplementedPartial = new(
		id: "ZODSGEN018",
		title: "Unimplemented partial custom validation method",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' is an unimplemented partial method",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor CustomValidationInvalidParameterModifier = new(
		id: "ZODSGEN019",
		title: "Invalid custom validation method parameter modifier",
		messageFormat: "Custom validation method '{0}' on schema type '{1}' must not use ref, in, out, params, or scoped parameters",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ComparePropertyNotFound = new(
		id: "ZODSGEN020",
		title: "Compare property not found",
		messageFormat: "CompareAttribute on '{0}' references unknown property '{1}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor DataAnnotationsReferenceNotFound = new(
		id: "ZODSGEN021",
		title: "Unable to find Data Annotations reference/ types",
		messageFormat: "Add a reference to System.ComponentModel.DataAnnotations",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN034: the OnZodValidate hook is implemented on a type that is not partial (or whose
	/// containing types are not all partial), so the generated declaration cannot be emitted.
	/// </summary>
	public static readonly DiagnosticDescriptor ZodRefinementHookTypeNotPartial = new(
		id: "ZODSGEN034",
		title: "Zod refinement hook requires a partial type",
		messageFormat: "The '{0}' refinement hook on '{1}' requires '{1}' (and every containing type) to be declared 'partial' so the generated hook declaration can be emitted",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN035: the hook member exists but is not the generator-declared
	/// <c>partial void OnZodValidate(RefineCtx&lt;T&gt; context)</c> form.
	/// </summary>
	public static readonly DiagnosticDescriptor ZodRefinementHookInvalidSignature = new(
		id: "ZODSGEN035",
		title: "Invalid Zod refinement hook declaration",
		messageFormat: "The '{0}' refinement hook on '{1}' must be declared as 'partial void OnZodValidate(RefineCtx<T> context)'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor IValidateOptionsReferenceNotFound = new(
		id: "ZODSGEN027",
		title: "Unable to find Microsoft.Extensions.Options reference",
		messageFormat: "IValidateOptions generation was requested for '{0}' but a reference to Microsoft.Extensions.Options is required",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor IValidateOptionsValueTypeTarget = new(
		id: "ZODSGEN028",
		title: "IValidateOptions generation requires a reference type",
		messageFormat: "IValidateOptions generation was requested for '{0}' but IValidateOptions<T> requires T to be a class",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor AmbiguousValidationMethods = new(
		id: "ZODSGEN029",
		title: "Synchronous refinement and async custom validation methods are mutually exclusive",
		messageFormat: "The type '{0}' declares both a synchronous refinement method ('{1}') and an async custom validation method ('{2}'); only one may be used",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedCustomRuleTarget = new(
		id: "ZODSGEN030",
		title: "Custom rule does not support the property type",
		messageFormat: "The rule '{0}' configured by attribute '{1}' does not implement IValidationRule<T> for property type '{2}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnmappableCustomRuleArgument = new(
		id: "ZODSGEN031",
		title: "Unable to map a custom rule argument",
		messageFormat: "Unable to map a value for constructor parameter '{0}' of rule '{1}' from attribute '{2}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedRuleAttributeGeneration = new(
		id: "ZODSGEN032",
		title: "Unable to generate a validation attribute for a custom rule",
		messageFormat: "Unable to generate a validation attribute for rule '{0}': {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor RuleAttributeWithoutSchema = new(
		id: "ZODSGEN033",
		title: "Rule attribute is applied to a type that gets no generated schema",
		messageFormat: "The attribute '{0}' on '{1}' is mapped to a validation rule, but no schema is generated for '{1}', so the rule will not run. Apply [ZodSchema] to the type or reference it as a complex property of a schema.",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>
	/// Reports when a <c>[ZodSchema]</c> target declares the retired synchronous refinement method
	/// (<c>IEnumerable&lt;ValidationError&gt; Validate()</c>) instead of the <c>OnZodValidate</c> hook.
	/// </summary>
	public static readonly DiagnosticDescriptor SyncRefinementMethodRetired = new(
		id: "ZODSGEN036",
		title: "Synchronous refinement method has been replaced by the OnZodValidate hook",
		messageFormat: "Member '{0}' on '{1}' uses the retired synchronous refinement contract; implement 'partial void OnZodValidate(RefineCtx<T> context)' on a partial type instead",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN037: a rule marked with <c>[ZodRule]</c> derives the validation attribute name from its own
	/// name (<c>XRule</c> → <c>XAttribute</c>), which is the same name for both halves of a generic /
	/// non-generic rule pair. When the compilation already declares a type with that name the generated
	/// attribute is suppressed, so the hand-authored declaration's own mapping governs every usage.
	/// </summary>
	public static readonly DiagnosticDescriptor RuleAttributeNameAlreadyDeclared = new(
		id: "ZODSGEN037",
		title: "Rule attribute name is already declared",
		messageFormat: "The rule '{0}' maps to validation attribute '{1}', but '{2}' already declares that name, so no attribute is generated and the existing declaration's own [ZodRule] mapping is used instead",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN038: a hand-authored rule attribute whose name encodes a rule name (<c>XAttribute</c> →
	/// <c>XRule</c>) must map to a rule that addresses every rule declared under that name. A mapping to a
	/// single non-generic rule while an arity-1 generic sibling also exists (or to a rule outside the family)
	/// leaves some usages of the attribute unresolved.
	/// </summary>
	public static readonly DiagnosticDescriptor RuleAttributeMappingIncomplete = new(
		id: "ZODSGEN038",
		title: "Rule attribute mapping does not cover the whole rule family",
		messageFormat: "The attribute '{0}' maps to rule '{1}', which does not cover every rule declared as '{2}'; map the attribute to '{3}' so that every usage of '{0}' resolves",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN039: a rule accepts an identity constructor parameter (<c>code</c>/<c>origin</c>) but does not
	/// implement <c>IZodRule</c>. The value is passed by the generated validation but never surfaced, so the
	/// parameter is inert: implement <c>IZodRule</c> to report it, or remove the parameter.
	/// </summary>
	public static readonly DiagnosticDescriptor RuleIdentityParameterNotImplemented = new(
		id: "ZODSGEN039",
		title: "Rule identity parameter is never surfaced",
		messageFormat: "The rule '{0}' accepts a '{1}' constructor parameter but does not implement 'ZodSharp.Core.IZodRule', so the value never reaches the reported error identity; implement 'IZodRule' or remove the parameter",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN040: an attribute supplies an argument that the resolved rule never consumes — it is neither a
	/// constructor parameter nor part of the error identity declared by <c>IZodRuleAttribute</c>, so the value
	/// has no effect on validation.
	/// </summary>
	public static readonly DiagnosticDescriptor UnusedRuleAttributeArgument = new(
		id: "ZODSGEN040",
		title: "Rule attribute argument has no effect",
		messageFormat: "'{0}' on attribute '{1}' has no effect: rule '{2}' has no matching constructor parameter and the value is not part of the reported error identity",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor MessageFormatPlaceholderNotDeclared = new(
		id: "ZODSASP001",
		title: "MessageFormat placeholder is not declared in Parameters",
		messageFormat: "MessageFormat placeholder '{0}' is not declared in ErrorType.Parameters",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeContainingTypeNotPartial = new(
		id: "ZODSASP002",
		title: "Error type containing type must be partial",
		messageFormat: "The containing type '{0}' must be declared 'partial' so that Create/Throw methods can be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeFieldInvalid = new(
		id: "ZODSASP003",
		title: "ErrorType field must be static readonly",
		messageFormat: "The ErrorType field '{0}' must be declared 'static readonly' for Create/Throw methods to be generated",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeUnhandledException = new(
		id: "ZODSASP100",
		title: "Unhandled exception in the ErrorType source generator",
		messageFormat: "The ErrorType source generator failed for '{0}': {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ErrorTypeInvalidParameters = new(
		id: "ZODSASP101",
		title: "Unable to extract ErrorType parameters",
		messageFormat: "The Parameters of ErrorType field '{0}' in '{1}' could not be extracted; only ErrorTypeParameter collection literals with a constant name and typeof, or ErrorType.Param<T> invocations, are supported",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN041: a typed union whose case types are all reference types can use the native C# 15
	/// union returned by <c>Z.NativeUnion</c> on net11+, which is allocation-free and supports
	/// exhaustive pattern matching. Value-type cases are excluded because they box.
	/// </summary>
	public static readonly DiagnosticDescriptor NativeUnionRecommended = new(
		id: "ZODSGEN041",
		title: "Consider a native C# 15 union",
		messageFormat: "'{0}' has only reference-type cases ({1}); on .NET 11+ 'Z.NativeUnion' returns a native union that is allocation-free and supports exhaustive pattern matching",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN042: a validation rule (a type implementing <c>ZodSharp.Core.IValidationRule&lt;T&gt;</c>) does not
	/// expose its error identity as public constants. Every rule should declare <c>public const string ErrorCode</c>
	/// and <c>public const string MessageFormat</c> so its code and message can be asserted in tests without
	/// duplicating literals.
	/// </summary>
	public static readonly DiagnosticDescriptor RuleMissingErrorIdentityConstants = new(
		id: "ZODSGEN042",
		title: "Validation rule should expose public error identity constants",
		messageFormat: "Rule '{0}' should declare {1}: expose 'public const string ErrorCode' and 'public const string MessageFormat' so tests can assert against the rule instead of duplicating literals",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN043: a built-in rule (one declared in the runtime assembly, under <c>ZodSharp.Rules</c>) is marked
	/// with <c>[ZodRule]</c> but cannot produce a validation attribute because a constructor parameter cannot be
	/// represented as an attribute property, so the rule is only reachable through the runtime/fluent API.
	/// Reported as informational so the gap stays visible rather than silent; a derived name that collides with
	/// a <c>System.ComponentModel.DataAnnotations</c> attribute is emitted under a "Zod" suffix instead.
	/// </summary>
	public static readonly DiagnosticDescriptor BuiltInRuleAttributeNotGenerated = new(
		id: "ZODSGEN043",
		title: "Built-in rule does not generate a validation attribute",
		messageFormat: "The built-in rule '{0}' does not generate a validation attribute: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN044: a <c>[ZodSchema]</c> type that also uses an automatic scalar form
	/// (<c>[Scalar&lt;TValue&gt;]</c> or <c>[Scalar(typeof(TValue))]</c>) cannot produce a usable schema, so
	/// the value-object generator's generated <c>Create</c> would reference a <c>{Type}Schema</c> that does
	/// not exist or cannot validate. Reported for a suppressed <c>Validate</c> method and for an underlying
	/// type that cannot be represented.
	/// </summary>
	public static readonly DiagnosticDescriptor AutomaticScalarSchemaUnavailable = new(
		id: "ZODSGEN044",
		title: "Automatic scalar schema cannot be generated",
		messageFormat: "The [ZodSchema] type '{0}' uses an automatic [Scalar] form, but a schema cannot be generated for it: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	/// <summary>
	/// ZODSGEN045: a type that exports JSON Schema references a nested <c>[ZodSchema]</c> type whose export
	/// is disabled (<c>[ZodSchema(GenerateJsonSchema = false)]</c>, or the assembly-level
	/// <c>ZodSharpGenerateJsonSchemas=false</c> switch). The nested type's schema cannot be referenced, so
	/// the parent's exported schema falls back to an empty schema for that property.
	/// </summary>
	public static readonly DiagnosticDescriptor NestedSchemaExportDisabled = new(
		id: "ZODSGEN045",
		title: "Nested type has JSON Schema export disabled",
		messageFormat: "Property '{0}' references type '{1}', which disables JSON Schema generation; the exported schema cannot describe it",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);
}

namespace ZodSharp.SourceGenerators.Helpers;

// Deliberately not public (the default accessibility for a top-level type): the generated type
// library exposes Purview.SourceGeneratorFramework type identities, which the IL merge internalizes,
// so a public spec would leave the merged analyzer with a public member whose signature references
// an internal type. The BuildSdk generates InternalsVisibleTo for the matching unit-test assembly, so
// tests still use it.
[GenerateTypeLibrary]
static partial class TypeLibraryGenerator
{
	public const string ZodSharpNamespace = "ZodSharp";

	public const string ZodSharpCoreNamespace = ZodSharpNamespace + ".Core";

	public const string ZodSharpRulesNamespace = ZodSharpNamespace + ".Rules";

	public const string ZodSharpSchemasNamespace = ZodSharpNamespace + ".Schemas";

	// The Purview.ValueObjects scalar contracts. A scalar value object is validated through its underlying
	// value, so a rule written against that value is adapted by the ScalarRuleAdapter the value-object
	// generator emits into the consuming compilation; the adapter is referenced by name, not by a package.
	public const string ValueObjectsNamespace = "Purview.ValueObjects";

	public const string ValueObjectsSerializationNamespace = ValueObjectsNamespace + ".Serialization";

	// Both the manual [Scalar] attribute and the automatic [Scalar<TValue>] / [Scalar(typeof(TValue))]
	// attributes share this name; they are distinguished by arity and by their constructor arguments.
	public const string ScalarAttributeName = "ScalarAttribute";

	public const string ScalarRuleAdapterName = "ScalarRuleAdapter";

	// Simple name of the refinement context type declared in ZodSharpSchemasNamespace.
	public const string ZodRefineContextName = "RefineCtx";

	// Default custom async validation method name when none is explicitly configured.
	public const string DefaultCustomValidationMethodName = "CustomValidationAsync";

	// Name of the generated partial refinement hook a [ZodSchema] target may implement. The generator
	// declares it, so the IDE offers the implementation and the name is never resolved by convention.
	public const string ZodRefinementHookName = "OnZodValidate";

	// Name of the generated bridge that lets a schema class (a different type) invoke the private partial
	// hook. Keeps the hook itself optional while giving the generated Validate a call target.
	public const string ZodRefinementHookInvokerName = "InvokeZodRefinementHook";

	// The pre-hook synchronous refinement method name. The generator no longer binds it; it is kept so the
	// retirement diagnostic (ZODSGEN036) can recognise the old contract and point at the replacement.
	public const string RetiredSyncRefinementMethodName = "Validate";

	// This matches the name of the class, just so we can use the `nameof` for later...
	[TypeRef(ZodSharpNamespace)]
	static readonly TypeIdentity ZodSchemaAttribute = default;

	[TypeRef(ZodSharpNamespace)]
	static readonly TypeIdentity ZodIgnoreAttribute = default;

	[TypeRef(ZodSharpNamespace)]
	static readonly TypeIdentity CollectionBehaviorAttribute = default;

	[TypeRef(ZodSharpNamespace)]
	static readonly TypeIdentity ZodSharpSchemaDefaultsAttribute = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ZodSchemaGeneratedAttribute = default;

	// Other ZodSharp types...
	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity IZodSchemaValidator = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity IValidationRule = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity IZodRule = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity IRequiredRule = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ZodRuleAttribute = default;

	[TypeRef("System")]
	static readonly TypeIdentity AttributeUsageAttribute = default;

	[TypeRef(ZodSharpRulesNamespace)]
	static readonly TypeIdentity EnumRule = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationResult = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationResultMetadataName = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationError = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ErrorType = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ErrorTypeAttribute = default;

	[TypeRef(ZodSharpSchemasNamespace)]
	static readonly TypeIdentity RefineCtx = default;

	[TypeRef("Microsoft.Extensions.Options")]
	static readonly TypeIdentity IValidateOptions = default;

	[TypeRef("Microsoft.Extensions.Options")]
	static readonly TypeIdentity ValidateOptionsResult = default;

	[TypeRef("System.Diagnostics.CodeAnalysis")]
	static readonly TypeIdentity DoesNotReturnAttribute = default;
}

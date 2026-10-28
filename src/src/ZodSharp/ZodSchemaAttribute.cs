namespace ZodSharp;

/// <summary>
/// Attribute to mark a class for automatic schema generation.
/// When applied to a class, a zero-allocation validator will be generated at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Defined once, here, rather than generated into every consuming compilation. A generated copy gave every project
/// in a solution an identical type and XML documentation ID, which crashed tools that index documentation
/// across a project and its references (the ASP.NET Core OpenAPI XML-comment generator reported "An item with the
/// same key has already been added").
/// </para>
/// <para>
/// The source generator reads the attribute by its metadata name, so it recognises it on types declared in this
/// compilation and on types in referenced assemblies alike.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class ZodSchemaAttribute : Attribute
{
	/// <summary>
	/// Optional name for the generated schema class.
	/// If not specified, uses "{ClassName}Schema".
	/// </summary>
	public string? SchemaName { get; init; }

	/// <summary>
	/// Whether to generate a static Validate method.
	/// Default is true.
	/// </summary>
	public bool GenerateValidateMethod { get; init; } = true;

	/// <summary>
	/// Whether to generate a static Parse method (throws on failure).
	/// Default is true.
	/// </summary>
	public bool GenerateParseMethod { get; init; } = true;

	/// <summary>
	/// Whether to enable composition methods (.and(), .or(), .refine()).
	/// Default is true.
	/// </summary>
	public bool EnableComposition { get; init; } = true;

	/// <summary>
	/// Optional name of a custom asynchronous validation method to invoke
	/// after synchronous validation. When set, the generator looks for a
	/// method with the signature:
	/// <c>ValueTask&lt;ValidationResult&lt;T&gt;&gt; MethodName(T value, CancellationToken cancellationToken)</c>
	/// declared as a static method on the model type or an instance method on the
	/// generated <c>{TypeName}SchemaValidator</c> partial.
	/// If null, the default name <c>CustomValidationAsync</c> is used.
	/// No diagnostic is reported when the default name has no matching method.
	/// Mutually exclusive with the <c>OnZodValidate</c> refinement hook (ZODSGEN029).
	/// </summary>
	public string? CustomValidationMethodName { get; init; }

	/// <summary>
	/// Whether to generate an <c>IValidateOptions&lt;T&gt;</c> validator for this type.
	/// Generation is driven by auto-detection by default: enabled when the type name ends
	/// with a configured suffix (e.g. <c>Options</c> or <c>Settings</c>) and the
	/// <c>ZodSharpAutoGenerateOptionsValidators</c> MSBuild property is not disabled.
	/// Set to true to force generation or set <c>SuppressIValidateOptions</c> to opt out.
	/// </summary>
	public bool GenerateIValidateOptions { get; init; }

	/// <summary>
	/// Suppresses generation of the <c>IValidateOptions&lt;T&gt;</c> validator for this type,
	/// even when auto-detection would otherwise enable it.
	/// </summary>
	public bool SuppressIValidateOptions { get; init; }

	/// <summary>
	/// Whether enum properties are validated automatically: the generated validator rejects a
	/// value that is not a defined member of the enum type, or that resolves to a member
	/// excluded from the allowed set (an enum member marked [ZodIgnore], or a value the
	/// property's [DeniedValues] attribute lists). [Flags] enums are never validated.
	/// Default is true.
	/// </summary>
	public bool ValidateEnumValues { get; init; } = true;

	/// <summary>
	/// Whether to generate the JSON Schema export members for this type: the static
	/// <c>BuildJsonSchema</c> method and the <c>IJsonSchemaProvider</c> implementation on the
	/// generated validator adapter. Set to <c>false</c> to keep a validation-only type out of the
	/// cross-platform (TypeScript/JavaScript) contract surface. Default is true.
	/// A type whose export is disabled cannot be referenced by an exported type; the generator
	/// falls back to an empty schema and the analyzer reports ZODSGEN045.
	/// </summary>
	public bool GenerateJsonSchema { get; init; } = true;

	/// <summary>
	/// Default for whether this type's collection properties allow null elements.
	/// When unset, the assembly-level default applies (the <c>ZodSharpAllowNulls</c> MSBuild
	/// property, overridden by <c>[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]</c>).
	/// When set, it applies to this type's collections unless a property overrides it with
	/// <c>[CollectionBehavior(AllowNulls = ...)]</c> or an explicitly nullable element type
	/// (<c>T?</c>). The built-in default is to reject null elements.
	/// </summary>
	public bool AllowNulls { get; init; }
}

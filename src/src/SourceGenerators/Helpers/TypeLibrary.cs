namespace ZodSharp.SourceGenerators.Helpers;

[GenerateTypeLibrary]
public static partial class TypeLibraryGenerator
{
	public const string ZodSharpNamespace = "ZodSharp";

	public const string ZodSharpCoreNamespace = ZodSharpNamespace + ".Core";

	public const string ZodSharpSchemasNamespace = ZodSharpNamespace + ".Schemas";

	// Default custom async validation method name when none is explicitly configured.
	public const string DefaultCustomValidationMethodName = "CustomValidationAsync";

	// Default synchronous refinement method name when none is explicitly configured.
	public const string DefaultSyncValidationMethodName = "Validate";

	// This matches the name of the class, just so we can use the `nameof` for later...
	[TypeRef(ZodSharpNamespace)]
	static readonly TypeIdentity ZodSchemaAttribute = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ZodSchemaGeneratedAttribute = default;

	// Other ZodSharp types...
	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity IZodSchemaValidator = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationResult = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationResultMetadataName = default;

	[TypeRef(ZodSharpCoreNamespace)]
	static readonly TypeIdentity ValidationError = default;

	[TypeRef(ZodSharpSchemasNamespace)]
	static readonly TypeIdentity RefineCtx = default;

	[TypeRef("Microsoft.Extensions.Options")]
	static readonly TypeIdentity IValidateOptions = default;

	[TypeRef("Microsoft.Extensions.Options")]
	static readonly TypeIdentity ValidateOptionsResult = default;
}

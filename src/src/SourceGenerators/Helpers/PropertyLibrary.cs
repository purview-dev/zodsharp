namespace ZodSharp.SourceGenerators.Helpers;

static class PropertyLibrary
{
	/// <summary>
	/// The prefix the compiler gives an MSBuild property declared as <c>CompilerVisibleProperty</c> in the
	/// generated globalconfig (<c>build_property.Name = value</c>). The separator is a dot, not a colon.
	/// </summary>
	public const string BuildPropertyPrefix = "build_property.";

	/// <summary>
	/// Builds the analyzer config key the compiler uses for an MSBuild property.
	/// </summary>
	/// <param name="propertyName">The MSBuild property name.</param>
	/// <returns>The <c>build_property.{propertyName}</c> key.</returns>
	public static string BuildPropertyKey(string propertyName) => BuildPropertyPrefix + propertyName;

	public const string DisableZodSharpSourceGeneratorProperty = "DisableZodSharpSourceGenerator";

	public const string DisableErrorTypeGeneratorProperty = "DisableErrorTypeGenerator";

	public const string AutoGenerateIValidateOptionsProperty = "ZodSharpAutoGenerateOptionsValidators";

	public const string AutoGenerateIValidateOptionsSuffixesProperty = "ZodSharpAutoGenerateOptionsValidatorSuffixes";

	public const string GenerateJsonSchemasProperty = "ZodSharpGenerateJsonSchemas";

	public const string EnumSerializationProperty = "ZodSharpEnumSerialization";

	public const string EnumNamingPolicyProperty = "ZodSharpEnumNamingPolicy";

	public const string AllowNullsProperty = "ZodSharpAllowNulls";

	public const string DefaultImmutableArrayBehaviorProperty = "ZodSharpDefaultImmutableArrayBehavior";

	public const string DateTimeOffsetSerializationProperty = "ZodSharpDateTimeOffsetSerialization";
}

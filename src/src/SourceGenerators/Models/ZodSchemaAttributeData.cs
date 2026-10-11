namespace ZodSharp.SourceGenerators.Models;

[Generate("ZodSharp.ZodSchemaAttribute")]
readonly partial record struct ZodSchemaAttributeData(
	string? SchemaName,
	[Property(DefaultValue = true)] bool GenerateValidateMethod,
	[Property(DefaultValue = true)] bool GenerateParseMethod,
	[Property(DefaultValue = true)] bool EnableComposition,
	string? CustomValidationMethodName,
	[Property(DefaultValue = false)] bool GenerateIValidateOptions,
	[Property(DefaultValue = false)] bool SuppressIValidateOptions,
	[Property(DefaultValue = true)] bool ValidateEnumValues,
	[Property(DefaultValue = true)] bool GenerateJsonSchema
);

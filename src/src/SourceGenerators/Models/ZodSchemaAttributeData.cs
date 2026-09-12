namespace ZodSharp.SourceGenerators.Models;

[Generate("ZodSharp.ZodSchemaAttribute")]
readonly partial record struct ZodSchemaAttributeData(
	string? SchemaName,
	[Property(DefaultValue = true)] bool GenerateValidateMethod,
	[Property(DefaultValue = true)] bool GenerateParseMethod,
	[Property(DefaultValue = false)] bool EnableComposition,
	string? CustomValidationMethodName,
	string? RefinementMethodName,
	[Property(DefaultValue = false)] bool GenerateIValidateOptions,
	[Property(DefaultValue = false)] bool SuppressIValidateOptions
);

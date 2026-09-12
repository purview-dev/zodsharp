using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators.Infra;

public sealed record ZodSourceGeneratorTestOptions : SourceGeneratorTestOptions
{
	public ZodSourceGeneratorTestOptions()
	{
		AdditionalAssemblyTypes =
		[
			typeof(Z),
			typeof(ImmutableArray),
			typeof(RequiredAttribute),
			typeof(System.Text.Json.JsonSerializer),
			typeof(System.Text.RegularExpressions.Regex),
			typeof(Schemas.RefineCtx<>),
			typeof(Microsoft.Extensions.Options.IValidateOptions<>),
			typeof(Microsoft.Extensions.Options.ValidateOptionsResult),
		];
		AdditionalNamespaces = [TypeLibraryGenerator.ZodSharpNamespace];
		ExcludeGeneratedSourceHintNames = ["EmbeddedAttribute", "ZodSchemaAttribute"];
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableZodSharpSourceGeneratorProperty;
	}

	public static readonly ZodSourceGeneratorTestOptions NoValidation = new() { ThrowOnGenerationException = false };

	public static readonly ZodSourceGeneratorTestOptions Compile = new() { CompileToAssembly = true };
}

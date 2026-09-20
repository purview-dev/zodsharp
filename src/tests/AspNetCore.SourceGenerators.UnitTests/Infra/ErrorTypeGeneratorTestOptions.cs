using System.Collections.Immutable;
using ZodSharp.AspNetCore.SourceGenerators.Helpers;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore.SourceGenerators.Infra;

public sealed record ErrorTypeGeneratorTestOptions : SourceGeneratorTestOptions
{
	public ErrorTypeGeneratorTestOptions()
	{
		AdditionalNamespaces = ["ZodSharp.AspNetCore", "ZodSharp.Core"];
		AdditionalAssemblyTypes =
		[
			typeof(ErrorType),
			typeof(ErrorTypeParameter),
			typeof(ErrorTypeParameters),
			typeof(ValidationError),
			typeof(ZodException),
			typeof(ImmutableArray),
		];
		ExcludeGeneratedSourceHintNames = ["ErrorTypeAttribute", "EmbeddedAttribute"];
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableAspNetCoreErrorTypeGeneratorProperty;
	}
}

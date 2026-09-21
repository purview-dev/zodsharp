using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators.Infra;

public sealed record ErrorTypeGeneratorTestOptions : SourceGeneratorTestOptions
{
	public ErrorTypeGeneratorTestOptions()
	{
		AdditionalNamespaces = ["ZodSharp.Core"];
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
		DisableSourceGeneratorPropertyName = PropertyLibrary.DisableErrorTypeGeneratorProperty;
	}
}

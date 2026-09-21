using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// Value-equatable capability facts for the <see cref="ErrorTypeGenerator"/>. No Roslyn
/// objects are retained in this model.
/// </summary>
sealed record ErrorTypeGeneratorCapabilities(bool HasErrorType, bool HasErrorTypeParameter, bool HasErrorTypeParameters)
	: IGenerationCapabilities
{
	public static ErrorTypeGeneratorCapabilities Create(Compilation compilation) =>
		new(
			compilation.GetTypeByMetadataName(ErrorTypeMetadataName) is not null,
			compilation.GetTypeByMetadataName(ErrorTypeParameterMetadataName) is not null,
			compilation.GetTypeByMetadataName(ErrorTypeParametersMetadataName) is not null
		);

	const string ErrorTypeMetadataName = "ZodSharp.Core.ErrorType";

	const string ErrorTypeParameterMetadataName = "ZodSharp.Core.ErrorTypeParameter";

	const string ErrorTypeParametersMetadataName = "ZodSharp.Core.ErrorTypeParameters";
}

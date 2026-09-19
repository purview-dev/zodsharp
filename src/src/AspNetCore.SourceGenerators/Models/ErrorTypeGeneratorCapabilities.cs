using Microsoft.CodeAnalysis;

namespace ZodSharp.AspNetCore.SourceGenerators.Models;

/// <summary>
/// Value-equatable capability facts for the <see cref="ErrorTypeGenerator"/>. No Roslyn
/// objects are retained in this model.
/// </summary>
sealed record ErrorTypeGeneratorCapabilities(bool HasErrorType) : IGenerationCapabilities
{
	public static ErrorTypeGeneratorCapabilities Create(Compilation compilation) =>
		new(compilation.GetTypeByMetadataName(ErrorTypeMetadataName) is not null);

	const string ErrorTypeMetadataName = "ZodSharp.AspNetCore.ErrorType";
}

namespace ZodSharp.AspNetCore.SourceGenerators.Models;

/// <summary>
/// Value-equatable model combining the generation context with the discovered error type fields.
/// </summary>
sealed record ErrorTypeGenerationModel(
	GenerationContext<ErrorTypeGeneratorCapabilities> Context,
	EquatableArray<GeneratorResult<ErrorTypeFieldModel>> Fields
);

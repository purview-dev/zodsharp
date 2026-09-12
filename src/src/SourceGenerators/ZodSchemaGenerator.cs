using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Source generator that creates optimized validators for classes marked with [ZodSchema].
/// Uses IIncrementalGenerator for better performance and incremental compilation support.
/// </summary>
[Generator]
public sealed partial class ZodSchemaGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<ZodSchemaGenerator>()
			.RegisterPostInitializationOutput(static ctx =>
			{
				foreach (var (HintName, SourceText) in AttributeGenHelper.GenerateMarkers())
					ctx.AddSource($"{HintName}.g.cs", SourceText);
			});

		var generationValueProviders = SourceGenLibrary.GetGeneratorValueProviders(context);

		context.RegisterSourceOutput(
			generationValueProviders,
			static (spc, model) =>
			{
				if (model.Context.Settings.IsSourceGeneratorDisabled)
					return;

				if (!model.Context.Capabilities.HasRequiredAttribute)
					return;

				foreach (var schema in model.ZodSchemas)
				{
					if (!schema.ShouldProcess)
						continue;

					try
					{
						SchemaGenerationOutputContext outputContext = new(model.Context, schema.Value);
						BuildSchema(outputContext, spc, schema.Value.IsPrimary);
					}
					catch (CodeWriterScopeValidationException)
					{
						throw;
					}
					catch (Exception ex)
					{
						var diagnostic = ReportableDiagnostic.Create(
							DiagnosticLibrary.UnhandledException,
							true,
							schema.Value.TargetType.Name,
							ex.Message
						);

						spc.ReportDiagnostic(diagnostic.ToDiagnostic());
					}
				}
			}
		);
	}
}

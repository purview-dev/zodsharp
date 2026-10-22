using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Source generator that creates optimized validators for classes marked with [ZodSchema].
/// Uses IIncrementalGenerator for better performance and incremental compilation support.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "The generator intentionally composes the framework emitters, the schema model and the rule library."
)]
[Generator]
public sealed partial class ZodSchemaGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context
			.RegisterEmbeddedAttribute<ZodSchemaGenerator>()
			.RegisterPostInitializationOutput(static ctx =>
			{
				foreach (var (hintName, sourceText) in AttributeGenHelper.GenerateMarkers())
					ctx.AddSource($"{hintName}.g.cs", sourceText);
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
					// CodeWriterScopeValidationException was caught here and rethrown, to fail loudly on a
					// generator bug. It reached the consumer as CS8785 instead, which discards every file this
					// generator produced for the compilation and buries the real cause under the 'type or
					// namespace not found' errors that follow (PSGFR44). It now takes the same path as any
					// other failure: the exception message lists every open scope, so the diagnostic is no less
					// loud, and the rest of the generated output still arrives.
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

		context.RegisterSourceOutput(
			generationValueProviders.Combine(GetRuleAttributeProvider(context).Collect()),
			static (spc, pair) =>
			{
				if (pair.Left.Context.Settings.IsSourceGeneratorDisabled)
					return;

				EmitRuleAttributes(spc, pair.Left, pair.Right);
			}
		);
	}
}

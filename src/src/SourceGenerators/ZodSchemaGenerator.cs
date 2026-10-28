using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

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
		// No post-initialization output: [ZodSchema] is defined once in Purview.ZodSharp. A copy generated into every
		// compilation gave each project in a solution an identical type and XML documentation ID, which crashed the
		// ASP.NET Core OpenAPI XML-comment generator ("An item with the same key has already been added").
		var generationValueProviders = SourceGenLibrary.GetGeneratorValueProviders(context);

		context.RegisterSourceOutput(generationValueProviders, static (spc, model) =>
		{
			if (model.Context.Settings.IsSourceGeneratorDisabled)
				return;

			if (!model.Context.Capabilities.HasRequiredAttribute)
				return;

			List<ZodSchemaDescriptor> processed = [];
			foreach (var schema in model.ZodSchemas)
			{
				if (schema.ShouldProcess)
					processed.Add(schema.Value);
			}

			// Which schemas need asynchronous validation is a property of the whole graph (a nested schema
			// can make its parent asynchronous), so it is decided once per output from the collected schemas.
			AsyncSchemaIndex asyncIndex = new(processed);

			foreach (var schema in model.ZodSchemas)
			{
				if (!schema.ShouldProcess)
					continue;

				try
				{
					SchemaGenerationOutputContext outputContext = new(model.Context, schema.Value, asyncIndex);
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

			try
			{
				GenerateAssemblyRegistration(spc, model, processed);
			}
			catch (Exception ex)
			{
				spc.ReportDiagnostic(
					ReportableDiagnostic
						.Create(DiagnosticLibrary.UnhandledException, true, "assembly registration", ex.Message)
						.ToDiagnostic()
				);
			}
		});

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

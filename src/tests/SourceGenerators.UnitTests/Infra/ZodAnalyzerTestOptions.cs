namespace ZodSharp.SourceGenerators.Infra;

public sealed record ZodAnalyzerTestOptions : AnalyzerTestOptions
{
	public ZodAnalyzerTestOptions()
	{
		AdditionalAssemblyTypes =
		[
			typeof(Z),
			typeof(System.Collections.Immutable.ImmutableArray),
			typeof(System.ComponentModel.DataAnnotations.RequiredAttribute),
			typeof(System.Text.Json.JsonSerializer),
			typeof(System.Text.RegularExpressions.Regex),
			typeof(Core.ValidationResult<>),
			typeof(ValueTask),
			typeof(CancellationToken),
			typeof(Schemas.RefineCtx<>),
			typeof(Microsoft.Extensions.Options.IValidateOptions<>),
			typeof(Microsoft.Extensions.Options.ValidateOptionsResult),
		];
		AdditionalNamespaces = ["ZodSharp"];
		AdditionalSources =
		[
			"""
				namespace ZodSharp;

				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
				public sealed class ZodSchemaAttribute : System.Attribute
				{
					public string? SchemaName { get; init; }
					public bool GenerateValidateMethod { get; init; } = true;
					public bool GenerateParseMethod { get; init; } = true;
					public bool EnableComposition { get; init; } = false;
					public string? CustomValidationMethodName { get; init; }
					public string? RefinementMethodName { get; init; }
					public bool GenerateIValidateOptions { get; init; } = false;
					public bool SuppressIValidateOptions { get; init; } = false;
				}
				""",
		];
	}
}

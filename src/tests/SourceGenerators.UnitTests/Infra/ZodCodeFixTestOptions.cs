namespace ZodSharp.SourceGenerators.Infra;

public sealed record ZodCodeFixTestOptions : CodeFixTestOptions
{
	public ZodCodeFixTestOptions()
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
	}
}

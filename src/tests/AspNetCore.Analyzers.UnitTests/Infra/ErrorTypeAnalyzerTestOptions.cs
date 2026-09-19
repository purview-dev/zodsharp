namespace ZodSharp.AspNetCore.Analyzers.Infra;

public sealed record ErrorTypeAnalyzerTestOptions : AnalyzerTestOptions
{
	public ErrorTypeAnalyzerTestOptions()
	{
		AdditionalNamespaces = ["ZodSharp.AspNetCore"];
		AdditionalSources =
		[
			"""
				namespace ZodSharp.AspNetCore;

				public sealed record ErrorType(
					string Code,
					string? Description = null,
					int HttpStatus = 400,
					string? Title = null,
					string? Type = null,
					string? MessageFormat = null)
				{
					public System.Collections.Generic.IReadOnlyList<string> Parameters { get; init; } = [];
				}
				""",
		];
	}
}

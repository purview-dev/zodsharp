using ZodSharp.Core;

namespace ZodSharp.SourceGenerators.Infra;

public sealed record ErrorTypeAnalyzerTestOptions : AnalyzerTestOptions
{
	public ErrorTypeAnalyzerTestOptions()
	{
		AdditionalNamespaces = ["ZodSharp.Core"];
		AdditionalAssemblyTypes = [typeof(ErrorType), typeof(ErrorTypeParameter)];
	}
}

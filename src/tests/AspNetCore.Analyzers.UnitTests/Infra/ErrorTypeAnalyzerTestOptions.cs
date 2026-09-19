namespace ZodSharp.AspNetCore.Analyzers.Infra;

public sealed record ErrorTypeAnalyzerTestOptions : AnalyzerTestOptions
{
	public ErrorTypeAnalyzerTestOptions()
	{
		AdditionalNamespaces = ["ZodSharp.AspNetCore"];
		AdditionalAssemblyTypes = [typeof(ErrorType)];
		// The generator emits this attribute at compile time; the analyzer-only harness
		// provides the same surface so the analyzer can resolve it.
		AdditionalSources =
		[
			"""
				namespace ZodSharp.AspNetCore;

				[global::System.AttributeUsage(global::System.AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
				public sealed class ErrorTypeAttribute : global::System.Attribute { }
				""",
		];
	}
}

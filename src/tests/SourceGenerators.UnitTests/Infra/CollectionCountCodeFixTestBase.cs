using ZodSharp.CodeFixes;

namespace ZodSharp.SourceGenerators.Infra;

public abstract class CollectionCountCodeFixTestBase
	: TUnitCodeFixTestBase<
		CollectionCountAttributeAnalyzer,
		CollectionCountAttributeCodeFixProvider,
		ZodCodeFixTestOptions
	>
{
	// Empty
}

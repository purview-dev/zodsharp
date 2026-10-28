using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators.Infra;

public abstract class ZodSharpSourceGeneratorTestBase<TGenerator>
	: TUnitSourceGeneratorTestBase<TGenerator, ZodSourceGeneratorTestOptions>
	where TGenerator : class, IIncrementalGenerator, new()
{
	/// <summary>
	/// The sources the generator emits into every compilation, whatever it contains. None: <c>[ZodSchema]</c> and
	/// <c>[ErrorType]</c> are defined once in Purview.ZodSharp, so no marker type is duplicated across the
	/// projects of a solution.
	/// </summary>
	public static readonly string[] GeneratedAttributes = [];

	public static readonly int ExpectedFileCount = GeneratedAttributes.Length;

	public static readonly int ExpectedFileCountPlusGen = ExpectedFileCount + 1;

	public const int HintNameHashHexLength = 16;

	public const string GeneratedSourceFileSuffix = ".g.cs";
}

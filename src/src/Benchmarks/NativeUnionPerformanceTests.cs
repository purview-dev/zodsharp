#if NET11_0_OR_GREATER
using BenchmarkDotNet.Attributes;
using ZodSharp.Schemas;
using ZodSharp.Unions;

namespace ZodSharp;

/// <summary>
/// Compares the hand-rolled union with the .NET 11 native union when the case type is a value type. The native
/// union implements the non-boxing access pattern, so both the schema result and pattern matching stay
/// allocation-free; the hand-rolled union stores each case in its own typed field for the same reason. The
/// comparison exists so a regression (a declared <c>union</c>, or a missing <c>TryGetValue</c>) shows up as an
/// allocation in the memory column.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class NativeUnionPerformanceTests
{
	readonly ZodTypedUnion<double, string> _typedUnionSchema;
	readonly ZodTypedNativeUnion<double, string> _nativeUnionSchema;

	public NativeUnionPerformanceTests()
	{
		_typedUnionSchema = Z.Union(Z.Number(), Z.String());
		_nativeUnionSchema = Z.NativeUnion(Z.Number(), Z.String());
	}

	[Benchmark(Baseline = true)]
	public double TypedUnion_NumberCase() => Describe(_typedUnionSchema.Validate(42d).Value);

	[Benchmark]
	public double NativeUnion_NumberCase() => Describe(_nativeUnionSchema.Validate(42d).Value);

	static double Describe(in Union<double, string> value) => value.TryGetValue(out double number) ? number : 0d;

	static double Describe(in NativeUnion<double, string> value) =>
		value switch
		{
			double number => number,
			string => 0d,
		};
}
#endif

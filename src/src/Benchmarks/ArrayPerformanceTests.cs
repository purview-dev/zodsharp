using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Performance tests for array validation scenarios with varying sizes.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ArrayPerformanceTests
{
	readonly ZodArray<string> _smallArraySchema;
	readonly ZodArray<string> _mediumArraySchema;
	readonly ZodArray<string> _largeArraySchema;
	readonly ZodArray<double> _numberArraySchema;

	readonly string[] _smallArray;
	readonly string[] _mediumArray;
	readonly string[] _largeArray;
	readonly double[] _numberArray;

	public ArrayPerformanceTests()
	{
		_smallArraySchema = Z.Array(Z.String().Min(1).Max(10)).Min(1).Max(10);
		_mediumArraySchema = Z.Array(Z.String().Email()).Min(1).Max(100);
		_largeArraySchema = Z.Array(Z.String().Min(1).Max(50)).Min(1).Max(1000);
		_numberArraySchema = Z.Array(Z.Number().Min(0).Max(100).Int()).Min(1).Max(1000);

		_smallArray = [.. Enumerable.Range(1, 10).Select(static i => $"item{i}")];
		_mediumArray = [.. Enumerable.Range(1, 100).Select(static i => $"user{i}@example.com")];
		_largeArray = [.. Enumerable.Range(1, 1000).Select(static i => $"item{i}")];
		_numberArray = [.. Enumerable.Range(1, 1000).Select(static i => (double)i)];
	}

	[Benchmark]
	public ValidationResult<string[]> ValidateSmallArray() => _smallArraySchema.Validate(_smallArray);

	[Benchmark]
	public ValidationResult<string[]> ValidateMediumArray() => _mediumArraySchema.Validate(_mediumArray);

	[Benchmark]
	public ValidationResult<string[]> ValidateLargeArray() => _largeArraySchema.Validate(_largeArray);

	[Benchmark]
	public ValidationResult<double[]> ValidateNumberArray() => _numberArraySchema.Validate(_numberArray);

	[Benchmark]
	public ValidationResult<string[]> ValidateLargeArrayWithComplexSchema()
	{
		var schema = Z.Array(
			Z.String().Min(5).Max(100).Email().Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")
		);
		return schema.Validate(_mediumArray);
	}

	[Benchmark]
	public ValidationResult<string[]> ValidateLargeArrayInvalid()
	{
		var invalid = _largeArray.Concat([""]).ToArray();
		return _largeArraySchema.Validate(invalid);
	}
}

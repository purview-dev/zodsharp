using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Memory performance tests to identify allocation issues.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class MemoryPerformanceTests
{
	readonly ZodString _stringSchema;
	readonly ZodArray<double> _arraySchema;
	readonly ZodObject _objectSchema;

	public MemoryPerformanceTests()
	{
		_stringSchema = Z.String().Min(1).Max(100).Email();
		_arraySchema = Z.Array(Z.Number().Min(0).Max(100)).Min(1).Max(100);
		_objectSchema = Z.Object().Field("name", Z.String().Min(1)).Field("age", Z.Number().Min(0)).Build();
	}

	[Benchmark(Baseline = true)]
	public ValidationResult<string> ValidateString_Allocations() => _stringSchema.Validate("user@example.com");

	[Benchmark]
	public ValidationResult<double[]> ValidateArray_Allocations()
	{
		var data = Enumerable.Range(1, 100).Select(static i => (double)i).ToArray();
		return _arraySchema.Validate(data);
	}

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateObject_Allocations()
	{
		Dictionary<string, object?> data = new() { { "name", "John" }, { "age", 30.0 } };
		return _objectSchema.Validate(data);
	}

	[Benchmark]
	public void ValidateString_ManyIterations()
	{
		for (var i = 0; i < 1000; i++)
		{
			_stringSchema.Validate("user@example.com");
		}
	}

	[Benchmark]
	public void ValidateArray_ManyIterations()
	{
		var data = Enumerable.Range(1, 100).Select(static i => (double)i).ToArray();
		for (var i = 0; i < 100; i++)
		{
			_arraySchema.Validate(data);
		}
	}

	[Benchmark]
	public void ValidateObject_ManyIterations()
	{
		Dictionary<string, object?> data = new() { { "name", "John" }, { "age", 30.0 } };
		for (var i = 0; i < 1000; i++)
		{
			_objectSchema.Validate(data);
		}
	}
}

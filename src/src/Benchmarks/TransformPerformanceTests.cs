using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Performance tests for transform operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class TransformPerformanceTests
{
	readonly ZodString _toLowerSchema;
	readonly ZodString _toUpperSchema;
	readonly ZodString _trimSchema;
	readonly ZodString _chainedTransformSchema;
	readonly ZodString _transformWithValidationSchema;

	public TransformPerformanceTests()
	{
		_toLowerSchema = Z.String().ToLower();
		_toUpperSchema = Z.String().ToUpper();
		_trimSchema = Z.String().Trim();
		_chainedTransformSchema = Z.String().Trim().ToLower();
		_transformWithValidationSchema = Z.String().Min(5).Max(100).Trim().ToLower().Email();
	}

	[Benchmark]
	public ValidationResult<string> TransformToLower() => _toLowerSchema.Validate("HELLO WORLD");

	[Benchmark]
	public ValidationResult<string> TransformToUpper() => _toUpperSchema.Validate("hello world");

	[Benchmark]
	public ValidationResult<string> TransformTrim() => _trimSchema.Validate("  hello world  ");

	[Benchmark]
	public ValidationResult<string> TransformChained() => _chainedTransformSchema.Validate("  HELLO WORLD  ");

	[Benchmark]
	public ValidationResult<string> TransformWithValidation() =>
		_transformWithValidationSchema.Validate("  USER@EXAMPLE.COM  ");
}

using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Basic performance tests for common validation scenarios.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class BasicPerformanceTests
{
	readonly ZodString _stringSchema;
	readonly ZodNumber _numberSchema;
	readonly ZodBoolean _booleanSchema;
	readonly ZodArray<string> _arraySchema;
	readonly ZodString _multiRuleStringSchema;
	readonly ZodNumber _multiRuleNumberSchema;
	readonly string[] _stringArray;

	public BasicPerformanceTests()
	{
		_stringSchema = Z.String().Min(3).Max(50).Email();
		_numberSchema = Z.Number().Min(0).Max(100).Int();
		_booleanSchema = Z.Boolean();
		_arraySchema = Z.Array(Z.String()).Min(1).Max(10);
		_multiRuleStringSchema = Z.String()
			.Min(5)
			.Max(100)
			.Email()
			.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
		_multiRuleNumberSchema = Z.Number().Min(0).Max(100).Int().MultipleOf(2);
		_stringArray = ["a", "b", "c", "d", "e"];
	}

	[Benchmark]
	public ValidationResult<string> ValidateString() => _stringSchema.Validate("user@example.com");

	[Benchmark]
	public ValidationResult<double> ValidateNumber() => _numberSchema.Validate(42.0);

	[Benchmark]
	public ValidationResult<bool> ValidateBoolean() => _booleanSchema.Validate(true);

	[Benchmark]
	public ValidationResult<string[]> ValidateStringArray() => _arraySchema.Validate(_stringArray);

	[Benchmark]
	public ValidationResult<string> ValidateStringWithMultipleRules() =>
		_multiRuleStringSchema.Validate("test@example.com");

	[Benchmark]
	public ValidationResult<double> ValidateNumberWithMultipleRules() => _multiRuleNumberSchema.Validate(42.0);
}

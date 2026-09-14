using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Performance tests for object validation scenarios.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ObjectPerformanceTests
{
	readonly ZodObject _simpleObjectSchema;
	readonly ZodObject _mediumObjectSchema;
	readonly ZodObject _complexObjectSchema;

	readonly Dictionary<string, object?> _simpleObject;
	readonly Dictionary<string, object?> _mediumObject;
	readonly Dictionary<string, object?> _complexObject;

	static readonly string[] TagTestingValues = ["developer", "csharp", "dotnet", "performance"];
	static readonly double[] ScoreTestingValues = [95.0, 87.0, 92.0];

	public ObjectPerformanceTests()
	{
		_simpleObjectSchema = Z.Object().Field("name", Z.String().Min(1)).Field("age", Z.Number().Min(0)).Build();

		_mediumObjectSchema = Z.Object()
			.Field("id", Z.String().UUID())
			.Field("name", Z.String().Min(3).Max(100))
			.Field("email", Z.String().Email())
			.Field("age", Z.Number().Min(0).Max(120).Int())
			.Field("active", Z.Boolean())
			.Field("tags", Z.Array(Z.String()).Min(0).Max(10))
			.Build();

		_complexObjectSchema = Z.Object()
			.Field("id", Z.String().UUID())
			.Field("firstName", Z.String().Min(1).Max(50))
			.Field("lastName", Z.String().Min(1).Max(50))
			.Field("email", Z.String().Email())
			.Field("phone", Z.String().Regex(@"^\+?[1-9]\d{1,14}$"))
			.Field("age", Z.Number().Min(0).Max(120).Int())
			.Field("height", Z.Number().Min(0).Max(300).Finite())
			.Field("weight", Z.Number().Min(0).Max(500).Finite())
			.Field("active", Z.Boolean())
			.Field("verified", Z.Boolean())
			.Field("tags", Z.Array(Z.String()).Min(0).Max(20))
			.Field("scores", Z.Array(Z.Number()).Min(0).Max(100))
			.Field(
				"metadata",
				Z.Object()
					.Field("createdAt", Z.String())
					.Field("updatedAt", Z.String())
					.Field("version", Z.Number().Int())
					.Build()
			)
			.Build();

		_simpleObject = new Dictionary<string, object?> { { "name", "John" }, { "age", 30.0 } };

		_mediumObject = new Dictionary<string, object?>
		{
			{ "id", "550e8400-e29b-41d4-a716-446655440000" },
			{ "name", "John Doe" },
			{ "email", "john@example.com" },
			{ "age", 30.0 },
			{ "active", true },
			{ "tags", TagTestingValues },
		};

		_complexObject = new Dictionary<string, object?>
		{
			{ "id", "550e8400-e29b-41d4-a716-446655440000" },
			{ "firstName", "John" },
			{ "lastName", "Doe" },
			{ "email", "john.doe@example.com" },
			{ "phone", "+1234567890" },
			{ "age", 30.0 },
			{ "height", 180.0 },
			{ "weight", 75.0 },
			{ "active", true },
			{ "verified", true },
			{ "tags", TagTestingValues },
			{ "scores", ScoreTestingValues },
			{
				"metadata",
				new Dictionary<string, object?>
				{
					{ "createdAt", "2024-01-01T00:00:00Z" },
					{ "updatedAt", "2024-01-02T00:00:00Z" },
					{ "version", 1.0 },
				}
			},
		};
	}

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateSimpleObject() =>
		_simpleObjectSchema.Validate(_simpleObject);

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateMediumObject() =>
		_mediumObjectSchema.Validate(_mediumObject);

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateComplexObject() =>
		_complexObjectSchema.Validate(_complexObject);

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateComplexObjectInvalid()
	{
		Dictionary<string, object?> invalid = new(_complexObject) { ["email"] = "invalid-email", ["age"] = -1.0 };
		return _complexObjectSchema.Validate(invalid);
	}
}

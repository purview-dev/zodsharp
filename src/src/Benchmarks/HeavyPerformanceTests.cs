using BenchmarkDotNet.Attributes;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

/// <summary>
/// Heavy performance tests for stress scenarios and edge cases.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 3)]
public class HeavyPerformanceTests
{
	readonly ZodObject _deepNestedSchema;
	readonly ZodObject _wideObjectSchema;
	readonly ZodArray<string[]> _nestedArraySchema;
	readonly ZodRefinement<string> _refinementStringSchema;
	readonly ZodObject _largeObjectWithArraysSchema;

	readonly Dictionary<string, object?> _deepNestedObject;
	readonly Dictionary<string, object?> _wideObject;
	readonly string[][] _nestedArray;
	readonly Dictionary<string, object?> _largeObjectWithArrays;

	static readonly string[] TagTestingValues = ["tag1", "tag2", "tag3"];

	public HeavyPerformanceTests()
	{
		var nestedLevel1 = Z.Object().Field("level1", Z.String().Min(1)).Build();

		var nestedLevel2 = Z.Object().Field("level2", nestedLevel1).Build();

		var nestedLevel3 = Z.Object().Field("level3", nestedLevel2).Build();

		_deepNestedSchema = Z.Object().Field("root", nestedLevel3).Build();

		var wideObjectBuilder = Z.Object();
		for (var i = 0; i < 50; i++)
		{
			wideObjectBuilder.Field($"field{i}", Z.String().Min(1).Max(100));
		}

		_wideObjectSchema = wideObjectBuilder.Build();

		var stringArray = Z.Array(Z.String().Min(1));
		var nestedArray = Z.Array(stringArray);
		_nestedArraySchema = nestedArray;

		_refinementStringSchema = Z.String()
			.Min(5)
			.Max(100)
			.Email()
			.Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")
			.StartsWith("user")
			.EndsWith(".com")
			.Refine(static s => s.Contains('@', StringComparison.Ordinal), "Must contain @")
			.Refine(static s => s.Length > 10, "Must be longer than 10")
			.Refine(static s => s.Count(static c => c == '.') <= 2, "Too many dots");

		_largeObjectWithArraysSchema = Z.Object()
			.Field(
				"items",
				Z.Array(
						Z.Object()
							.Field("id", Z.String().UUID())
							.Field("name", Z.String().Min(1).Max(100))
							.Field("tags", Z.Array(Z.String()).Min(0).Max(10))
							.Build()
					)
					.Min(1)
					.Max(100)
			)
			.Build();

		_largeObjectWithArrays = new()
		{
			{
				"items",
				Enumerable
					.Range(1, 100)
					.Select(static i => new Dictionary<string, object?>
					{
						{ "id", Guid.NewGuid().ToString() },
						{ "name", $"Item {i}" },
						{ "tags", TagTestingValues },
					})
					.ToArray()
			},
		};

		_deepNestedObject = new Dictionary<string, object?>
		{
			{
				"root",
				new Dictionary<string, object?>
				{
					{
						"level3",
						new Dictionary<string, object?>
						{
							{
								"level2",
								new Dictionary<string, object?> { { "level1", "value" } }
							},
						}
					},
				}
			},
		};

		_wideObject = [];
		for (var i = 0; i < 50; i++)
		{
			_wideObject[$"field{i}"] = $"value{i}";
		}

		_nestedArray =
		[
			["a", "b", "c"],
			["d", "e", "f"],
			["g", "h", "i"],
			["j", "k", "l"],
		];
	}

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateDeepNestedObject() =>
		_deepNestedSchema.Validate(_deepNestedObject);

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateWideObject() =>
		_wideObjectSchema.Validate(_wideObject);

	[Benchmark]
	public ValidationResult<string[][]> ValidateNestedArray() => _nestedArraySchema.Validate(_nestedArray);

	[Benchmark]
	public ValidationResult<string> ValidateStringWithManyRefinements() =>
		_refinementStringSchema.Validate("user@example.com");

	[Benchmark]
	public ValidationResult<Dictionary<string, object?>> ValidateLargeObjectWithArrays() =>
		_largeObjectWithArraysSchema.Validate(_largeObjectWithArrays);
}

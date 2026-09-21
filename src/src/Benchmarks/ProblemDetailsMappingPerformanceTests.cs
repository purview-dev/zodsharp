using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Performance and allocation profile for converting a thrown <see cref="ZodException"/> into
/// <see cref="HttpValidationProblemDetails"/>.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ProblemDetailsMappingPerformanceTests
{
	readonly ZodException _unmappedException;
	readonly ZodException _mappedException;
	readonly ErrorTypeRegistry _registry;
	readonly Func<string, ErrorType?> _lookup;

	public ProblemDetailsMappingPerformanceTests()
	{
		_unmappedException = new ZodException([
			ValidationError.Create(
				"too_small",
				"Field 'Items' must contain at least 2 elements.",
				["Items"],
				origin: "array",
				minimum: 2,
				inclusive: true
			),
			ValidationError.Create(
				"too_big",
				"Field 'Order.Lines[3].Quantity' must contain no more than 5 elements.",
				["Order", "Lines", "[3]", "Quantity"],
				origin: "collection",
				maximum: 5,
				inclusive: true
			),
		]);

		_mappedException = new ZodException([
			ValidationError.Create(
				"aggregate_save_failed",
				"The aggregate could not be saved.",
				[],
				parameters: new Dictionary<string, object?>
				{
					["AggregateId"] = "agg-123",
					["AggregateType"] = "Invoice",
				}
			),
		]);

		_registry = new ErrorTypeRegistry();
		_registry.Register(
			new ErrorType(
				Code: "aggregate_save_failed",
				Category: "invalid_value",
				Description: "The aggregate could not be saved.",
				HttpStatus: StatusCodes.Status409Conflict,
				MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save"
			)
			{
				Parameters =
				[
					new ErrorTypeParameter("AggregateId", typeof(string)),
					new ErrorTypeParameter("AggregateType", typeof(string)),
				],
			}
		);

		_lookup = code => _registry.TryGet(code, out var errorType) ? errorType : null;
	}

	[Benchmark]
	public HttpValidationProblemDetails MapWithoutRegistry() => _unmappedException.ToHttpValidationProblemDetails();

	[Benchmark]
	public HttpValidationProblemDetails MapWithRegistryLookup() =>
		_mappedException.ToHttpValidationProblemDetails(_lookup);

	[Benchmark]
	public HttpValidationProblemDetails MapWithRegistryAndFormatting() =>
		_mappedException.ToHttpValidationProblemDetails(_registry);
}

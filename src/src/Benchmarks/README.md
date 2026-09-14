# ZodSharp Performance Benchmarks

BenchmarkDotNet performance suite for ZodSharp. This is a `net10.0` console application; every suite uses `[MemoryDiagnoser]` plus a `[SimpleJob]` profile so time and allocation data are captured together.

## Structure

| File | Covers |
| --- | --- |
| `BasicPerformanceTests.cs` | Primitive validations — string, number, boolean, small string arrays, and multi-rule chains on strings and numbers |
| `ObjectPerformanceTests.cs` | Object validation — a 2-field simple object, a 6-field medium object, a ~13-field complex object with nested data, and an invalid complex object |
| `ArrayPerformanceTests.cs` | Arrays of different sizes — small (10), medium (100) and large (1000) string arrays, number arrays, arrays with complex schemas, and invalid large arrays |
| `HeavyPerformanceTests.cs` | Stress cases — a 4-level deeply nested object, a 50-field wide object, nested jagged arrays, a string with many chained refinements, and a large object containing arrays |
| `MemoryPerformanceTests.cs` | Allocation-focused benchmarks for string, array and object validation (uses more iterations to surface allocation differences) |
| `TransformPerformanceTests.cs` | Transformations — `ToLower`, `ToUpper`, `Trim`, chained transforms, and a transform combined with validation |
| `UnionPerformanceTests.cs` | Union and discriminated-union validation, including invalid inputs |

## How to run

From the repository root:

```bash
# Run the full benchmark suite
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release

# Or via just
just perf-tests
```

Run a single suite with `--filter`:

```bash
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release --filter "*BasicPerformanceTests*"
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release --filter "*MemoryPerformanceTests*"
```

Always use `-c Release`; running benchmarks in Debug produces meaningless numbers.

## Results

Benchmark results are written to the `BenchmarkDotNet.Artifacts` folder in the project directory:

- **HTML reports** — interactive per-benchmark results
- **Markdown reports** — summary tables suitable for commit/PR comments
- **Logs** — detailed execution logs

## Interpreting results

Key metrics reported per benchmark:

1. **Mean** — average execution time
2. **Error / StdDev** — noise and spread of the timings
3. **Gen 0/1/2** — garbage collections per generation per 1000 operations
4. **Allocated** — bytes allocated per operation

Signs to investigate:

- **High time** — look for unnecessary work in hot paths
- **High allocations** — the zero-allocation claim breaks; check for boxing, LINQ, or closure captures in the rule chain
- **Gen 2 collections** — long-lived allocations; a sign of avoidable large-object churn

## Adding a new benchmark suite

1. Add a `*.cs` file under `src/src/Benchmarks/`.
2. Mark the class with `[MemoryDiagnoser]` and a `[SimpleJob]` profile.
3. Build the schema and test data in the constructor (setup runs once per benchmark process).
4. Add one `[Benchmark]` method per scenario, returning the `ValidationResult<T>` (BenchmarkDotNet treats the return value as the "operation" — do not discard it).

```csharp
using BenchmarkDotNet.Attributes;
using ZodSharp;
using ZodSharp.Core;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class MyPerformanceTests
{
    readonly ZodString _schema = Z.String().Min(3).Max(50);

    [Benchmark]
    public ValidationResult<string> ValidateValidValue() => _schema.Validate("user@example.com");
}
```

Run the suite with `--filter "*MyPerformanceTests*"` and confirm the class appears in the `BenchmarkDotNet.Artifacts` output.

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

Run a single suite with `--filter` (note the `--` separator so the filter reaches BenchmarkDotNet, not `dotnet run`):

```bash
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release -- --filter "*BasicPerformanceTests*"
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release -- --filter "*MemoryPerformanceTests*"
```

Always use `-c Release`; running benchmarks in Debug produces meaningless numbers.

## Results

Benchmark results are written to the `BenchmarkDotNet.Artifacts` folder in the project directory:

- **HTML reports** — interactive per-benchmark results
- **Markdown reports** — summary tables suitable for commit/PR comments
- **Logs** — detailed execution logs

The tables below were captured with the committed suite on the following environment (release build, .NET 10):

- BenchmarkDotNet v0.15.8, Windows 11 (10.0.28020.2991)
- 13th Gen Intel Core i9-13900KF 3.00 GHz, 1 CPU, 32 logical / 24 physical cores
- .NET SDK 10.0.401, runtime .NET 10.0.12, X64 RyuJIT x86-64-v3

### Basic

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                          | Mean      | Error     | StdDev    | Allocated |
|-------------------------------- |----------:|----------:|----------:|----------:|
| ValidateString                  | 41.627 ns | 0.8568 ns | 0.2225 ns |         - |
| ValidateNumber                  | 10.702 ns | 0.2730 ns | 0.0709 ns |         - |
| ValidateBoolean                 |  2.170 ns | 0.0314 ns | 0.0082 ns |         - |
| ValidateStringArray             | 57.031 ns | 2.6305 ns | 0.6831 ns |         - |
| ValidateStringWithMultipleRules | 77.313 ns | 0.4016 ns | 0.1043 ns |         - |
| ValidateNumberWithMultipleRules | 16.427 ns | 0.4199 ns | 0.1090 ns |         - |

### Object

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                       | Mean        | Error     | StdDev    | Gen0   | Allocated |
|----------------------------- |------------:|----------:|----------:|-------:|----------:|
| ValidateSimpleObject         |    89.78 ns |  2.797 ns |  0.726 ns |      - |         - |
| ValidateMediumObject         |   340.18 ns | 12.943 ns |  3.361 ns |      - |         - |
| ValidateComplexObject        |   795.69 ns | 51.004 ns | 13.246 ns |      - |         - |
| ValidateComplexObjectInvalid | 1,152.80 ns | 35.102 ns |  9.116 ns | 0.1030 |    1960 B |

### Array

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                              | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------------------ |------------:|----------:|----------:|-------:|----------:|
| ValidateSmallArray                  |    124.4 ns |   1.38 ns |   0.36 ns |      - |         - |
| ValidateMediumArray                 |  4,585.3 ns | 313.30 ns |  81.36 ns |      - |         - |
| ValidateLargeArray                  | 11,682.3 ns | 913.59 ns | 237.26 ns |      - |         - |
| ValidateNumberArray                 | 12,797.3 ns | 716.73 ns | 110.91 ns |      - |         - |
| ValidateLargeArrayWithComplexSchema |  8,011.8 ns | 531.73 ns | 138.09 ns |      - |         - |
| ValidateLargeArrayInvalid           | 11,206.7 ns | 379.31 ns |  98.51 ns | 0.0458 |     904 B |

### Heavy

`IterationCount=3  LaunchCount=1  WarmupCount=2`

| Method                            | Mean        | Error       | StdDev   | Allocated |
|---------------------------------- |------------:|------------:|---------:|----------:|
| ValidateDeepNestedObject          |    198.5 ns |    25.69 ns |  1.41 ns |         - |
| ValidateWideObject                |  2,217.2 ns |   343.53 ns | 18.83 ns |         - |
| ValidateNestedArray               |    177.7 ns |    36.18 ns |  1.98 ns |         - |
| ValidateStringWithManyRefinements |    114.7 ns |    11.33 ns |  0.62 ns |         - |
| ValidateLargeObjectWithArrays     | 19,578.0 ns | 1,223.70 ns | 67.08 ns |         - |

### Transform

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                  | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |---------:|---------:|---------:|-------:|----------:|
| TransformToLower        | 22.26 ns | 1.241 ns | 0.322 ns | 0.0025 |      48 B |
| TransformToUpper        | 28.39 ns | 1.026 ns | 0.267 ns | 0.0025 |      48 B |
| TransformTrim           | 29.10 ns | 0.525 ns | 0.136 ns | 0.0025 |      48 B |
| TransformChained        | 40.71 ns | 0.794 ns | 0.123 ns | 0.0051 |      96 B |
| TransformWithValidation | 78.79 ns | 3.853 ns | 1.001 ns | 0.0058 |     112 B |

### Union

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                                  | Mean      | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------------------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| ValidateUnion_String                    |  48.70 ns | 2.309 ns | 0.600 ns |      - |      - |         - |
| ValidateUnion_Number                    |  69.38 ns | 2.473 ns | 0.383 ns | 0.0314 |      - |     592 B |
| ValidateUnion_Boolean                   |  99.71 ns | 4.742 ns | 1.231 ns | 0.0421 |      - |     792 B |
| ValidateDiscriminatedUnion_FirstOption  | 169.33 ns | 7.262 ns | 1.886 ns |      - |      - |         - |
| ValidateDiscriminatedUnion_SecondOption | 171.90 ns | 3.450 ns | 0.896 ns |      - |      - |         - |
| ValidateUnion_Invalid                   | 195.87 ns | 5.493 ns | 1.427 ns | 0.0722 | 0.0002 |    1360 B |

### Memory

`IterationCount=10  LaunchCount=1  WarmupCount=3`

| Method                        | Mean          | Error         | StdDev       | Ratio    | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |--------------:|--------------:|-------------:|---------:|--------:|----------:|------------:|
| ValidateString_Allocations    |      45.82 ns |      3.871 ns |     2.304 ns |     1.00 |    0.07 |         - |          NA |
| ValidateArray_Allocations     |   1,180.24 ns |     25.234 ns |    15.016 ns |    25.82 |    1.24 |         - |          NA |
| ValidateObject_Allocations    |      94.28 ns |      3.079 ns |     1.832 ns |     2.06 |    0.10 |         - |          NA |
| ValidateString_ManyIterations |  49,215.82 ns | 11,071.853 ns | 7,323.345 ns | 1,076.52 |  160.88 |         - |          NA |
| ValidateArray_ManyIterations  | 108,752.84 ns |  1,231.489 ns |   814.553 ns | 2,378.80 |  111.36 |         - |          NA |
| ValidateObject_ManyIterations |  87,507.56 ns |  1,188.630 ns |   707.334 ns | 1,914.09 |   89.82 |         - |          NA |

> `Gen0`/`Gen1` are GC collections per 1000 operations; `Allocated` is managed memory per operation. "NA" allocations mean zero bytes were measured.
>
> All valid-input paths are **zero-allocation**. The only remaining allocations are on the failure path (error collection) and the string transforms themselves (each `ToLower`/`ToUpper`/`Trim` produces a new string, which is inherent to the transform).

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

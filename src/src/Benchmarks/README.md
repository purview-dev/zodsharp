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

| Method                          | Mean           | Error          | StdDev        | Gen0   | Allocated |
|-------------------------------- |---------------:|---------------:|--------------:|-------:|----------:|
| ValidateString                  |      42.746 ns |      1.8166 ns |     0.4718 ns |      - |         - |
| ValidateNumber                  |      11.585 ns |      0.0628 ns |     0.0163 ns |      - |         - |
| ValidateBoolean                 |       2.904 ns |      0.0372 ns |     0.0097 ns |      - |         - |
| ValidateStringArray             |      91.207 ns |      2.8108 ns |     0.7300 ns | 0.0318 |     600 B |
| ValidateStringWithMultipleRules | 970,250.195 ns | 25,651.4777 ns | 6,661.6073 ns |      - |   15048 B |
| ValidateNumberWithMultipleRules |      57.008 ns |      1.4734 ns |     0.3826 ns | 0.0166 |     312 B |

### Object

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                       | Mean       | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|----------------------------- |-----------:|---------:|---------:|-------:|-------:|----------:|
| ValidateSimpleObject         |   136.3 ns |  2.62 ns |  0.68 ns | 0.0224 |      - |     424 B |
| ValidateMediumObject         |   454.2 ns | 16.88 ns |  2.61 ns | 0.0672 |      - |    1272 B |
| ValidateComplexObject        | 1,067.0 ns | 65.47 ns | 17.00 ns | 0.1545 |      - |    2936 B |
| ValidateComplexObjectInvalid | 1,386.3 ns | 70.29 ns | 18.25 ns | 0.2422 | 0.0019 |    4560 B |

### Array

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                              | Mean         | Error        | StdDev      | Gen0    | Gen1    | Allocated |
|------------------------------------ |-------------:|-------------:|------------:|--------:|--------:|----------:|
| ValidateSmallArray                  |     175.9 ns |      4.13 ns |     1.07 ns |  0.0496 |       - |     936 B |
| ValidateMediumArray                 |   5,087.2 ns |     34.46 ns |     8.95 ns |  0.4272 |  0.0076 |    8136 B |
| ValidateLargeArray                  |  14,841.8 ns |    194.64 ns |    50.55 ns |  4.2419 |  1.0529 |   80136 B |
| ValidateNumberArray                 | 152,157.4 ns |  3,853.20 ns |   596.29 ns | 32.4707 | 30.2734 |  612136 B |
| ValidateLargeArrayWithComplexSchema | 974,079.3 ns | 24,780.85 ns | 3,834.86 ns |       - |       - |   23248 B |
| ValidateLargeArrayInvalid           |  14,911.2 ns |    636.74 ns |    98.54 ns |  4.2877 |  1.0681 |   80888 B |

### Heavy

`IterationCount=3  LaunchCount=1  WarmupCount=2`

| Method                            | Mean         | Error        | StdDev      | Gen0   | Gen1   | Allocated |
|---------------------------------- |-------------:|-------------:|------------:|-------:|-------:|----------:|
| ValidateDeepNestedObject          |     337.3 ns |     27.29 ns |     1.50 ns | 0.0710 | 0.0005 |   1.31 KB |
| ValidateWideObject                |   2,726.7 ns |    221.74 ns |    12.15 ns | 0.2670 | 0.0038 |   4.92 KB |
| ValidateNestedArray               |     321.5 ns |     75.48 ns |     4.14 ns | 0.1040 |      - |   1.91 KB |
| ValidateStringWithManyRefinements | 987,030.7 ns | 67,218.17 ns | 3,684.45 ns |      - |      - |  15.13 KB |
| ValidateLargeObjectWithArrays     |  38,199.8 ns |  9,842.49 ns |   539.50 ns | 6.8970 | 2.0752 | 127.64 KB |

### Transform

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                  | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |----------:|---------:|---------:|-------:|----------:|
| TransformToLower        |  22.24 ns | 0.544 ns | 0.141 ns | 0.0025 |      48 B |
| TransformToUpper        |  23.18 ns | 0.940 ns | 0.244 ns | 0.0025 |      48 B |
| TransformTrim           |  31.34 ns | 1.297 ns | 0.337 ns | 0.0025 |      48 B |
| TransformChained        |  40.45 ns | 1.096 ns | 0.170 ns | 0.0051 |      96 B |
| TransformWithValidation | 144.83 ns | 5.236 ns | 1.360 ns | 0.0288 |     544 B |

### Union

`IterationCount=5  LaunchCount=1  WarmupCount=3`

| Method                                  | Mean      | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------------------------- |----------:|----------:|---------:|-------:|-------:|----------:|
| ValidateUnion_String                    |  57.70 ns |  0.552 ns | 0.085 ns | 0.0033 |      - |      64 B |
| ValidateUnion_Number                    |  72.18 ns |  2.130 ns | 0.330 ns | 0.0314 |      - |     592 B |
| ValidateUnion_Boolean                   | 112.88 ns |  2.550 ns | 0.662 ns | 0.0421 |      - |     792 B |
| ValidateDiscriminatedUnion_FirstOption  | 233.45 ns |  6.170 ns | 1.602 ns | 0.0246 |      - |     464 B |
| ValidateDiscriminatedUnion_SecondOption | 259.09 ns | 13.888 ns | 2.149 ns | 0.0443 |      - |     840 B |
| ValidateUnion_Invalid                   | 201.83 ns |  4.354 ns | 1.131 ns | 0.0722 | 0.0002 |    1360 B |

### Memory

`IterationCount=10  LaunchCount=1  WarmupCount=3`

| Method                        | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------ |--------------:|-------------:|-------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| ValidateString_Allocations    |      42.31 ns |     0.774 ns |     0.512 ns |     1.00 |    0.02 |       - |      - |         - |          NA |
| ValidateArray_Allocations     |   1,512.42 ns |    33.074 ns |    19.682 ns |    35.75 |    0.61 |  0.4807 | 0.0019 |    9048 B |          NA |
| ValidateObject_Allocations    |     145.85 ns |     1.476 ns |     0.878 ns |     3.45 |    0.05 |  0.0350 |      - |     664 B |          NA |
| ValidateString_ManyIterations |  42,098.80 ns |   239.443 ns |   142.488 ns |   995.08 |   12.12 |       - |      - |         - |          NA |
| ValidateArray_ManyIterations  | 147,362.19 ns | 4,961.440 ns | 3,281.686 ns | 3,483.18 |   84.52 | 43.2129 | 1.2207 |  814512 B |          NA |
| ValidateObject_ManyIterations | 135,243.05 ns | 1,037.450 ns |   686.209 ns | 3,196.72 |   40.59 | 22.4609 |      - |  424240 B |          NA |

> `Gen0`/`Gen1` are GC collections per 1000 operations; `Allocated` is managed memory per operation. "NA" allocations mean zero bytes were measured.

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

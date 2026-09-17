# Performance

ZodSharp is designed for maximum performance: validation rules are `readonly record struct`s, hot paths use `Span<T>`, and the source generator emits direct typed codegen with no reflection. The committed BenchmarkDotNet suite measures every scenario.

## Running the benchmarks

```bash
# All suites
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release
# or
just perf-tests

# A specific suite (the `--` passes the filter to BenchmarkDotNet)
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release -- --filter "*ObjectPerformanceTests*"
```

Results are written to `BenchmarkDotNet.Artifacts/` (HTML, Markdown, logs) in the project directory. Use `-c Release`; the suite uses `[MemoryDiagnoser]` and a `[SimpleJob]` profile.

## Measurement environment

- BenchmarkDotNet 0.15.8, .NET 10.0.12, Windows 11 (10.0.28020.2991).
- 13th Gen Intel Core i9-13900KF 3.00 GHz (24 physical / 32 logical cores), X64 RyuJIT x86-64-v3.

Numbers are indicative; re-run on your own hardware for local planning.

## Core validation (`BasicPerformanceTests`)

| Scenario | Mean | Allocated |
|---|---|---|
| ValidateBoolean | 2.170 ns | 0 B |
| ValidateNumber | 10.702 ns | 0 B |
| ValidateString | 41.627 ns | 0 B |
| ValidateStringArray | 57.031 ns | 0 B |
| ValidateStringWithMultipleRules | 77.313 ns | 0 B |
| ValidateNumberWithMultipleRules | 16.427 ns | 0 B |

## Objects (`ObjectPerformanceTests`)

| Scenario | Mean | Allocated |
|---|---|---|
| ValidateSimpleObject (2 fields) | 89.78 ns | 0 B |
| ValidateMediumObject (6 fields) | 340.18 ns | 0 B |
| ValidateComplexObject (13 fields, nested) | 795.69 ns | 0 B |
| ValidateComplexObjectInvalid | 1,152.80 ns | 1,960 B |

## Arrays (`ArrayPerformanceTests`)

| Scenario | Mean | Allocated |
|---|---|---|
| ValidateSmallArray | 124.4 ns | — |
| ValidateLargeArray (1000 items) | 11,682.3 ns | — |
| ValidateMediumArray (100 items) | 4,585.3 ns | — |
| ValidateNumberArray | 12,797.3 ns | — |
| ValidateLargeArrayWithComplexSchema | 8,011.8 ns | — |
| ValidateLargeArrayInvalid | 11,206.7 ns | 904 B |

## Heavy scenarios (`HeavyPerformanceTests`)

| Scenario | Mean |
|---|---|
| ValidateDeepNestedObject (4 levels) | 198.5 ns |
| ValidateWideObject (50 fields) | 2,217.2 ns |
| ValidateNestedArray | 177.7 ns |
| ValidateStringWithManyRefinements | 114.7 ns |
| ValidateLargeObjectWithArrays | 19,578.0 ns |

## Transforms (`TransformPerformanceTests`)

| Scenario | Mean | Allocated |
|---|---|---|
| TransformToLower | 22.26 ns | 48 B |
| TransformToUpper | 28.39 ns | 48 B |
| TransformTrim | 29.10 ns | 48 B |
| TransformChained | 40.71 ns | 96 B |
| TransformWithValidation | 78.79 ns | 112 B |

## Unions (`UnionPerformanceTests`)

| Scenario | Mean | Allocated |
|---|---|---|
| ValidateUnion_String (first option) | 48.70 ns | 0 B |
| ValidateUnion_Number (second option) | 69.38 ns | 592 B |
| ValidateUnion_Boolean (third option) | 99.71 ns | 792 B |
| ValidateDiscriminatedUnion_FirstOption | 169.33 ns | 0 B |
| ValidateDiscriminatedUnion_SecondOption | 171.90 ns | 0 B |
| ValidateUnion_Invalid | 195.87 ns | 1,360 B |

> [!NOTE]
> Union validations allocate on the failure path and while attempting non-matching options — the string option is free, but matching a later option allocates the error collection from the earlier attempts. Discriminated unions dispatch directly and remain zero-allocation.

## Memory (`MemoryPerformanceTests`)

All valid-input paths are zero-allocation:

| Scenario | Mean | Ratio |
|---|---|---|
| ValidateString_Allocations (baseline) | 45.82 ns | 1.00 |
| ValidateObject_Allocations | 94.28 ns | 2.06 |
| ValidateArray_Allocations | 1,180.24 ns | 25.82 |

## UUID validation (`UuidPerformanceTests`)

UUID validation uses a zero-allocation char-scan (version nibble at position 14, variant nibble at position 19) instead of a regex. Measured against the previous compiled regex:

| Scenario | Mean | Allocated |
|---|---|---|
| Rule_CharScan_Valid (`.UUID()`) | 21.99 ns | 0 B |
| Rule_LegacyRegex_Valid (previous implementation) | 27.50 ns | 0 B |
| Rule_CharScan_Invalid | < 1 ns | 0 B |
| Rule_LegacyRegex_Invalid | 14.22 ns | 0 B |
| Rule_CharScan_Nil | 20.67 ns | 0 B |
| Rule_CharScanV7_Valid (`.UUID(UuidVersion.V7)`) | 20.89 ns | 0 B |
| Rule_CharScanV7_Mismatch | 20.11 ns | 0 B |
| Schema_UUID_Valid | 29.80 ns | 0 B |
| Schema_UUIDV7_Valid | 26.64 ns | 0 B |

The char-scan is ~20% faster than the previous regex on the valid path, is version-aware at no extra cost, and rejects wrong-length strings in under a nanosecond.

## Optimizations that make it fast

1. **Struct-based rules** — every rule is a `readonly record struct` implementing `IValidationRule<T>`, so there is no per-validation object allocation.
2. **Zero-allocation helpers** — `Span<T>`/`ReadOnlySpan<T>` string validation (`ValidateSpan`) and `ArrayPool<T>`-backed helpers.
3. **Compiled validators** — `CompiledValidator.Compile` removes interface dispatch (see [Compiled Validators and Caching](Compiled-Validators-and-Caching.md)).
4. **Source generation** — `[ZodSchema]` emits direct property access and typed equality checks with no reflection (see [Source Generator](Source-Generator.md)).
5. **Fluent composition** — schemas are immutable and shareable, so `SchemaCache` avoids repeated construction (see [Compiled Validators and Caching](Compiled-Validators-and-Caching.md)).

The only allocations on a successful validation are the string transforms (`ToLower`/`ToUpper`/`Trim` produce new strings) and the union non-first-option paths noted above.
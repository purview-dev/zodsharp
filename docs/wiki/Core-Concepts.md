# Core Concepts

## The validation pipeline

Every schema derives from `ZodType<TOutput, TInput>` (namespace `ZodSharp.Core`). Validation is a two-phase pipeline:

1. **ParseInternal** — each schema overrides this hook to perform its type check and traversal (rejecting `null` where not allowed, coercing types, walking objects/arrays/tuples/unions, producing structured failures).
2. **Rules** — on success, the accumulated `IValidationRule<TOutput>` structs are evaluated. A failing rule emits a `ValidationError` with code `"validation_failed"`.

```csharp
ValidationResult<TOutput> result = schema.Validate(value);
```

## Validate, SafeParse, Parse, ValidateAsync

| Member | Behaviour |
|---|---|
| `Validate(TInput value)` | Returns a `ValidationResult<TOutput>`. Never throws. |
| `SafeParse(TInput value)` | Alias of `Validate`. |
| `Parse(TInput value)` | Returns the validated `TOutput`, or throws `ZodException` on failure (via `GetValueOrThrow()`). |
| `ValidateAsync(TInput value, CancellationToken)` | `ValueTask` wrapper around `Validate`. The pipeline is synchronous; the token is observed before validation and throws `OperationCanceledException` when already cancelled. Genuinely async work only happens in the source generator's custom async validation. |

## ValidationResult<T>

`ValidationResult<T>` is a `readonly record struct` in `ZodSharp.Core`:

- `bool IsSuccess` — annotated `[MemberNotNullWhen(true, nameof(Value))]`.
- `T? Value` — the validated value; valid only when `IsSuccess`.
- `ImmutableArray<ValidationError> Errors` — populated on failure.

Static factories: `Success(value)`, `Failure(ValidationError)`, `Failure(ImmutableArray<ValidationError>)`, `Failure(IEnumerable<ValidationError>)`, and `Merge(lhs, rhs)` (succeeds only if both succeed; concatenates errors).

## ValidationError

`ValidationError` is a `readonly record struct` carrying machine-readable metadata:

- `Code` — e.g. `"invalid_type"`, `"too_small"`, `"too_big"`, `"invalid_union"`, `"missing_field"`, `"unrecognized_key"`, `"validation_failed"`, `"refinement_failed"`, `"transform_error"`.
- `Message` — human-readable message.
- `Path` — `ImmutableArray<string>`, e.g. `["user", "email"]`; array indexes appear as string segments such as `"0"`.
- `Origin` — category for structured size issues (`"string"`, `"array"`, `"collection"`).
- `Minimum` / `Maximum` — inclusive bounds for size issues.
- `Inclusive` — whether the bound is inclusive.
- `Parameters` — `IReadOnlyDictionary<string, object?>`, e.g. union failures carry every option error under `"errors"`.

Create custom issues with `ValidationError.Create(code, message, path, parameters, origin, minimum, maximum, inclusive)`.

## ZodException

`ZodException` (namespace `ZodSharp.Core`) is thrown by `Parse` and `GetValueOrThrow()`. It exposes `ImmutableArray<ValidationError> Errors`. Its `ToString()` renders one line per error: `"{joinedPath}: {Message} ({Code})"`.

```csharp
try
{
    var value = schema.Parse("AB");
}
catch (ZodException ex)
{
    foreach (var error in ex.Errors)
        Console.WriteLine($"{string.Join(".", error.Path)}: {error.Message}");
}
```

## Schemas and rules

- Schemas are classes deriving from `ZodType<TOutput, TInput>`; the fluent methods return `this` (or a wrapping schema) so chains read naturally.
- Rules are `readonly record struct` implementations of `IValidationRule<T>` (`bool IsValid(in T value)`, `string GetErrorMessage(in T value)`) — zero allocation.
- Custom rules can be attached with the public `AddRule`/`Rule` methods and surfaced as DataAnnotations-style attributes; see [Custom Rules](Custom-Rules.md).
- `ValidateSpan(ReadOnlySpan<char> value)` is available on `ZodString` for span-based validation; use `IsValidSpan(value, out errors)` for an allocation-free check.
- A schema's `Description` is set with `.Describe("...")`.

## Composition model

`ZodType` composes via wrappers rather than mutation. The base type guards that input and output types match, then returns a new schema:

- `Transform<TNew>(Func<TOutput, TNewOutput>)` → `ZodTransform<TOutput, TNewOutput>`.
- `Refine(Func<TOutput, bool>, string? message)` → `ZodRefinement<TOutput>`.
- `SuperRefine(Action<RefineCtx<TOutput>>)` → `ZodSuperRefinement<TOutput>`.
- `Pipe<TTarget>(IZodSchema<TTarget, TOutput>)` → `ZodPipe<TOutput, TTarget>`.
- `Catch(TOutput | Func<TOutput, ImmutableArray<ValidationError>, TOutput>)` → `ZodCatch<TOutput>`.
- `Prefault(TOutput)` → `ZodPrefault<TOutput>`.
- `Default(TOutput)` → `ZodDefault<TOutput>`.
- `And(IZodSchema<TOutput, TOutput>)` → `ZodIntersection<TOutput>`.
- `Or<TOther>(IZodSchema<TOther, TOther>)` → `ZodTypedUnion<TOutput, TOther>`.

See [Composition and Transforms](Composition-and-Transforms.md) for details and semantics.

## Optionality and null handling

Schemas report optionality through the internal `IOptionalSchema` interface:

- `IsOptional` — `true` for `ZodOptional`, `ZodNullable`, `ZodDefault`, `ZodPrefault`.
- `ProvidesValueOnMissing` — `true` for `ZodDefault` and `ZodPrefault`; object fields route missing values through `Validate(null!)` to inject the produced value.
- `IAcceptsNull.ValidateNull()` is implemented by nullable/optional/default/prefault schemas and `ZodNull`; object-field and union wrappers route `null` input to it instead of failing coercion.

## Type coercion

Object fields and union options wrap typed schemas so boxed values from a `Dictionary<string, object?>` can be validated:

- `null` routes to `IAcceptsNull.ValidateNull()` where supported.
- Exact-typed values reuse the original boxed value.
- Numeric coercion uses `IConvertible` (invariant culture), so a boxed `long` can satisfy a `Z.Number()` field.
- Non-nullable value types reject `null`; anything else falls through to failure.

## Dependency injection

`IZodSchemaFactory` is the registry that resolves validators by type. See [Dependency Injection](Dependency-Injection.md).
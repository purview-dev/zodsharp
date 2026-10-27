# Composition and Transforms

Every schema derives from `ZodType<TOutput, TInput>`, so the composition methods below are available on every schema (guarded so they only apply when input equals output). Each method returns a **new** wrapping schema; the original is unchanged.

## Transform

```csharp
var schema = Z.String().Transform(s => s.ToUpperInvariant());
var result = schema.Validate("hello"); // "HELLO"
```

`ZodTransform<TInput, TOutput>` validates the input schema first, then runs the transform. If the transform throws, the exception is caught into a `transform_error` `ValidationError` with category `custom` and the fixed message `"Transform failed."` (`ZodMessages.TransformFailed`). The exception's message is deliberately not reported, because it can disclose internal detail to an HTTP client. `OperationCanceledException`, `OutOfMemoryException` and `StackOverflowException` propagate.

Transforms chain:

```csharp
var schema = Z.String().Transform(s => s.Trim()).Transform(s => s.ToUpperInvariant());
```

`ZodString` ships convenience transforms: `.ToLower()`, `.ToUpper()`, and `.Trim()`.

## Refine

`ZodRefinement<T>` runs the base schema first, then a predicate. A failing predicate produces code `refinement_failed` with category `custom` (custom `message` or `"Custom validation failed"`). The source generator's `ApplyAnd`/`ApplyOr`/`ApplyRefine` report the same code and category.

```csharp
var even = Z.Number().Refine(n => n % 2 == 0, "Must be even");
```

## SuperRefine

`ZodSuperRefinement<T>` takes an `Action<RefineCtx<T>>` and can emit multiple, path-located issues.

```csharp
var password = Z.String().SuperRefine(ctx =>
{
    if (!ctx.Value.Any(char.IsUpper))
        ctx.AddIssue("Must contain an uppercase letter", new[] { "uppercase" });
    if (ctx.Value.Length < 8)
        ctx.AddIssue("too_short", "Must be at least 8 characters", new[] { "length" });
});
```

`RefineCtx<T>` exposes:

- `T Value` - the value being refined.
- `ZodPath FullPath` - the typed base path under which issues are reported; `ImmutableArray<string> Path` is its legacy projection.
- `AddIssue(string code, string message, string[]? path = null)` - appends to the base path. Digit segments (or `"[0]"`) are treated as indexes. Throws `ArgumentException` for a null/whitespace code or message.
- `AddIssue(string code, string message, ZodPath path, IReadOnlyDictionary<string, object?>? parameters = null, string? category = null)` - a typed relative path, optional parameters, and a category (`custom` when omitted).
- `AddIssue(ValidationError issue)` - a fully specified issue (bounds, origin, parameters, message format), re-pathed under the base path with every field kept.
- `AddIssue(string message, string[]? path = null)` - shorthand with code `refinement_failed`.
- `Issues` / `HasIssues`.

Every issue added through the context carries category `custom` unless you supply another. `new RefineCtx<T>(value, ZodPath path)` creates a context directly, for example in a test.

```csharp
var order = Z.String().SuperRefine(ctx =>
{
    ctx.AddIssue("invalid_sku", "Unknown SKU", ZodPath.From(ZodPathSegment.Member("Lines"), ZodPathSegment.At(0)));
    ctx.AddIssue(
        ValidationError.Create("out_of_stock", "Not enough stock", ZodPath.Member("Quantity")) with
        {
            Category = ZodIssueCategories.TooBig,
            Upper = ErrorBound.Including(10),
        }
    );
});
```
## Pipe

`ZodPipe<TSourceOutput, TTargetOutput>` runs the source schema, then validates its output against a target schema.

```csharp
var schema = Z.String().Pipe(Z.String().Min(10));
```

## Catch

`ZodCatch<T>` swallows inner failures and returns a fallback as a **successful** result. The fallback can be a constant or a factory that receives the input and the errors.

```csharp
var withFallback = Z.String().Catch("n/a");
var computed = Z.Number().Catch((value, errors) => 0);
```

## Default

`ZodDefault<T>` substitutes a value when input is `null` and reports `IsOptional = true` and `ProvidesValueOnMissing = true`. The default is **not** re-validated.

```csharp
var schema = Z.String().Default("unknown");
var result = schema.Validate(null); // "unknown"
```

## Prefault

`ZodPrefault<T>` substitutes a value when the input equals `default(T)`, then **still validates** the substituted value through the inner schema.

```csharp
var schema = Z.Number().Prefault(1);
```

## And / Or

- `.And(other)` → `ZodIntersection<T>` — both must succeed; an issue raised identically by both sides is reported once (see [Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md)).
- `.Or<TOther>(other)` → `ZodTypedUnion<T, TOther>` — either may succeed.

## Example: layered validation

```csharp
var schema = Z.String()
    .Min(3)
    .Transform(s => s.Trim())
    .Refine(s => s.StartsWith("PUR-", StringComparison.Ordinal), "Must start with PUR-")
    .Default("PUR-UNKNOWN");
```

## Behavioral differences at a glance

| Wrapper | Trigger | Re-validates substituted value |
|---|---|---|
| `Default(value)` | `null` input | No |
| `Prefault(value)` | input equals `default(T)` | Yes |
| `Catch(value\|factory)` | inner schema fails | No (returns fallback as success) |
| `Refine(predicate)` | predicate returns false | — |
| `SuperRefine(action)` | issues added to context | — |
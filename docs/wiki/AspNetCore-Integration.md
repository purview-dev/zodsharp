# ASP.NET Core Integration

The `Purview.ZodSharp.AspNetCore` package converts failed validation results into standard `ProblemDetails` / `HttpValidationProblemDetails` payloads while preserving the structured validation issues. It also registers Purview.ZodSharp schema resolution into your application's dependency injection container.

## Install

```bash
dotnet add package Purview.ZodSharp.AspNetCore
```

## ProblemDetails

```csharp
using ZodSharp.AspNetCore;

var result = BasketSchema.Validate(basket);

if (!result.IsSuccess)
{
    var problem = result.ToHttpValidationProblemDetails();
    return Results.ValidationProblem(
        problem.Errors,
        extensions: new Dictionary<string, object?>
        {
            ["issues"] = problem.Extensions["issues"],
        });
}
```

| Member | Behaviour |
|---|---|
| `ToHttpValidationProblemDetails<T>(ValidationResult<T> result, int statusCode = 400)` | `HttpValidationProblemDetails`; error paths flattened to dotted keys (`user.email`, array indexes as `[0]`) |
| `ToValidationProblemDetails<T>(ValidationResult<T> result, int statusCode = 400)` | `ValidationProblemDetails` |

Both throw `InvalidOperationException` when the result `IsSuccess`. The structured metadata is preserved in the `issues` extension as a `ValidationIssue[]`:

```csharp
public sealed class ValidationIssue
{
    public required string Code { get; init; }
    public string? Origin { get; init; }
    public int? Minimum { get; init; }
    public int? Maximum { get; init; }
    public bool? Inclusive { get; init; }
    public required string[] Path { get; init; }
    public required string Message { get; init; }
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
}
```

## Exception handling

A thrown `ZodException` (for example from `Parse`, `GetValueOrThrow()`, or a value object's generated
`Create` under strict deserialization) can be mapped to ProblemDetails on demand:

```csharp
using ZodSharp.AspNetCore;

try
{
    var parsed = EmailAddressSchema.Parse(rawValue);
}
catch (ZodException ex)
{
    return Results.ValidationProblem(ex.ToHttpValidationProblemDetails().Errors);
}
```

Or handled automatically by an `IExceptionHandler`. Register it and ensure `UseExceptionHandler()` is in
the pipeline:

```csharp
builder.Services.AddZodSharpProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
```

`AddZodSharpProblemDetails(Action<ZodProblemDetailsOptions>? configure)` registers
`ZodExceptionHandler` and configures `ZodProblemDetailsOptions`:

- `ErrorTypeRegistry Registry` — resolves error codes to `ErrorType`s. Defaults to
  `ErrorTypeRegistry.Default`.
- `bool FormatMessages` — when `true`, messages are formatted from the matched `ErrorType.MessageFormat`.
- `Func<ImmutableArray<ValidationError>, int>? StatusCodeSelector` — an escape hatch that takes complete
  control of the response status code.

## Mapping error types to status codes

Register an `ErrorType` (code, description, HTTP status, and optional message template) in a registry,
then let the mapper derive the status code, title, detail, and formatted messages automatically:

```csharp
using ZodSharp.AspNetCore;

public static partial class ConcurrentErrorType
{
    [ErrorType]
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed",
        Description: "The aggregate could not be saved.",
        HttpStatus: StatusCodes.Status409Conflict,
        MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save",
        Parameters:
        [
            new("AggregateId", typeof(string)),
            new("AggregateType", typeof(string))
        ]);
}

// Register once at startup:
ErrorTypeRegistry.Default.Register(ConcurrentErrorType.SaveFailed);
```

Parameters can also be declared with the `ErrorType.Param<T>("Name")` helper instead of an explicit
`typeof(...)`:

```csharp
Parameters:
[
    new("AggregateId", typeof(string)),
    ErrorType.Param<string>("AggregateType")
]
```

Both the bundled `ZODSASP001` analyzer and the source generator treat `ErrorType.Param<T>` entries
exactly like any other declared parameter, so placeholder checking and helper generation are unchanged.

When an error carries that code, the response status, title, and message are derived automatically:

```csharp
throw new ZodException([
    ValidationError.Create(
        "aggregate_save_failed",
        "The aggregate could not be saved.",
        path: [],
        parameters: new Dictionary<string, object?>
        {
            ["AggregateId"] = "agg-123",
            ["AggregateType"] = "Invoice",
        }),
]);
```

### Generated `Create` / `Throw` helpers

Marking the field with `[ErrorType]` and the containing class `partial` lets the bundled source
generator turn each field into strongly typed static helpers. For the field above it generates
`ConcurrentErrorType.CreateSaveFailed(...)` and `ConcurrentErrorType.ThrowSaveFailed(...)` with one
strongly typed parameter per entry in `Parameters` (the declared `typeof(...)` type, or the
`ErrorType.Param<T>` generic type argument):

```csharp
// Returns a ValidationError with the code, the formatted message, and the typed parameters.
var error = ConcurrentErrorType.CreateSaveFailed("agg-123", "Invoice");

// Throws a ZodException carrying the same ValidationError.
ConcurrentErrorType.ThrowSaveFailed("agg-123", "Invoice");

// The path and structured issue metadata can be populated too:
var error = ConcurrentErrorType.CreateSaveFailed(
    "agg-123",
    "Invoice",
    path: ["order", "items", "[0]"],
    origin: "collection",
    minimum: 1,
    maximum: 10,
    inclusive: true);
```

The generated `Create` builds a typed `ErrorTypeParameters` instance (validated against the declared
parameter types and exposed through `ValidationError.Parameters`) and sets the message from
`ErrorType.FormatMessage`, so `error.Message` already reads
`Aggregate 'agg-123' (of type Invoice) failed to save` and mapping through the registry produces the
`409 Conflict` response described below. The analyzers `ZODSASP001`/`ZODSASP002`/`ZODSASP003`
(bundled with the package) warn when a `MessageFormat` placeholder is not declared in `Parameters`,
when an `[ErrorType]` field's containing class is not `partial`, or when the field is not
`static readonly`.

Produces a `409 Conflict` `HttpValidationProblemDetails` with:

```json
{
  "status": 409,
  "detail": "The aggregate could not be saved.",
  "errors": {
    "": ["Aggregate 'agg-123' (of type Invoice) failed to save"]
  },
  "traceId": "...",
  "aggregateId": "agg-123",
  "issues": [
    {
      "code": "aggregate_save_failed",
      "path": [],
      "message": "Aggregate 'agg-123' (of type Invoice) failed to save",
      "parameters": { "AggregateId": "agg-123", "AggregateType": "Invoice" }
    }
  ]
}
```

Mapping rules:

- **Status** — the highest matched `ErrorType.HttpStatus` wins; unmapped codes fall back to the default
  (`400`). Override with `ZodProblemDetailsOptions.StatusCodeSelector`.
- **Title / Type / Detail** — taken from the highest-status matched `ErrorType`; otherwise defaulted.
- **Message** — `MessageFormat` named placeholders (for example `{AggregateId}`) are substituted from
  `ValidationError.Parameters`. Placeholders without a matching value are left as-is so templating gaps
  stay visible. The analyzer `ZODSASP001` (bundled with the package) warns at compile time when a
  `MessageFormat` placeholder is not declared in `Parameters`.
- **Parameters** — the error's `ValidationError.Parameters` are surfaced both per-issue in the `issues`
  extension and merged (camel-cased) into the top-level ProblemDetails extensions for client correlation.

`ToHttpValidationProblemDetails` / `ToValidationProblemDetails` accept an `ErrorTypeRegistry` or a
`Func<string, ErrorType?>` lookup for on-demand mapping, and the same mapping applies to
`ValidationResult<T>`.

## Dependency injection

```csharp
builder.Services.AddZodSharp(options =>
{
    options.ScanAssemblies.Add(typeof(UserDto).Assembly);
});
```

`AddZodSharp(Action<ZodSchemaFactoryOptions>? configure)` registers `IZodSchemaFactory` as a singleton, applies `options.ConfigureFactory`, and calls `factory.RegisterFromAssembly(assembly)` for each entry in `options.ScanAssemblies`. This auto-discovers source-generated `[assembly: ZodSchemaGenerated(typeof(...))]` registrations.

`ZodSchemaFactoryOptions`:

- `List<Assembly> ScanAssemblies` — assemblies to scan for generated schemas.
- `Action<IZodSchemaFactory>? ConfigureFactory` — additional factory configuration.

See [Dependency Injection](Dependency-Injection.md) for the underlying factory and options-validation wiring.
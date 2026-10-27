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
    public string? Category { get; init; }
    public string? Origin { get; init; }
    public int? Minimum { get; init; }
    public int? Maximum { get; init; }
    public bool? Inclusive { get; init; }
    public required string[] Path { get; init; }
    public required string Message { get; init; }
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
}
```

## Minimal API validation

Use `WithZodSharpValidation<T>()` to validate the request DTO at the endpoint and short-circuit to a standard
validation-problem response, or `ToValidationProblem()` to return a `Results.ValidationProblem` `IResult`
directly from a handler:

```csharp
using Microsoft.AspNetCore.Builder;
using ZodSharp;
using ZodSharp.AspNetCore;

// Validate the bound request DTO automatically (requires AddZodSharp to register the validator):
app.MapPost("/users", (UserDto dto) => dto)
   .WithZodSharpValidation<UserDto>();

// Or validate manually and return a minimal-API IResult:
app.MapPost("/users", (UserDto dto) =>
{
    var result = UserDtoSchema.Validate(dto);
    return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToValidationProblem();
});
```

`WithZodSharpValidation<T>()` adds an endpoint filter that validates the first bound argument of type `T`
against the `IZodSchemaFactory` registered by `AddZodSharp` (or `AddZodSharpFactory`). On failure it returns
the same `HttpValidationProblemDetails` payload produced by `ToHttpValidationProblemDetails()`; on success it
passes through to the handler. `ToValidationProblem()` is the handler-side equivalent, returning a status-400
`Results.ValidationProblem` `IResult`.

## .NET 11: the built-in validation pipeline

On .NET 11, ASP.NET Core validates minimal-API parameters itself through Microsoft.Extensions.Validation.
`AddZodSharpValidation()` (package `Purview.ZodSharp.Extensions.Validation`) makes ZodSharp the first
validation resolver, and on `net11.0` `AddZodSharpProblemDetails()` turns the pipeline's response into the
full ZodSharp contract:

```csharp
builder.Services.AddZodSharp(options => options.ScanAssemblies.Add(typeof(Order).Assembly));
builder.Services.AddZodSharpValidation();
builder.Services.AddValidation();          // when DataAnnotations parameters/types or union bodies are used
builder.Services.AddZodSharpProblemDetails();

var app = builder.Build();

var orders = app.MapGroup("/orders");      // every endpoint is validated - no per-endpoint call
orders.MapPost("/", (Order order, [Range(1, 100)] int page) => TypedResults.Ok());
orders.MapPost("/import", (ImportRequest request) => TypedResults.Ok()).DisableValidation();
```

How it works:

- `AddZodSharpProblemDetails()` registers a scoped `ZodRequestValidationReport`, which is the bridge's
  `IZodValidationReportSink`. The bridge appends every full-fidelity ZodSharp error of the request to it.
- It composes `ProblemDetailsOptions.CustomizeProblemDetails` with a post-configure step. The ZodSharp callback
  runs **first**, then whatever callback the application configured, whether before or after the call or
  through its `problemDetails` argument. The application can therefore still change the result. A later
  `PostConfigure` that replaces the callback outright removes the ZodSharp one.
- It calls `AddProblemDetails()`, without which the callback never runs.

In the callback, for an `HttpValidationProblemDetails`:

| Report | Result |
|---|---|
| ZodSharp errors reported | `ZodProblemDetailsWriter.Apply` rebuilds `errors` and `issues`. ZodSharp keys are replaced and DataAnnotations entries are kept, each with a `dataannotations` issue. `status`, `title`, `type` and `detail` come from the highest `ErrorType`, then `StatusCodeSelector` and `StatusCodeOverride` are applied. The status is mirrored to the response. |
| Empty (DataAnnotations-only failure) | The payload is left untouched, apart from the `issues` projection (`ProjectDataAnnotationsIssues`, default `true`). |

The body is byte-identical (apart from `traceId`) to the one the endpoint filter, `ZodExceptionHandler` and
`ToValidationProblem()` produce.

Notes:

- **Localization**: register `AddLocalization()` and an `IZodMessageFormatter`, for example
  `new StringLocalizerZodMessageFormatter(factory, sharedResourceType: typeof(ValidationMessages))`. Messages
  follow `Accept-Language` (with `UseRequestLocalization`); codes and paths are unchanged.
- **Unions**: a union parameter is claimed by the runtime parameter resolver, which the application's
  `AddValidation()` call adds. The active case is reported under `Value` (`Value.Email`).
- **Double validation**: `WithZodSharpValidation()` on an endpoint the pipeline validates skips the parameters
  the bridge covers, logging that once at `Debug` when the endpoint is built. On a `DisableValidation()` endpoint
  it validates as usual.
- **OpenAPI**: the `Purview.ZodSharp.AspNetCore.OpenApi` operation transformer adds the validation problem
  response for endpoints with a ZodSharp-known parameter. No endpoint metadata is added by the pipeline path.

| | `WithZodSharpValidation()` filter | Built-in pipeline |
|---|---|---|
| Frameworks | net8.0 to net11.0 | net11.0 |
| Scope | endpoints and groups you mark | every endpoint; opt out with `DisableValidation()` |
| DataAnnotations parameters | not validated | validated and merged into one body |
| Binding failures | `OnBindingFailure` | ASP.NET Core's own response |
| Response metadata | adds the 400 response | the OpenAPI transformer |

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

`AddZodSharpProblemDetails(Action<ZodProblemDetailsOptions>? configure = null, Action<ProblemDetailsOptions>? problemDetails = null)`
registers `ZodExceptionHandler`, calls `services.AddProblemDetails(problemDetails)`, and configures
`ZodProblemDetailsOptions`:

- `ErrorTypeRegistry Registry` — resolves error codes to `ErrorType`s. Defaults to
  `ErrorTypeRegistry.Default`.
- `bool FormatMessages` — when `true`, messages are formatted from the matched `ErrorType.MessageFormat`.
- `Func<ImmutableArray<ValidationError>, int>? StatusCodeSelector` — an escape hatch that takes complete
  control of the response status code.

> [!NOTE]
> `AddZodSharpProblemDetails` only wires up exception handling and ProblemDetails services — it does
> **not** register `IZodSchemaFactory`. If you also want DI-based validator resolution, register the
> factory separately with `AddZodSharp` (below) or the core `AddZodSharpFactory`.

## Mapping error types to status codes

The `ErrorType` factory lives in the core `Purview.ZodSharp` package: `ErrorType`, the
`[ErrorType]` attribute, the source generator that emits `Create`/`Throw` helpers, and the
`ZODSASP001`/`ZODSASP002`/`ZODSASP003` analyzers are all shipped with it and are usable without any
ASP.NET Core dependency. The `Purview.ZodSharp.AspNetCore` package adds the `ErrorTypeRegistry`
and the ProblemDetails mapping on top.

Register an `ErrorType` (code, optional category, description, HTTP status, and optional message
template) in a registry, then let the mapper derive the status code, title, detail, and formatted
messages automatically:

```csharp
using ZodSharp.AspNetCore;
using ZodSharp.Core;

public static partial class ConcurrentErrorType
{
    [ErrorType]
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed",
        Category: "invalid_value",
        Description: "The aggregate could not be saved.",
        HttpStatus: 409,
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

`HttpStatus` is kept on `ErrorType` purely for convenience; the core library does not use it — only the
ASP.NET Core integration reads it when building ProblemDetails responses. The optional `Category` is a
broad grouping that can span many specific codes (for example code `invalid_tenant_id` with category
`invalid_value`); the generated helpers copy it onto each `ValidationError`, and it is surfaced on the
serialized `ValidationIssue` in the `issues` extension.

Parameters can also be declared with the `ErrorType.Param<T>("Name")` helper instead of an explicit
`typeof(...)`:

```csharp
Parameters:
[
    new("AggregateId", typeof(string)),
    ErrorType.Param<string>("AggregateType")
]
```

Both the bundled `ZODSASP001` analyzer and the source generator (shipped with the core package)
treat `ErrorType.Param<T>` entries exactly like any other declared parameter, so placeholder checking
and helper generation are unchanged.

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

Marking the field with `[ErrorType]` and the containing class `partial` lets the source generator
bundled with the core package turn each field into strongly typed static helpers. For the field above
it generates `ConcurrentErrorType.CreateSaveFailed(...)` and
`ConcurrentErrorType.ThrowSaveFailed(...)` with one strongly typed parameter per entry in
`Parameters` (the declared `typeof(...)` type, or the `ErrorType.Param<T>` generic type argument):

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
`409 Conflict` response described below.

### ErrorType diagnostics

The `ErrorType` factory, its source generator, and its analyzers ship with the core `Purview.ZodSharp` package:

| ID | Severity | Meaning |
|---|---|---|
| `ZODSASP001` | Warning | A `MessageFormat` placeholder is not declared in `ErrorType.Parameters` |
| `ZODSASP002` | Warning | The containing type of an `[ErrorType]` field is not declared `partial` |
| `ZODSASP003` | Warning | An `[ErrorType]` field is not declared `static readonly` |
| `ZODSASP100` | Error | Unhandled exception in the `ErrorType` source generator |
| `ZODSASP101` | Error | The `Parameters` of an `[ErrorType]` field could not be extracted |

`ZODSASP001`–`ZODSASP003` explain why `Create`/`Throw` helpers were not generated; `ZODSASP100`/`ZODSASP101` are fatal generator failures that name the field they failed on.

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
  stay visible. The analyzer `ZODSASP001` (shipped with the core package) warns at compile time when a
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
    options.ScanAssemblyGraphs.Add(typeof(Program).Assembly);
    options.ScanLoadedAssemblies = true;
});
```

`AddZodSharp(Action<ZodSchemaFactoryOptions>? configure)` registers `IZodSchemaFactory` as a singleton, applies `options.ConfigureFactory`, and calls `factory.RegisterFromAssembly(assembly)` for each distinct assembly discovered from the configured assembly sources. This auto-discovers source-generated `[assembly: ZodSchemaGenerated(typeof(...))]` registrations.

`ZodSchemaFactoryOptions`:

- `List<Assembly> ScanAssemblies` — assemblies to scan for generated schemas.
- `List<Assembly> ScanAssemblyGraphs` — root assemblies whose referenced assembly graphs should be scanned for generated schemas.
- `bool ScanLoadedAssemblies` — whether to scan all currently loaded assemblies for generated schemas.
- `Action<IZodSchemaFactory>? ConfigureFactory` — additional factory configuration.

For modular registration, the package also exposes additive assembly-contribution helpers:

```csharp
builder.Services.AddZodSharp();
builder.Services.AddZodSharpAssembly(typeof(UserDto).Assembly);
builder.Services.AddZodSharpAssemblyGraph(typeof(Program).Assembly);
builder.Services.AddZodSharpLoadedAssemblies();
```

- `AddZodSharpAssembly(Assembly)` contributes an exact assembly scan.
- `AddZodSharpAssemblyGraph(Assembly)` contributes the root assembly plus its referenced assemblies.
- `AddZodSharpLoadedAssemblies()` contributes a scan across assemblies currently loaded into the application domain.

> [!IMPORTANT]
> The factory is registered only if one is not already present (`TryAdd` semantics): the **first**
> `AddZodSharp` (or `AddZodSharpFactory`) call wins, and any later calls — including their
> `ScanAssemblies`/`ScanAssemblyGraphs`/`ScanLoadedAssemblies`/`ConfigureFactory` settings — are ignored.
> State is never overwritten, so calling it more than once is safe. The additive assembly-contribution
> helpers above still contribute before the service provider is built.

### Choosing a registration method

| Method | Package | Registers | Use when |
|---|---|---|---|
| `AddZodSharp(options)` | `Purview.ZodSharp.AspNetCore` | singleton `IZodSchemaFactory` + auto-registers generated validators from configured assembly sources | ASP.NET Core apps that want factory registration plus optional assembly-source configuration. |
| `AddZodSharpAssembly(...)`, `AddZodSharpAssemblyGraph(...)`, `AddZodSharpLoadedAssemblies()` | `Purview.ZodSharp.AspNetCore` | additive generated-validator assembly source contributions | Modular ASP.NET Core apps where deeper layers or implementation packages contribute their own schema assemblies. |
| `AddZodSharpFactory(configure)` | core `Purview.ZodSharp` | singleton `IZodSchemaFactory` | Any .NET host where you want manual control — register validators yourself in the `configure` callback. |
| `AddZodSharpProblemDetails(...)` | `Purview.ZodSharp.AspNetCore` | `ZodExceptionHandler` + ProblemDetails services only — does **not** register the factory | Mapping thrown `ZodException`s to `ProblemDetails`; pair it with `AddZodSharp` when you also need DI validator resolution. |
| `AddZodSchemaOptionsValidator<T>(...)` | core `Purview.ZodSharp` | singleton `IValidateOptions<T>` | Validating options objects; requires a factory registered first via `AddZodSharp` or `AddZodSharpFactory`. |

See [Dependency Injection](Dependency-Injection.md) for the underlying factory and options-validation wiring.

# Purview.ZodSharp.AspNetCore

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.AspNetCore.svg)](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

ASP.NET Core integration for [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp). Convert failed validation results into standard `ProblemDetails` / `HttpValidationProblemDetails` payloads while preserving the structured validation issues.

## Installation

```bash
dotnet add package Purview.ZodSharp.AspNetCore
```

## Usage

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

`ToHttpValidationProblemDetails()` maps each `ValidationError` to a standard `HttpValidationProblemDetails.Errors` entry keyed by its JSON path. The structured `ValidationIssue` metadata (code, origin, minimum/maximum, inclusive bounds, path, message) is preserved in the `issues` extension so clients get more than a flat message list.

A `ValidationProblemDetails` overload is also available:

```csharp
var problem = result.ToValidationProblemDetails();
```

## Exception handling

Thrown `ZodException`s (e.g. from a value object's strict deserialization) are converted automatically by
an `IExceptionHandler`:

```csharp
builder.Services.AddZodSharpProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
```

## Dependency injection

`AddZodSharp` registers `IZodSchemaFactory` as a singleton and auto-registers every source-generated
validator from the configured assemblies:

```csharp
builder.Services.AddZodSharp(options =>
{
    options.ScanAssemblies.Add(typeof(UserDto).Assembly);
    options.ScanAssemblyGraphs.Add(typeof(Program).Assembly);
    options.ScanLoadedAssemblies = true;
});
```

The factory is registered only if one is not already present — the first `AddZodSharp` (or
`AddZodSharpFactory`) call wins and later `AddZodSharp` configure callbacks are ignored, so state is
never overwritten.

For modular apps, assembly contributions can also be added independently before building the provider:

```csharp
builder.Services.AddZodSharp();
builder.Services.AddZodSharpAssembly(typeof(UserDto).Assembly);
builder.Services.AddZodSharpAssemblyGraph(typeof(Program).Assembly);
builder.Services.AddZodSharpLoadedAssemblies();
```

- `ScanAssemblies` / `AddZodSharpAssembly(...)` scan exact assemblies.
- `ScanAssemblyGraphs` / `AddZodSharpAssemblyGraph(...)` scan the root assembly plus its referenced assemblies.
- `ScanLoadedAssemblies` / `AddZodSharpLoadedAssemblies()` scan the assemblies currently loaded into the application domain.
- Exact, graph, and loaded-assembly contributions are additive before the service provider is built.

When to use which registration:

- `AddZodSharp` (this package) — ASP.NET Core apps; registers the factory and can auto-discover generated validators.
- `AddZodSharpAssembly(...)` / `AddZodSharpAssemblyGraph(...)` / `AddZodSharpLoadedAssemblies()` (this package) — modular ASP.NET Core apps; contribute generated-validator assembly sources additively.
- `AddZodSharpFactory` (core package) — any .NET host; you register validators manually in the `configure` callback.
- `AddZodSharpProblemDetails` (this package) — exception handling and ProblemDetails services only; it does **not** register the factory.
- `AddZodSchemaOptionsValidator<T>` (core package) — options validation via `IValidateOptions<T>`; requires a factory registered first.

## Mapping error types to status codes

The `ErrorType` factory (`ErrorType`, the `[ErrorType]` attribute, its source generator, and the
`ZODSASP001`/`ZODSASP002`/`ZODSASP003` analyzers) is part of the core `Purview.ZodSharp` package.
This package adds the `ErrorTypeRegistry` and maps error codes to HTTP statuses and formatted messages:

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

ErrorTypeRegistry.Default.Register(ConcurrentErrorType.SaveFailed);
```

`HttpStatus` is stored on `ErrorType` purely for convenience — only this ASP.NET Core integration reads
it when deriving the ProblemDetails response status. The optional `Category` is a broad grouping that
can span many specific codes; the generated helpers copy it onto each `ValidationError` and it is
surfaced on the serialized `ValidationIssue` in the `issues` extension.

Parameters can also be declared with the `ErrorType.Param<T>("Name")` helper instead of an explicit
`typeof(...)`; the analyzer and source generator handle both forms the same way:

```csharp
Parameters:
[
    new("AggregateId", typeof(string)),
    ErrorType.Param<string>("AggregateType")
]
```

A `ValidationError` carrying `parameters` such as `["AggregateId"] = "agg-123"` is then surfaced as a
`409 Conflict` response whose message reads `Aggregate 'agg-123' (of type Invoice) failed to save`. The
analyzer `ZODSASP001` (shipped with the core package) warns when a `MessageFormat` placeholder is not
declared in `Parameters`.

### Generated `Create` / `Throw` helpers

Because the class above is `partial`, the source generator bundled with the core package adds strongly
typed helpers derived from the declared `Parameters` — each parameter is emitted with its declared
`typeof(...)` type, or the `ErrorType.Param<T>` generic type argument:

```csharp
// ValidationError with the code, the formatted message, and the typed parameters:
var error = ConcurrentErrorType.CreateSaveFailed("agg-123", "Invoice");

// ZodException carrying that ValidationError:
ConcurrentErrorType.ThrowSaveFailed("agg-123", "Invoice");

// Path and structured issue metadata can be populated too:
var error = ConcurrentErrorType.CreateSaveFailed(
    "agg-123",
    "Invoice",
    path: ["order", "items", "[0]"],
    origin: "collection",
    minimum: 1,
    maximum: 10,
    inclusive: true);
```

The generated helpers construct a typed `ErrorTypeParameters` instance (exposed through
`ValidationError.Parameters`) whose values are validated against the declared types and can be read back
through `Get<T>(name)`.

The analyzer `ZODSASP002` (shipped with the core package) warns when an `ErrorType` field's containing
class is not declared `partial`.

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.AspNetCore` on NuGet.

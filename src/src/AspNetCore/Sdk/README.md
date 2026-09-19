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

## Mapping error types to status codes

Register an `ErrorType` and map error codes to HTTP statuses and formatted messages:

```csharp
public static partial class ConcurrentErrorType
{
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed",
        Description: "The aggregate could not be saved.",
        HttpStatus: StatusCodes.Status409Conflict,
        MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save")
    {
        Parameters = ["AggregateId", "AggregateType"]
    };
}

ErrorTypeRegistry.Default.Register(ConcurrentErrorType.SaveFailed);
```

A `ValidationError` carrying `parameters` such as `["AggregateId"] = "agg-123"` is then surfaced as a
`409 Conflict` response whose message reads `Aggregate 'agg-123' (of type Invoice) failed to save`. The
analyzer `ZODSASP001` (bundled with the package) warns when a `MessageFormat` placeholder is not declared
in `Parameters`.

### Generated `Create` / `Throw` helpers

Because the class above is `partial`, the bundled source generator adds strongly typed helpers derived
from the declared `Parameters`:

```csharp
// ValidationError with the code, the formatted message, and the named parameters:
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

The analyzer `ZODSASP002` warns when an `ErrorType` field's containing class is not declared `partial`.

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.AspNetCore` on NuGet.

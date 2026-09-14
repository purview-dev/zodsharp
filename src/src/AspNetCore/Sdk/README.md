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

## Further reading

For the full API, cross-platform TypeScript/Zod interop, and performance notes see the [repository README](https://github.com/purview-dev/zodsharp/blob/main/README.md).

## License

MIT — see the package metadata in `Purview.ZodSharp.AspNetCore` on NuGet.

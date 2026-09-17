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
}
```

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
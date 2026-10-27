---
name: purview-zodsharp-aspnetcore
description: >-
  Use when wiring Purview.ZodSharp into ASP.NET Core or any DI host - ProblemDetails from a
  ValidationResult or a thrown ZodException, minimal-API endpoint and route-group validation with
  WithZodSharpValidation, ZodTypedResults, the shared ZodProblemDetailsWriter, registering
  IZodSchemaFactory with AddZodSharp or AddZodSharpFactory, validating options objects (including .NET 11
  async startup validation), and mapping error codes to HTTP statuses with ErrorType.
license: MIT
metadata:
  product: Purview.ZodSharp.AspNetCore
  repository: purview-dev/zodsharp
---

# ASP.NET Core and dependency injection

`Purview.ZodSharp.AspNetCore` turns failed validation into standard `HttpValidationProblemDetails`
payloads **without losing the structured issues**, validates minimal-API parameters, and registers schema
resolution into DI.

```bash
dotnet add package Purview.ZodSharp.AspNetCore
```

Namespaces: the `To…ProblemDetails`/`ToValidationProblem` extensions are in **`ZodSharp`**; the DI
extensions in `Microsoft.Extensions.DependencyInjection`; `WithZodSharpValidation` in
`Microsoft.AspNetCore.Builder`; the types (`ZodProblemDetailsWriter`, `ZodProblemDetailsOptions`,
`ValidationIssue`, `ZodTypedResults`, `ErrorTypeRegistry`) in `ZodSharp.AspNetCore`.

## Setup

```csharp
builder.Services.AddZodSharp(o => o.ScanAssemblies.Add(typeof(Order).Assembly));
builder.Services.AddZodSharpProblemDetails(o =>
{
    o.PathNaming = ZodPathNaming.MemberName;   // default; Json applies the JsonOptions naming policy
});

var app = builder.Build();
app.UseExceptionHandler();                     // lets ZodExceptionHandler map a thrown ZodException

var orders = app.MapGroup("/orders").WithZodSharpValidation();   // every endpoint in the group
orders.MapPost("/", (Order order) => TypedResults.Created($"/orders/{order.Id}", order));
```

`AddZodSharpProblemDetails(configure?, problemDetails?)` calls `AddProblemDetails`, registers
`ZodExceptionHandler` and the shared `ZodProblemDetailsWriter`, and adds source-generated JSON metadata to
the minimal-API `JsonOptions` (Native AOT safe). It does **not** register `IZodSchemaFactory` - pair it
with `AddZodSharp`/`AddZodSharpFactory`.

## One contract on every path

The endpoint filter, `ZodExceptionHandler`, `ZodTypedResults` and every extension method delegate to
`ZodProblemDetailsWriter`, so they produce the **same body**:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Email": ["Email must be a valid email address"], "Items[0].Quantity": ["Quantity must be at least 1"] },
  "issues": [
    { "path": "Items[0].Quantity", "segments": ["Items", 0, "Quantity"], "code": "too_small",
      "category": "too_small", "origin": "number", "minimum": 1, "inclusive": true,
      "message": "Quantity must be at least 1" }
  ],
  "traceId": "00-…"
}
```

- **Keys** are `ValidationError.FullPath.ToDottedString()`: `Items[0].Name`, `Map[en]`, `Map["a.b"]`;
  the root is `""`. C# member names by default, matching Microsoft.Extensions.Validation and MVC.
- **`issues[]`** is a `ValidationIssue` (a `readonly record struct`): `path` (dotted), `segments`
  (strings and integer indexes, Zod's shape), `code`, `category`, `origin`, typed `minimum`/`maximum`
  (numbers, or ISO-8601 for dates), `inclusive`, `message`, `parameters` (camel-cased, safe scalars only)
  and `unionErrors` (nested issues per union option, paths relative to the union). In-process code can
  still read the CLR `Path` (string array), `Minimum`, `Maximum` and raw `Parameters` (`ErrorTypeParameters`).
- **`traceId`** is `Activity.Current?.Id`, else `HttpContext.TraceIdentifier`.
- Bodies go through `IProblemDetailsService`, so `ProblemDetailsOptions.CustomizeProblemDetails` runs.

## Minimal APIs

```csharp
app.MapPost("/users", (UserDto dto, [FromServices] IClock clock) => …)
   .WithZodSharpValidation(o => o.ThrowIfNoValidator = true);

app.MapPost("/legacy", (UserDto dto) => …).WithZodSharpValidation<UserDto>();   // only T
```

`WithZodSharpValidation()` works on any `IEndpointConventionBuilder` (endpoint or `RouteGroupBuilder`). An
endpoint filter **factory** picks the parameters once, when the endpoint is built:

- Skipped: services (`[FromServices]`, keyed services, anything the container resolves), `HttpContext`,
  `HttpRequest`, `HttpResponse`, `ClaimsPrincipal`, `CancellationToken`, form files, streams and
  `[SkipValidation]`. `[FromBody]`/`[FromRoute]`/`[FromQuery]`/`[FromHeader]`/`[FromForm]` force a parameter in.
- Validators resolve through `IZodSchemaFactory.TryGetUntypedValidator(Type)`; **every** selected argument
  is validated with `ValidateAsync(value, RequestServices, RequestAborted)`.
- Error keys follow Microsoft.Extensions.Validation: complex parameter unprefixed (`Name`), sequence
  parameter prefixed (`items[0].Name`), an error about the value itself keyed by the parameter name.
- `ThrowIfNoValidator` (or the `<T>` form) **fails when the endpoint is built**, naming the handler and
  parameter, instead of a 500 per request.
- **.NET 11** runs filters after a route/query/header/form binding failure (status already 400):
  `OnBindingFailure = PassThrough` (default) keeps ASP.NET Core's response; `ConvertToProblemDetails`
  writes the ZodSharp contract. A malformed JSON body never reaches filters.
- Adds 400 `application/problem+json` response metadata (`ProducesStatusCode`), so OpenAPI shows it.

## Manual results and exceptions

```csharp
app.MapPost("/users", Results<Ok<UserDto>, ValidationProblem> (UserDto dto, HttpContext ctx) =>
{
    var result = UserDtoSchema.Validate(dto);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : ZodTypedResults.ValidationProblem(result, ctx);
});
```

| Member | Produces |
| --- | --- |
| `ZodTypedResults.ValidationProblem(result \| exception, ctx)` | ASP.NET's `ValidationProblem` (always 400) |
| `ZodTypedResults.Problem(result \| exception, ctx)` | `ZodProblemDetailsResult` with the computed status |
| `result.ToValidationProblem(ctx)` / `exception.ToValidationProblem(ctx)` | the same, as `IResult` |
| `ToHttpValidationProblemDetails(ctx)` / `(ZodProblemDetailsOptions, ctx?)` | `HttpValidationProblemDetails` |
| `ToHttpValidationProblemDetails(statusCode = 400)` / `(registry, …)` / `(lookup, …)` | 2.x overloads, no DI |
| `ToValidationProblemDetails(…)` | MVC `ValidationProblemDetails`, every extension copied |

The result-based members **throw `InvalidOperationException` for a successful result**. A `ZodException`
thrown from `Parse` inside a handler is mapped by `ZodExceptionHandler` to the identical body.

## The writer and its options

```csharp
var writer = ZodProblemDetailsWriter.GetOrCreate(httpContext.RequestServices);
HttpValidationProblemDetails problem = writer.Create(errors, httpContext);
writer.Apply(existingProblem, errors, httpContext);   // keeps non-ZodSharp `errors` entries
await writer.WriteAsync(httpContext, problem);
```

`ZodProblemDetailsOptions`:

- `Registry` (`ErrorTypeRegistry.Default`) or `ErrorTypeResolver` - code to `ErrorType`.
- `FormatMessages` (`true`) - use the matched `ErrorType.MessageFormat`, **only when every placeholder
  resolves** (from parameters or built-ins `path`, `code`, `origin`, `minimum`, `maximum`, `displayName`).
- `PathNaming`, `IncludeIssues`, `DefaultStatusCode` (400).
- `MergeParametersIntoExtensions` (`true`) - also copy parameters to the top level. Reserved names
  (`type`, `title`, `status`, `detail`, `instance`, `errors`, `issues`, `traceId`) become `param_{name}`.
- `ProjectDataAnnotationsIssues` (`true`) - `Apply` gives foreign entries a `dataannotations` issue.
- `StatusCodeSelector(errors)` then `StatusCodeOverride(errors, computed)` - run last and win.

Messages render through a registered `IZodMessageFormatter` (core) in `CultureInfo.CurrentUICulture`;
without one they are used as produced. `DefaultZodMessageFormatter` re-formats `MessageFormat` in a culture.

## Error codes to HTTP statuses

`ErrorType`, `[ErrorType]` and the `ZODSASP*` analyzers ship in the **core** package; this package adds
`ErrorTypeRegistry` and the mapping.

```csharp
public static partial class ConcurrentErrorType
{
    [ErrorType]
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed", Category: "invalid_value", HttpStatus: 409,
        Description: "The aggregate could not be saved.",
        MessageFormat: "Aggregate '{AggregateId}' failed to save",
        Parameters: [ErrorType.Param<string>("AggregateId")]);
}

builder.Services.AddZodSharpProblemDetails(o => o.MapErrorType(ConcurrentErrorType.SaveFailed));
var error = ConcurrentErrorType.CreateSaveFailed("agg-123");   // or ThrowSaveFailed(...)
```

- **Status** - the highest matched `HttpStatus`, else 400; the selector/override run after and win.
- **Title / Type / Detail** - from that `ErrorType`, else the RFC 9110 defaults.
- **Parameters** - per issue in `issues[].parameters`; values leave the process, so never put internal
  context in them.

## Registering the factory

| Method | Package | Registers |
| --- | --- | --- |
| `AddZodSharp(options)` | AspNetCore | factory + generated validators from `ScanAssemblies`, `ScanAssemblyGraphs`, `ScanLoadedAssemblies` |
| `AddZodSharpAssembly/AssemblyGraph/LoadedAssemblies` | AspNetCore | additive assembly sources |
| `AddZodSharpFactory(configure)` | core | the factory only |
| `AddZodSharpProblemDetails(…)` | AspNetCore | writer, exception handler, ProblemDetails - not the factory |

The **first** `AddZodSharp`/`AddZodSharpFactory` call wins; later calls are ignored. A hand-built schema
registers with `factory.Register(new ZodSchemaValidator<T>(schema))`. Native AOT apps register validators
explicitly rather than scanning assembly graphs.

## Options validation

```csharp
builder.Services.AddOptions<SmtpOptions>().BindAndValidateWithZod("Smtp");   // binds + ValidateOnStart

// Native AOT: bind yourself so the configuration binder generator intercepts it.
builder.Services.AddOptions<SmtpOptions>()
    .BindConfiguration("Smtp")
    .AddZodSchemaValidator(MissingValidatorBehavior.Throw, validateOnStart: true, configurationSection: "Smtp");
```

- Failures read `{name}: {location}: {message} ({code})` - `Smtp:Servers:0:Host: Host is required
  (required)` with a section, `Servers[0].Host: …` without; the name is omitted for the default options.
- `ZodValidateOptionsResult` carries the full `Errors`.
- **.NET 11:** `ZodSchemaOptionsValidator<T>` also implements `IAsyncValidateOptions<T>` and registers via
  `OptionsBuilder<T>.Validate<TValidator>()`. `ValidateOnStart()` runs async and service-aware rules (in a
  scope); synchronous access (`IOptions<T>.Value`, reloads) checks only the synchronous rules.
- `OptionsBuilder<T>.AddZodSchemaValidator()` validates only that builder's options name;
  `services.AddZodSchemaOptionsValidator<T>()` validates every name.
- `MissingValidatorBehavior`: `Throw` (default) or `Ignore`.

## Related skills

`purview-zodsharp-core` for the runtime API and error model, `purview-zodsharp-source-generator` for the
validators this resolves, and `purview-zodsharp-diagnostics` for `ZODSASP*`.

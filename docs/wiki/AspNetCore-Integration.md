# ASP.NET Core Integration

The `Purview.ZodSharp.AspNetCore` package converts failed validation results into standard `ProblemDetails` / `HttpValidationProblemDetails` payloads while preserving the structured validation issues. It also registers Purview.ZodSharp schema resolution into your application's dependency injection container.

## Install

```bash
dotnet add package Purview.ZodSharp.AspNetCore
```

## ProblemDetails

Every path that turns ZodSharp errors into ProblemDetails delegates to one writer, `ZodProblemDetailsWriter`:
the endpoint filter, `ZodExceptionHandler`, `ZodTypedResults`, the .NET 11 pipeline enrichment, and every
`ToHttpValidationProblemDetails`/`ToValidationProblemDetails`/`ToValidationProblem` overload. They all produce the
same body (see [The error contract](#the-error-contract)).

```csharp
using ZodSharp;

app.MapPost("/baskets", (Basket basket, HttpContext httpContext) =>
{
    var result = BasketSchema.Validate(basket);
    return result.IsSuccess ? Results.Ok(result.Value) : result.ToValidationProblem(httpContext);
});
```

The extensions are in the `ZodSharp` namespace and exist for both `ValidationResult<T>` and `ZodException`:

| Member | Behaviour |
|---|---|
| `ToHttpValidationProblemDetails(int statusCode = 400)` | `HttpValidationProblemDetails` with default options, without resolving `ErrorType`s |
| `ToHttpValidationProblemDetails(ErrorTypeRegistry registry, int statusCode = 400)` | Resolves `ErrorType`s from `registry` and formats their messages |
| `ToHttpValidationProblemDetails(Func<string, ErrorType?> lookup, int statusCode = 400)` | Resolves `ErrorType`s through `lookup` |
| `ToHttpValidationProblemDetails(ZodProblemDetailsOptions options, HttpContext? httpContext = null)` | Uses the supplied options |
| `ToHttpValidationProblemDetails(HttpContext httpContext)` | Uses the writer, and therefore the options, registered in the request services (`ValidationResult<T>` only) |
| `ToValidationProblemDetails(...)` | The same overloads, returning MVC `ValidationProblemDetails` with every extension copied |
| `ToValidationProblem()` | An `IResult` with default options (`ValidationResult<T>` only) |
| `ToValidationProblem(HttpContext httpContext)` | An `IResult` written by the registered writer, keeping the computed status (prefer this) |

The `ValidationResult<T>` overloads throw `InvalidOperationException` when the result `IsSuccess`.

### Typed results

`ZodTypedResults` produces the same body for handlers that declare their results:

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using ZodSharp.AspNetCore;

app.MapPost("/orders", Results<Ok<Order>, ValidationProblem> (Order order, HttpContext httpContext) =>
{
    var result = OrderSchema.Validate(order);
    return result.IsSuccess
        ? TypedResults.Ok(result.Value)
        : ZodTypedResults.ValidationProblem(result, httpContext);
});
```

| Member | Returns |
|---|---|
| `ZodTypedResults.ValidationProblem(result, httpContext)` / `ValidationProblem(exception, httpContext)` | ASP.NET Core's `ValidationProblem`, for `Results<...>` unions. It is always `400`, even when a mapped `ErrorType` yields another status. |
| `ZodTypedResults.Problem(result, httpContext)` / `Problem(exception, httpContext)` | `ZodProblemDetailsResult`, which keeps the computed status (for example a mapped `409`) and writes through `IProblemDetailsService`. |

### `ValidationIssue`

The `issues` extension is a `ValidationIssue[]`. `ValidationIssue` is a `readonly record struct` in
`ZodSharp.AspNetCore`, and a safe projection of `ValidationError`:

| JSON property | CLR member | Value |
|---|---|---|
| `path` | `DottedPath` | The `errors` key (`Items[0].Quantity`); empty for the root |
| `segments` | `Segments` | Zod's path shape: strings for members and keys, integers for indexes |
| `code` | `Code` | The ZodSharp code |
| `category` | `Category` | The Zod issue code for built-in errors (`too_small`, `invalid_type`, ...) |
| `origin` | `Origin` | `string`, `number`, `array`, `date`, ... |
| `minimum`/`maximum` | `Lower`/`Upper` | From the error's typed bounds: JSON numbers, or ISO-8601 strings for dates |
| `inclusive` | `Inclusive` | Whether the bound is inclusive |
| `message` | `Message` | The message, rendered by the registered `IZodMessageFormatter` |
| `parameters` | `SafeParameters` | Camel-cased, restricted to invariant scalars and arrays of them |
| `unionErrors` | `UnionErrors` | For a failed union, each option's issues, with paths relative to the union |

The CLR-only members `Path` (`string[]`), `Minimum`, `Maximum` (`int?`) and `Parameters` (the raw
`ErrorTypeParameters`) keep their 2.x meaning for in-process code and are not serialized.

## The error contract

A request with a short `CustomerName`, an empty `Lines[0].Sku`, and out-of-range quantities in `Lines[0]` and
`ByCode["en"]` produces this body. It is real output from the .NET 11 pipeline with `AddZodSharpProblemDetails()`.
The `WithZodSharpValidation()` filter produces the same body.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "CustomerName": ["Field 'CustomerName' must contain at least 3 characters."],
    "Lines[0].Sku": ["Sku is required"],
    "Lines[0].Quantity": ["Field 'Quantity' must be greater than or equal to 1 and less than or equal to 100."],
    "ByCode[en].Quantity": ["Field 'Quantity' must be greater than or equal to 1 and less than or equal to 100."]
  },
  "issues": [
    {
      "path": "CustomerName",
      "segments": ["CustomerName"],
      "code": "too_small",
      "category": "too_small",
      "origin": "string",
      "minimum": 3,
      "inclusive": true,
      "message": "Field 'CustomerName' must contain at least 3 characters.",
      "parameters": { "count": 2, "displayName": "CustomerName", "declaringType": "Order", "member": "CustomerName" }
    },
    {
      "path": "Lines[0].Sku",
      "segments": ["Lines", 0, "Sku"],
      "code": "missing_field",
      "category": "invalid_type",
      "message": "Sku is required",
      "parameters": { "displayName": "Sku", "declaringType": "OrderLine", "member": "Sku" }
    },
    {
      "path": "Lines[0].Quantity",
      "segments": ["Lines", 0, "Quantity"],
      "code": "invalid_range",
      "category": "too_small",
      "origin": "number",
      "minimum": 1,
      "maximum": 100,
      "inclusive": true,
      "message": "Field 'Quantity' must be greater than or equal to 1 and less than or equal to 100.",
      "parameters": { "minimum": "1", "maximum": "100", "displayName": "Quantity", "declaringType": "OrderLine", "member": "Quantity" }
    },
    {
      "path": "ByCode[en].Quantity",
      "segments": ["ByCode", "en", "Quantity"],
      "code": "invalid_range",
      "category": "too_big",
      "origin": "number",
      "minimum": 1,
      "maximum": 100,
      "inclusive": true,
      "message": "Field 'Quantity' must be greater than or equal to 1 and less than or equal to 100.",
      "parameters": { "minimum": "1", "maximum": "100", "displayName": "Quantity", "declaringType": "OrderLine", "member": "Quantity" }
    }
  ],
  "count": 2,
  "displayName": "Quantity",
  "declaringType": "OrderLine",
  "member": "Quantity",
  "minimum": "1",
  "maximum": "100",
  "traceId": "00-3b87d0e8fa5f0b442bf75ffb97d44b3d-103774b7a225269d-02"
}
```

A failed union reports its options' issues under `unionErrors`:

```json
{
  "path": "Contact",
  "segments": ["Contact"],
  "code": "invalid_union",
  "category": "invalid_union",
  "message": "Value does not match any of the union options (string, number)",
  "parameters": { "options": ["string", "number"] },
  "unionErrors": [
    [{ "path": "", "segments": [], "code": "invalid_string", "category": "invalid_format", "origin": "string", "message": "Invalid email format", "parameters": { "format": "email" } }],
    [{ "path": "", "segments": [], "code": "invalid_type", "category": "invalid_type", "message": "Expected number, received string", "parameters": { "expected": "number", "received": "string" } }]
  ]
}
```

The contract:

- **Keys.** `errors` keys and `issues[].path` are `ValidationError.FullPath.ToDottedString()`: members joined with
  `.`, indexes as `[0]`, and keys as `[en]` or, when ambiguous, quoted (`Map["0"]`, `Map["a.b"]`). The root is `""`.
  See [Core Concepts](Core-Concepts.md#paths). By default member names are the C# names, matching
  Microsoft.Extensions.Validation and MVC model state. `PathNaming = ZodPathNaming.Json` applies the minimal-API
  `JsonOptions` naming policy.
- **`code` and `category`.** `code` is ZodSharp's specific identity (`invalid_range`). `category` is the Zod issue
  code (`too_small`), so a client can treat ZodSharp and Zod issues alike.
- **Top-level parameters.** Parameters are written per issue only. With `MergeParametersIntoExtensions = true` (the 2.4
  behaviour, off by default) every issue's parameters are also merged into the top-level extensions, and the last
  issue wins. A parameter whose camel-cased name is reserved (`type`, `title`, `status`, `detail`, `instance`,
  `errors`, `issues`, `traceId`) is written as `param_{name}`.
- **`traceId`.** It is present on every path: `Activity.Current?.Id`, else `HttpContext.TraceIdentifier`.
- **No input values.** Messages and parameters never contain the input unless `ZodErrorOptions.IncludeReceivedValues`
  is enabled. See [Core Concepts](Core-Concepts.md#pii).
- **Messages.** They come from the registered `IZodMessageFormatter` in `CultureInfo.CurrentUICulture`. See
  [Localization](Localization.md).
- **OpenAPI.** The body is described by the `ZodValidationProblemDetails` and `ZodValidationIssue` component schemas
  (see [OpenAPI Integration](OpenAPI-Integration.md)).

## Minimal API validation

There are two ways to validate minimal-API parameters: the `WithZodSharpValidation()` endpoint filter, on every
target framework, and on .NET 11 the built-in validation pipeline
([below](#net-11-the-built-in-validation-pipeline)).

```csharp
using ZodSharp.AspNetCore;

// Every endpoint of a group: each bound parameter with a registered validator is validated.
var api = app.MapGroup("/api").WithZodSharpValidation(options =>
{
    options.ThrowIfNoValidator = false;
    options.OnBindingFailure = ZodBindingFailureBehavior.PassThrough;
});

// One endpoint, one type: every bound argument assignable to UserDto is validated.
app.MapPost("/users", (UserDto dto) => dto).WithZodSharpValidation<UserDto>();
```

`WithZodSharpValidation()` works on any `IEndpointConventionBuilder`, including route groups:

- **Which parameters.** They are selected when the endpoint is built. Services (`[FromServices]`, keyed services,
  anything the container resolves), `HttpContext`, `HttpRequest`, `HttpResponse`, `ClaimsPrincipal`,
  `CancellationToken`, form files, streams and `[SkipValidation]` parameters are skipped. `[FromBody]`,
  `[FromRoute]`, `[FromQuery]`, `[FromHeader]` and `[FromForm]` force a parameter to be considered.
- **How.** Every selected parameter is validated through `IZodSchemaFactory.TryGetUntypedValidator`.
- **Error keys.** They follow Microsoft.Extensions.Validation: a complex parameter's errors are not prefixed
  (`Name`, `Address.Street`), a sequence parameter's are (`items[0].Name`), and an error about the value itself is
  keyed by the parameter name.
- **Response metadata.** The endpoint advertises a `400` `application/problem+json` response (status
  `ProducesStatusCode`) for OpenAPI.
- **The body.** The filter honours `ZodProblemDetailsOptions` and the `ErrorTypeRegistry`, and writes through
  `IProblemDetailsService`.

`WithZodSharpValidation<T>()` (on a `RouteHandlerBuilder`) validates every bound argument assignable to `T`. The
validator is resolved when the endpoint is built. When no `IZodSchemaFactory` or no validator for `T` is
registered, building the endpoint fails with a message naming the handler and the parameter.

| `ZodEndpointValidationOptions` | Default | Effect |
|---|---|---|
| `ThrowIfNoValidator` | `false` | Fail endpoint building when a complex bound parameter has no registered validator. |
| `OnBindingFailure` | `PassThrough` | What to do when binding already failed (see below). |
| `ProducesStatusCode` | `400` | The status advertised in response metadata; set it when a mapped `ErrorType` changes the status. |

On .NET 11, endpoint filters also run after a route, query, header or form binding failure, with the status
already set to 400 and the argument left at its default. A malformed JSON body is answered by ASP.NET Core itself
and never reaches a filter. Before .NET 11 this option has no effect.

| `ZodBindingFailureBehavior` | Effect |
|---|---|
| `PassThrough` | Do not validate; ASP.NET Core's binding-failure response stands. |
| `ConvertToProblemDetails` | Write the ZodSharp contract instead: an `invalid_type` issue (`invalid_json` for a body) for each unbound validated parameter, or one root `invalid_type` issue when the failed parameter cannot be identified. |

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

- `AddZodSharpProblemDetails()` registers the bridge's `IZodValidationReportSink`. It appends every
  full-fidelity ZodSharp error to a `ZodRequestValidationReport` stored on the current request
  (`HttpContext.Features`), so the report lives and dies with the request. Writes made outside an HTTP request that
  is still being handled (an interactive Blazor circuit, WebAssembly, a background service) are discarded.
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
  follow `Accept-Language` (with `UseRequestLocalization`); codes and paths are unchanged. See
  [Localization](Localization.md).
- **The bridge**: resolver placement, `ZodReport` and non-HTTP validation are covered in
  [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md).
- **Unions**: a union parameter is claimed by the runtime parameter resolver, which the application's
  `AddValidation()` call adds. The active case is reported under `Value` (`Value.Email`).
- **Double validation**: `WithZodSharpValidation()` on an endpoint the pipeline validates skips the parameters
  the bridge covers, logging that once at `Debug` when the endpoint is built. On a `DisableValidation()` endpoint
  it validates as usual.
- **OpenAPI**: the `Purview.ZodSharp.AspNetCore.OpenApi` operation transformer adds the validation problem
  response for endpoints with a ZodSharp-known parameter. No endpoint metadata is added by the pipeline path. See
  [OpenAPI Integration](OpenAPI-Integration.md).

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
using ZodSharp;

try
{
    var parsed = EmailAddressSchema.Parse(rawValue);
}
catch (ZodException ex)
{
    return ex.ToValidationProblem(httpContext);
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
does the following:

- registers `ZodExceptionHandler` and the shared `ZodProblemDetailsWriter`;
- calls `services.AddProblemDetails(problemDetails)` and configures `ZodProblemDetailsOptions`;
- adds the ProblemDetails payload's source-generated JSON metadata to the minimal-API `JsonOptions`, so it
  serializes under Native AOT;
- on `net11.0`, enriches the built-in validation pipeline ([above](#net-11-the-built-in-validation-pipeline)).

`AddZodSharp` also registers the writer and the JSON metadata.

| `ZodProblemDetailsOptions` | Default | Effect |
|---|---|---|
| `Registry` | `ErrorTypeRegistry.Default` | Resolves error codes to `ErrorType`s. |
| `ErrorTypeResolver` | `null` | A lookup used instead of `Registry`. |
| `FormatMessages` | `true` | Format messages from the matched `ErrorType.MessageFormat`, only when every placeholder resolves. |
| `PathNaming` | `ZodPathNaming.MemberName` | `Json` renders keys with the minimal-API `JsonOptions` naming policy. |
| `IncludeIssues` | `true` | Include the `issues` extension. |
| `MergeParametersIntoExtensions` | `false` | Also merge every issue's parameters into the top-level extensions (last wins). |
| `ProjectDataAnnotationsIssues` | `true` | Give non-ZodSharp `errors` entries an `issues` entry with code `dataannotations` (`ZodProblemDetailsWriter.DataAnnotationsCode`). |
| `DefaultStatusCode` | `400` | The status when no matched `ErrorType` supplies one. |
| `StatusCodeSelector` | `null` | `Func<ImmutableArray<ValidationError>, int>`: takes complete control of the status. It runs after the highest `ErrorType.HttpStatus` is computed, and its result wins. |
| `StatusCodeOverride` | `null` | `Func<ImmutableArray<ValidationError>, int, int>`: like `StatusCodeSelector`, but also receives the computed status. It runs last and wins. |

`MapErrorType(ErrorType)` registers an `ErrorType` with the configured `Registry`.

> [!NOTE]
> `AddZodSharpProblemDetails` only wires up exception handling and ProblemDetails services (and, on .NET 11,
> the pipeline enrichment). It does **not** register `IZodSchemaFactory`. If you also want DI-based validator resolution, register the
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
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "One or more validation errors occurred.",
  "status": 409,
  "detail": "The aggregate could not be saved.",
  "errors": {
    "": ["Aggregate 'agg-123' (of type Invoice) failed to save"]
  },
  "issues": [
    {
      "path": "",
      "segments": [],
      "code": "aggregate_save_failed",
      "category": "invalid_value",
      "message": "Aggregate 'agg-123' (of type Invoice) failed to save",
      "parameters": { "aggregateId": "agg-123", "aggregateType": "Invoice" }
    }
  ],
  "aggregateId": "agg-123",
  "aggregateType": "Invoice",
  "traceId": "..."
}
```

Mapping rules:

- **Status** — the highest matched `ErrorType.HttpStatus` wins; unmapped codes fall back to
  `DefaultStatusCode` (`400`). `StatusCodeSelector` and then `StatusCodeOverride` run after that computation, and
  their result wins.
- **Title / Type / Detail** — taken from the highest-status matched `ErrorType`; otherwise defaulted. `type` is the
  RFC 9110 URI for the status.
- **Message** — with `FormatMessages`, the `MessageFormat` replaces the error's message only when **every**
  placeholder resolves, so a template is never returned with `{placeholders}` left in. Placeholders resolve from
  `ValidationError.Parameters`, then from the built-ins `path`, `code`, `origin`, `minimum`, `maximum` and
  `displayName`. The analyzer `ZODSASP001` (shipped with the core package) warns at compile time when a
  `MessageFormat` placeholder is not declared in `Parameters`.
- **Parameters** — the error's `ValidationError.Parameters` are surfaced per issue in `issues[].parameters`
  (camel-cased, safe scalars only). With `MergeParametersIntoExtensions` they are also merged into the top-level
  extensions, and reserved names are written as `param_{name}`.

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

`AddZodSharp(Action<ZodSchemaFactoryOptions>? configure)` registers `IZodSchemaFactory` as a singleton. When the factory is built, it does three things in order:

1. It applies every `ZodSchemaRegistration`, including the trim-safe ones added by the generated
   `services.AddZodSharp{Assembly}Schemas()` extensions.
2. It applies `options.ConfigureFactory`.
3. It calls `factory.RegisterFromAssembly(assembly)` for each distinct assembly discovered from the configured
   assembly sources, which auto-discovers source-generated `[assembly: ZodSchemaGenerated(typeof(...))]`
   registrations.

Assembly scanning is not trim-safe. In a trimmed or Native AOT host, use the generated `AddZodSharp{Assembly}Schemas()` (see [Source Generator](Source-Generator.md)).

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
| `AddZodSharp{Assembly}Schemas()` | generated into each assembly with `[ZodSchema]` types | a trim-safe `ZodSchemaRegistration` naming every generated validator | Trimmed or Native AOT hosts, and anywhere assembly scanning is unwanted. |
| `AddZodSharpProblemDetails(...)` | `Purview.ZodSharp.AspNetCore` | `ZodExceptionHandler`, the ProblemDetails writer and services, and the .NET 11 pipeline enrichment — does **not** register the factory | Mapping thrown `ZodException`s to `ProblemDetails`; pair it with `AddZodSharp` when you also need DI validator resolution. |
| `AddZodSharpValidation(...)` | `Purview.ZodSharp.Extensions.Validation` (`net11.0`) | the Microsoft.Extensions.Validation resolver | Validating with the .NET 11 pipeline; see [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md). |
| `AddZodSchemaOptionsValidator<T>(...)` | core `Purview.ZodSharp` | singleton `IValidateOptions<T>` | Validating options objects; requires a factory registered first via `AddZodSharp` or `AddZodSharpFactory`. Validates every options name; on .NET 11 `ValidateOnStart()` runs its asynchronous rules too. |
| `OptionsBuilder<T>.BindAndValidateWithZod(section)` | `Purview.ZodSharp.AspNetCore` | binds a configuration section and adds the ZodSharp options validator | Options whose failures should name the configuration key (`Smtp:Servers:0:Host`). Uses the reflection-based binder; under Native AOT call `BindConfiguration` and `AddZodSchemaValidator` yourself. |

See [Dependency Injection](Dependency-Injection.md) for the underlying factory and options-validation wiring.

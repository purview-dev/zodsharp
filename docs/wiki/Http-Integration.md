# HttpClient Integration

`Purview.ZodSharp.Http` validates JSON bodies on both sides of an `HttpClient` call and reads the validation problems a
ZodSharp API returns back into ZodSharp errors. It targets `net10.0` and `net11.0`, references only the base framework
(no ASP.NET Core shared framework), and is browser-safe: it is the package a Blazor WebAssembly client, a Blazor Server
component, a MAUI app or a service-to-service client uses to talk to an API built with
[ASP.NET Core Integration](AspNetCore-Integration.md).

```bash
dotnet add package Purview.ZodSharp.Http
```

Every API takes a `JsonTypeInfo<T>`, typically from a source-generated `JsonSerializerContext`. There are deliberately
no `JsonSerializerOptions` overloads, so nothing resolves a contract reflectively; see
[Trimming and Native AOT](Trimming-and-AOT.md).

## Sending

```csharp
var sent = await http.PostAsJsonValidatedAsync(
    "api/orders", order, new OrderSchemaValidator(), AppJsonContext.Default.Order);
```

| API | Behaviour |
| --- | --- |
| `PostAsJsonValidatedAsync`, `PutAsJsonValidatedAsync`, `PatchAsJsonValidatedAsync` (`string?` or `Uri?`) | Validates with `ValidateAsync` (async refinements run). Invalid: a failed `ValidationResult<HttpResponseMessage>` and **no request**. Valid: the response, whatever its status. |
| `ZodJsonContent.Create`, `ZodJsonContent.CreateAsync` | The validated `JsonContent` (`application/json; charset=utf-8` unless a media type is given), for any other request shape. |

The body is the *validated* value (`result.Value`), so a schema's transforms apply before serialization.

## Service-aware rules

A rule that needs services - an `[ZodAsyncMember]` uniqueness check resolving a repository, a lookup that calls another
API - only runs when the validator is given an `IServiceProvider`. Each API has an overload that takes an
`IZodSchemaValidator<T>` (a generated `{TypeName}SchemaValidator`) and the services:

```csharp
var sent = await http.PostAsJsonValidatedAsync(
    "api/customers", customer, new RegisterCustomerRequestSchemaValidator(),
    AppJsonContext.Default.RegisterCustomerRequest, services, cancellationToken);

var read = await response.Content.ReadFromJsonAndValidateAsync(
    new CustomerSchemaValidator(), AppJsonContext.Default.Customer, services, cancellationToken);

var content = await ZodJsonContent.CreateAsync(
    new OrderSchemaValidator(), order, AppJsonContext.Default.Order, services);
```

| Where | Pass |
| --- | --- |
| Blazor WebAssembly | the component's (or typed client's) injected `IServiceProvider`: the browser's services |
| Blazor Server | the same injection: the circuit's services |
| A minimal API or controller | `HttpContext.RequestServices` |
| A worker or console app | a scope's `ServiceProvider` |

A service-aware rule is never skipped silently: a generated validator whose model has one throws
`InvalidOperationException` ("... needs an IServiceProvider. Call ValidateAsync with the caller's services.") when it is
used through an overload without services, so the mistake surfaces on the first call rather than as an unchecked
request. `Purview.ZodSharp.SystemTextJson` has the same overloads for `DeserializeAndValidateAsync` and
`ValidateAndSerializeAsync`.

## Receiving

```csharp
var result = await response.Content.ReadFromJsonAndValidateAsync(
    new OrderSchemaValidator(), AppJsonContext.Default.Order);
```

`ReadFromJsonAndValidateAsync` deserializes and validates in one pass, exactly like `DeserializeAndValidateAsync` in
[System.Text.Json Integration](SystemTextJson-Integration.md): malformed JSON, a `null` document or an unconvertible
value is one `invalid_json` error with the serializer's path, never an exception. The body is read as UTF-8, the
encoding RFC 8259 requires for JSON exchanged over a network.

## Validation problems

```csharp
if (await response.TryReadZodValidationProblemAsync() is { } problem)
{
    ImmutableArray<ValidationError> errors = problem.ToValidationErrors();
    ValidationResult<Order> result = problem.ToValidationResult<Order>();
}
```

- `TryReadZodValidationProblemAsync` returns `null` unless the response is unsuccessful, JSON
  (`application/problem+json`, `application/json` or `*+json`) and carries `errors` or `issues`. `Status` falls back to
  the response status.
- `ToValidationErrors` rebuilds one error per `issues` entry with its code, category, typed path (`segments`), origin,
  bounds, parameters and union options; without `issues` it falls back to one `custom` error per `errors` message, with
  the path parsed from the dotted key.
- The types live in `ZodSharp.Http` (`ZodValidationProblemDetails`, `ZodValidationProblemIssue`); the extensions live in
  `System.Net.Http`.

`Purview.ZodSharp.Http.UnitTests` starts a server with `AddZodSharpProblemDetails()` and proves every field it writes
(code, category, typed path, origin, numeric and exclusive bounds, parameters, union options, the root path) reads back
unchanged.

## A typed client for every render mode

Wrap the calls in one typed client and register it for each host, so the same rules guard the request wherever it is
made:

```csharp
// Blazor WebAssembly (Program.cs of the client): the browser's HttpClient and services.
builder.Services.AddScoped<CustomerAPIClient>();

// The server (Blazor Server, a worker, another API): a typed client from IHttpClientFactory. It is created in the
// requesting scope, so the IServiceProvider it receives is the circuit's or the request's.
services.AddHttpClient<CustomerAPIClient>(http => http.BaseAddress = new("https://api.example.com/"));
```

In Blazor, place either kind of rejection on the form's fields with
[Blazor Integration](Blazor-Integration.md)'s `EditContext.AddZodErrors(errors)` (rejected before sending) or
`EditContext.AddZodProblemDetails(problem)` (rejected by the server). Both use the typed paths, so `Lines[0].Quantity`
lands on `order.Lines[0].Quantity`.

## The sample

`src/src/Examples.Blazor` uses this package end to end:

- `Examples.Blazor.Client/CustomerAPIClient.cs` - the typed client: `PostAsJsonValidatedAsync` with the caller's
  services (so the async email-uniqueness rule runs before sending), `TryReadZodValidationProblemAsync` for a server
  rejection, and `ReadFromJsonAndValidateAsync` for the response.
- `Examples.Blazor.Client/RegisterCustomerForm.razor` - one form, `<ZodValidator>` plus `AddZodErrors` for whichever
  side rejected the registration.
- `/register` renders it in **Interactive WebAssembly** (posting from the browser); `/register-server` renders it in
  **Interactive Server**, where `IHttpClientFactory` creates the client and it calls the API service-to-service.
- `Examples.Blazor.IntegrationTests/CustomerAPIClientTests.cs` resolves the server's typed-client registration and drives
  it against the in-memory API, counting the requests actually sent; `tests/e2e/blazor-sample.e2e.mjs` drives both pages
  in a browser.

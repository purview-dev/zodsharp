# Purview.ZodSharp.Http

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.Http.svg)](https://www.nuget.org/packages/Purview.ZodSharp.Http)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

`HttpClient` integration for [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp). Validate request bodies before they are sent, validate response bodies as they are read, and turn the validation problems [Purview.ZodSharp.AspNetCore](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore) writes back into ZodSharp errors - with the same codes, typed paths and bounds on both sides of the wire.

- **Where it runs:** Blazor WebAssembly, Blazor Server, MAUI, console apps and server-to-server calls. It targets
  `net10.0` and `net11.0` and references only the base framework (never the ASP.NET Core shared framework).
- **Trim-, Native AOT- and WebAssembly-safe:** every API takes a `JsonTypeInfo<T>` from a source-generated
  `JsonSerializerContext`; there are deliberately no reflection-based `JsonSerializerOptions` overloads.

## Installation

```bash
dotnet add package Purview.ZodSharp.Http
```

## Contracts

```csharp
[ZodSchema]
public partial class Order
{
    [MinLengthZod(3)]
    public string Reference { get; set; } = string.Empty;

    [MinValue(1)]
    public int Quantity { get; set; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Order))]
partial class AppJsonContext : JsonSerializerContext;
```

## Send a validated body

The value is validated first (async refinements included); an invalid value returns the errors and **nothing is
sent**. A valid value returns the response, whatever its status.

```csharp
var sent = await http.PostAsJsonValidatedAsync(
    "api/orders", order, new OrderSchemaValidator(), AppJsonContext.Default.Order);

if (!sent.IsSuccess)
    return sent.Errors;              // the request was never made

using var response = sent.Value!;    // whatever the server answered
```

`PutAsJsonValidatedAsync` and `PatchAsJsonValidatedAsync` work the same way (each with `string?` and `Uri?` overloads),
and `ZodJsonContent.Create`/`CreateAsync` build the validated `JsonContent` for any other request.

### Service-aware rules

Rules that need services - an async uniqueness check that resolves a repository or calls an API - run when you pass a
generated `{TypeName}SchemaValidator` and an `IServiceProvider`:

```csharp
var sent = await http.PostAsJsonValidatedAsync(
    "api/customers", customer, new RegisterCustomerRequestSchemaValidator(),
    AppJsonContext.Default.RegisterCustomerRequest, services, cancellationToken);
```

Pass a component's injected `IServiceProvider` in Blazor (the browser's services in WebAssembly, the circuit's on the
server), or the request services on a server. Every send method, `ZodJsonContent.CreateAsync` and
`ReadFromJsonAndValidateAsync` has this overload. A generated validator with a service-aware rule throws
`InvalidOperationException` when it is used without services, so the rule is never skipped silently.

## Read a validated body

```csharp
var result = await response.Content.ReadFromJsonAndValidateAsync(
    new OrderSchemaValidator(), AppJsonContext.Default.Order);
```

A body that cannot be read as the type (malformed JSON, a `null` document, a value the serializer cannot convert) is an
`invalid_json` error rather than an exception, exactly as `DeserializeAndValidateAsync` in
[Purview.ZodSharp.SystemTextJson](https://www.nuget.org/packages/Purview.ZodSharp.SystemTextJson) reports it. The body
is read as UTF-8, the encoding JSON exchanged over a network must use.

## Read a server validation problem

```csharp
if (await response.TryReadZodValidationProblemAsync() is { } problem)
{
    var errors = problem.ToValidationErrors();        // codes, typed paths, bounds, parameters, union options
    var result = problem.ToValidationResult<Order>(); // or as a failed result
}
```

`TryReadZodValidationProblemAsync` returns `null` for anything that is not a validation problem: a successful response, a
non-JSON body, or a ProblemDetails without `errors`/`issues`. It reads both the full ZodSharp contract (`issues`) and a
plain ASP.NET Core `ValidationProblemDetails` (`errors` only); `Status` falls back to the response status. The types
(`ZodValidationProblemDetails`, `ZodValidationProblemIssue`) are in `ZodSharp.Http`; the extensions are in
`System.Net.Http`.

## A typed client

The pattern the repository's Blazor sample uses: one class for every caller, so the same rules guard the request in the
browser and on the server.

```csharp
sealed class CustomerAPIClient(HttpClient http, IServiceProvider services)
{
    public async Task<ValidationResult<CustomerRegistered>> RegisterAsync(
        RegisterCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var sent = await http.PostAsJsonValidatedAsync("api/customers", request,
            new RegisterCustomerRequestSchemaValidator(), AppJsonContext.Default.RegisterCustomerRequest,
            services, cancellationToken);
        if (!sent.IsSuccess)
            return ValidationResult<CustomerRegistered>.Failure(sent.Errors);   // rejected before sending

        using var response = sent.Value!;
        if (await response.TryReadZodValidationProblemAsync(cancellationToken) is { } problem)
            return problem.ToValidationResult<CustomerRegistered>();            // rejected by the server

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAndValidateAsync(
            new CustomerRegisteredSchemaValidator(), AppJsonContext.Default.CustomerRegistered,
            cancellationToken);
    }
}

// Blazor WebAssembly: the browser's HttpClient and services.
builder.Services.AddScoped<CustomerAPIClient>();

// A server (Blazor Server, a worker, another API): a typed client from IHttpClientFactory.
builder.Services.AddHttpClient<CustomerAPIClient>(http => http.BaseAddress = new("https://api.example.com/"));
```

In Blazor, [Purview.ZodSharp.Blazor](https://www.nuget.org/packages/Purview.ZodSharp.Blazor) puts either kind of
rejection on the form's fields: `editContext.AddZodErrors(errors)`, or `editContext.AddZodProblemDetails(problem)`.

## Documentation

- [HttpClient Integration](https://purview.dev/docs/zodsharp/http-integration/)
- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.Http` on NuGet.

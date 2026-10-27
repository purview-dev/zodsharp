# Purview.ZodSharp.Blazor

Blazor forms validated by [ZodSharp](https://github.com/purview-dev/zodsharp) on **Blazor Server**, **Blazor
WebAssembly** and **Auto** render mode (`net11.0`).

```bash
dotnet add package Purview.ZodSharp.Blazor
```

```csharp
// Program.cs - the server AND the client project of a Blazor Web App.
builder.Services.AddZodSharpMyAppSharedSchemas();   // generated, trim-safe registration of your [ZodSchema] models
builder.Services.AddZodSharpBlazor();
```

## Route 1: the standard `<DataAnnotationsValidator />`

```razor
<EditForm Model="order" OnValidSubmit="Save">
    <DataAnnotationsValidator />
    <InputText @bind-Value="order.Customer.Email" />
    <ValidationMessage For="() => order.Customer.Email" />
    <ValidationSummary />
</EditForm>
```

`[ZodSchema]` models validate with ZodSharp, **including nested objects, collections and dictionaries, with no
`[ValidatableType]` and no Microsoft.Extensions.Validation generator** - the generator does not flow to a
WebAssembly client, so this is what makes nested validation work there. A field change runs only that member's
own rules; async rules show the platform's `pending` and `faulted` states.

## Route 2: `<ZodValidator />` (full fidelity)

```razor
<EditForm Model="order" OnValidSubmit="Save">
    <ZodValidator ValidateOn="ZodValidationTrigger.Input" DebounceMilliseconds="300" />
    <ZodValidationMessage For="() => order.Customer.Email" ShowCode="true" />
    <ZodValidationSummary GroupByPath="true" ShowUnionDetails="true" />
</EditForm>
```

- Keeps the full `ValidationError` (code, category, origin, bounds, parameters) behind every message:
  `editContext.GetZodErrors(() => order.Customer.Email)`, `GetZodErrors(field)`, `GetZodErrors()`.
- `ValidateOn`: `Submit`, `Change` (default) or `Input` with `DebounceMilliseconds` (each change supersedes and
  cancels the pending check). Submit-time validation always runs.
- `Schema`: an explicit validator for models without a registered one -
  `ZodModelSchema.For<Order>(Z.Object().Field("Reference", Z.String().Min(3)).Build())`.
- `ValidateNestedOnFieldChange`: a field change re-validates the whole owning object (cross-field rules).
- With a `DataAnnotationsValidator` on the same form it records the full errors but leaves the messages to
  `DataAnnotationsValidator`, so nothing is shown twice, and logs one warning. Use one validator.

`ZodValidationMessage` and `ZodValidationSummary` render the same markup and CSS classes as the built-in
components, plus `data-code`/`data-category`.

## Server errors onto the form

```csharp
var response = await http.PostAsJsonAsync("/orders", order);
if (await response.TryReadZodValidationProblemAsync() is { } problem)
    editContext.AddZodProblemDetails(problem);   // cleared per field when the field changes
```

Reads Purview.ZodSharp.AspNetCore's `issues[]` (typed `segments`), or a plain `errors` dictionary, with
source-generated JSON metadata (trim- and AOT-safe).

## Static SSR client-side validation

`zodsharp-validation.js` ships as a static web asset and registers itself through a Blazor JavaScript initializer.
On the server, `AddZodSharpClientValidation()` (Purview.ZodSharp.AspNetCore) adds the matching rules - for
`[StartsWith]`, `[EndsWith]`, `[Includes]`, `[UUID]`, `[Cuid2]`, `[ULID]`, `[IPAddress]`, `[Cidr]`, `[Regex]`,
`[Int]`, `[MultipleOf]` and the bound attributes - to the form's `data-rules` payload, beside ASP.NET Core's
DataAnnotations rules.

Limitations:

- ASP.NET Core has no public API to add rules to its DataAnnotations provider, so ZodSharp decorates it and merges
  into its rendered payload. The payload format is pinned by contract tests.
- Async, service-aware and culture-sensitive rules, and custom rules whose metadata has no JavaScript equivalent,
  are enforced on the server only. .NET and JavaScript regular expressions differ at the edges.
- Client rules only improve the user experience; **the server always validates the posted form**.

## Notes

- Blazor Server: validation services resolve from the **circuit** scope. In async validators use
  `IDbContextFactory<T>` or an explicitly created scope rather than a scoped `DbContext`.
- Don't validate in `OnInitialized` of prerendered components; it would run once per render phase.
- WebAssembly: prefer the generated `AddZodSharp{Assembly}Schemas()` registration; assembly scanning is not
  trim-safe.

Documentation: <https://purview.dev/docs/zodsharp/>

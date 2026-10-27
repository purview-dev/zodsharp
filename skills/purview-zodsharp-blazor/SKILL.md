---
name: purview-zodsharp-blazor
description: >-
  Use when validating Blazor EditForm models with Purview.ZodSharp on Blazor Server, Blazor WebAssembly or Auto
  render mode - AddZodSharpBlazor, the standard DataAnnotationsValidator route versus ZodValidator, nested and
  collection field placement, debounced or per-field validation, GetZodErrors, ZodValidationMessage and
  ZodValidationSummary, mapping API ProblemDetails onto fields with TryReadZodValidationProblemAsync and
  AddZodProblemDetails, and static-SSR client-side validation with AddZodSharpClientValidation.
license: MIT
metadata:
  product: Purview.ZodSharp.Blazor
  repository: purview-dev/zodsharp
---

# Blazor forms

`Purview.ZodSharp.Blazor` (`net11.0`) validates `EditForm` models with ZodSharp in every render mode. It references
the component packages, never the ASP.NET Core shared framework, so it runs in WebAssembly.

```bash
dotnet add package Purview.ZodSharp.Blazor
```

## Setup - both Program.cs files

```csharp
builder.Services.AddZodSharpMyAppSharedSchemas();   // generated: AddZodSharp{AssemblyName}Schemas()
builder.Services.AddZodSharpBlazor();
```

- Call both in the **server and the client** project of a Blazor Web App. Put the models in a shared library;
  the ZodSharp generator runs there. Models declared in `.razor` files are invisible to generators.
- Prefer the generated registration. Assembly scanning (`AddZodSharpAssembly*`, `RegisterFromAssembly`) is not
  trim-safe and warns in a WebAssembly publish.
- `AddZodSharpBlazor` does **not** register an `IZodValidationReportSink`; never register a scoped one yourself (a
  Blazor Server scope is the whole circuit). The ASP.NET Core sink is request-bound and safe alongside Blazor.

## Choosing a route

| Need | Use |
| --- | --- |
| Standard forms over `[ZodSchema]` models, minimal setup | `<DataAnnotationsValidator />` |
| Codes, categories, bounds in the UI; custom message rendering | `<ZodValidator />` |
| A runtime `Z.Object(...)` schema for a CLR model | `<ZodValidator Schema="..." />` |
| Debounced per-keystroke validation | `<ZodValidator ValidateOn="ZodValidationTrigger.Input" DebounceMilliseconds="300" />` |
| Cross-field re-check when a field changes | `<ZodValidator ValidateNestedOnFieldChange="true" />` |
| API validation errors on fields | either, plus `AddZodProblemDetails` |

Use one validator per form. With both, `ZodValidator` records full errors but leaves messages to
`DataAnnotationsValidator` and logs a warning.

## Route 1 - DataAnnotationsValidator

Nothing else is needed. Lead with this guarantee: `[ZodSchema]` models get **nested, collection and dictionary
validation with no `[ValidatableType]`, no `AddValidation()` and no Microsoft.Extensions.Validation generator** -
which is the only way to get nested validation in WebAssembly, where that generator does not flow. Messages land
on the right `FieldIdentifier` (`order.Lines[1].Quantity`). A field change runs only that member's rules. Async
rules drive `IsValidationPending`/`IsValidationFaulted` and the `pending`/`faulted` CSS classes.

## Route 2 - ZodValidator

```razor
<EditForm Model="order" OnValidSubmit="Save">
    <ZodValidator />
    <InputText @bind-Value="order.Customer.Email" />
    <ZodValidationMessage For="() => order.Customer.Email" ShowCode="true" />
    <ZodValidationSummary GroupByPath="true" />
</EditForm>
```

- `ValidateOn`: `Submit` (field changes don't validate), `Change` (default), `Input` (+ `DebounceMilliseconds`;
  a newer change cancels the pending one). Submit-time validation always runs.
- Full errors: `editContext.GetZodErrors(() => order.Customer.Email)`, `GetZodErrors(field)`, `GetZodErrors()`.
- Runtime schema: `Schema="ZodModelSchema.For<Order>(Z.Object().Field("Reference", Z.String().Min(3)).Build())"`
  (field names are property names; nested objects are read too). A field change then validates the whole model
  and keeps that field's errors.
- `TimeProvider` is resolved from DI, so tests can fake debounce time.
- A validator that throws is logged through `ILogger<ZodValidator>` and the field is marked faulted.
- `ZodValidationMessage`/`ZodValidationSummary` match the built-in markup and add `data-code`/`data-category`;
  `Template` renders a `RenderFragment<ValidationError>`; `ShowUnionDetails` lists each union option's issues.

## Server errors onto fields

```csharp
var response = await http.PostAsJsonAsync("/orders", order);
if (await response.TryReadZodValidationProblemAsync() is { } problem)
    editContext.AddZodProblemDetails(problem);
```

Places `issues[].segments` (or parses the dotted `errors` keys of a plain ValidationProblemDetails), matches
camel-cased names case-insensitively, replaces the previous server errors, and clears a field's server error
when it changes (`clearOnFieldChange: false` keeps them). `ClearZodErrors()` removes every ZodSharp error.
Reading uses source-generated JSON metadata, so it is trim- and AOT-safe.

## Static SSR client-side validation

```csharp
builder.Services.AddZodSharpClientValidation();   // Purview.ZodSharp.AspNetCore, server only
```

Static-SSR forms then carry client rules for ZodSharp rule attributes beside ASP.NET Core's DataAnnotations
rules; `zodsharp-validation.js` registers the browser validators automatically (a Blazor JavaScript initializer).
Order relative to `AddRazorComponents` does not matter; a provider registered **after** it replaces ZodSharp's,
which a Development startup warning reports. Runtime schemas need `o.AddSchema<TModel>(schema)`.

Say plainly in any guidance: client rules are UX only; async, service-aware and culture-sensitive rules are
server-only; the server validates every post.

## Pitfalls

- **Circuit scope.** On Blazor Server, validators get the circuit's services for the whole connection. In async
  rules use `IDbContextFactory<T>` or create a scope; never resolve a scoped `DbContext` directly.
- **Prerendering.** Don't validate in `OnInitialized`; it runs once per render phase.
- **Synchronous `Validate()`** is obsolete and throws for models with async-only rules; use `ValidateAsync`.
- **WebAssembly culture.** Codes and default messages are invariant; localized messages need `AddLocalization()`
  and satellite assemblies (the formatter switches on automatically when an `IStringLocalizerFactory` exists).

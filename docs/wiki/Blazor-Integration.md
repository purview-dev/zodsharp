# Blazor Integration

`Purview.ZodSharp.Blazor` validates Blazor `EditForm` models with ZodSharp on **Blazor Server**, **Blazor
WebAssembly** and **Auto** render mode. It targets `net11.0` and references the component packages
(`Microsoft.AspNetCore.Components.Forms` and `.Web`), never the ASP.NET Core shared framework, so the same
package runs in the browser.

```bash
dotnet add package Purview.ZodSharp.Blazor
dotnet add package Purview.ZodSharp.AspNetCore   # server project only, for static-SSR client rules
```

## Setup

```csharp
// Program.cs - the server AND the client project.
builder.Services.AddZodSharpMyAppSharedSchemas();   // generated, trim-safe registration of [ZodSchema] models
builder.Services.AddZodSharpBlazor();
```

A Blazor Web App typically looks like this:

```
MyApp/          (server)  -> Purview.ZodSharp.Blazor, Purview.ZodSharp.AspNetCore
MyApp.Client/   (WASM)    -> Purview.ZodSharp.Blazor
MyApp.Shared/   (models)  -> Purview.ZodSharp (the generator runs here)
```

`AddZodSharpBlazor(configureValidation, configure)` calls `AddZodSharpValidation` (the
Microsoft.Extensions.Validation bridge), registers `ZodBlazorOptions` and registers `TimeProvider.System` when no
`TimeProvider` is registered. It never registers an `IZodValidationReportSink`.

## Two routes

| Need | Route |
| --- | --- |
| Standard forms, `[ZodSchema]` models, minimal setup | 1: `<DataAnnotationsValidator />` |
| Codes, categories, origins or bounds in the UI; custom message rendering | 2: `<ZodValidator />` |
| A runtime `Z.Object(...)` schema for a CLR model | 2: `Schema` parameter |
| API (server) validation errors on fields | either, plus `AddZodProblemDetails` |
| Debounced per-keystroke validation | 2: `Input` + `DebounceMilliseconds` |
| Field-level re-check of cross-field rules | 2: `ValidateNestedOnFieldChange` |

### Route 1: `<DataAnnotationsValidator />`

```razor
<EditForm Model="order" OnValidSubmit="Save">
    <DataAnnotationsValidator />
    <InputText @bind-Value="order.Customer.Email" />
    <ValidationMessage For="() => order.Customer.Email" />
    @foreach (var line in order.Lines)
    {
        <InputNumber @bind-Value="line.Quantity" />
        <ValidationMessage For="() => line.Quantity" />
    }
    <ValidationSummary />
</EditForm>
```

- **No `[ValidatableType]` and no Microsoft.Extensions.Validation generator** are needed for `[ZodSchema]` models.
  The ZodSharp resolver supplies the root type info, so Blazor takes its full-validation path: nested objects,
  collections (`Lines[0].Quantity`), dictionaries and unions each place their message on the right
  `FieldIdentifier`. In WebAssembly this is the difference between nested validation and top-level-only
  validation, because the Microsoft.Extensions.Validation generator does not flow to a client project.
- A field change runs only that member's own rules (`ValidateMember`), never nested schemas.
- Async rules use the platform's async validation, so `IsValidationPending`, `IsValidationFaulted` and the
  `pending`/`faulted` CSS classes work.
- Messages are localized through the shared formatter when `AddLocalization()` is configured.

The platform's message store holds strings only, so Route 1 cannot carry codes or other metadata.

### Route 2: `<ZodValidator />`

```razor
<EditForm Model="order" OnValidSubmit="Save">
    <ZodValidator ValidateOn="ZodValidationTrigger.Input" DebounceMilliseconds="250" />
    <InputText @bind-Value="order.Customer.Email" @bind-Value:event="oninput" />
    <ZodValidationMessage For="() => order.Customer.Email" ShowCode="true" />
    <ZodValidationSummary GroupByPath="true" />
</EditForm>
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `Schema` | `null` | An explicit `IZodUntypedValidator` for the model (see `ZodModelSchema`); otherwise the registered validator. |
| `ValidateOn` | `Change` | `Submit` (field changes don't validate), `Change`, `Input`. Submit-time validation always runs. |
| `DebounceMilliseconds` | `0` | Delay for `Input`-triggered field validation; each change supersedes and cancels the pending one. |
| `EnableClientValidation` | `true` | Emit static-SSR client rules, exactly as `DataAnnotationsValidator` does. |
| `ValidateNestedOnFieldChange` | `false` | Re-validate the whole object that owns the changed field and update all its fields. |

Behaviour:

- Form validation uses `ValidationRequestedEventArgs.AddAsyncValidator`; field validation uses
  `EditContext.RegisterAsyncFieldValidator`. Pending, faulted, cancellation and supersession come from the
  platform. Store updates are marshalled through `InvokeAsync`.
- A field change on a member the validator can validate on its own runs only that member's rules. For runtime
  schemas without member validation the whole model is validated and only the changed field's errors are kept;
  other fields' messages are left exactly as they were.
- The full `ValidationError` of every message is kept in `EditContext.Properties[ZodValidator.ErrorsKey]` and read
  with `GetZodErrors(field)`, `GetZodErrors(() => model.Member)` or `GetZodErrors()`.
- A validator that throws is logged through `ILogger<ZodValidator>` with the field, and the field (or form) is
  marked faulted.
- With a `DataAnnotationsValidator` on the same form, `ZodValidator` records the full errors for the types the bridge
  validates but leaves the messages to `DataAnnotationsValidator`, so nothing is duplicated, and logs one warning
  (`ZodBlazorOptions.WarnOnValidatorCoexistence`). A `DataAnnotationsValidator` with client validation disabled is
  not detected.

### Runtime schemas

```csharp
var schema = Z.Object()
    .Field("Reference", Z.String().Min(3))
    .Field("Customer", Z.Object().Field("Email", Z.String().Email()).Build())
    .Build();
IZodUntypedValidator validator = ZodModelSchema.For<Order>(schema);
```

`ZodModelSchema.For<TModel>(ZodObject)` reads the model's public properties (and nested objects) into the key/value
shape the object schema validates and places the errors on the CLR fields. Collections are passed as they are.
`ZodModelSchema.For<TModel>(IZodSchema<TModel, TModel>)` adapts a schema of the model's own type.

## Message components

`<ZodValidationMessage For="..." />` and `<ZodValidationSummary />` render the same markup and CSS classes as
`ValidationMessage` and `ValidationSummary` (interactive and static, including the static-SSR `data-valmsg-for`
placeholders), plus `data-code` and `data-category` when the full error is known.

- `ZodValidationMessage`: `ShowCode` (a `span.validation-code`), `Template` (`RenderFragment<ValidationError>`).
- `ZodValidationSummary`: `Model`/`ShowModelOnly` (model-level messages only), `GroupByPath`
  (`li.validation-group[data-path]`), `ShowUnionDetails` (each union option's issues).

With Route 1 both fall back to plain messages.

## Server errors onto forms

```csharp
var response = await http.PostAsJsonAsync("/orders", order);
if (await response.TryReadZodValidationProblemAsync() is { } problem)
    editContext.AddZodProblemDetails(problem);
```

- `TryReadZodValidationProblemAsync` reads an unsuccessful JSON response with `errors` or `issues` - the contract
  [ASP.NET Core Integration](AspNetCore-Integration.md) writes - using source-generated JSON metadata.
- `AddZodProblemDetails` places issues by their typed `segments`, so `Lines[0].Quantity` lands on
  `order.Lines[0].Quantity`; without `issues` it parses the dotted `errors` keys. Camel-cased member names match
  case-insensitively, and dictionary keys resolve. When a `ZodValidator` is on the form its validator resolves the
  paths.
- Server errors replace the previous server errors and are cleared per field when the field changes
  (`clearOnFieldChange: false` keeps them). `ClearZodErrors()` removes every ZodSharp error.

## Static SSR client-side validation

```csharp
// Server Program.cs
builder.Services.AddRazorComponents();
builder.Services.AddZodSharpClientValidation();   // Purview.ZodSharp.AspNetCore; either order works
```

A statically rendered form with `<DataAnnotationsValidator />` (or `<ZodValidator />`) then carries, beside ASP.NET
Core's DataAnnotations rules, client rules for:

| Attribute | Client validator |
| --- | --- |
| `[StartsWith]`, `[EndsWith]` (ordinal or ordinal-ignore-case) | `zod-startswith`, `zod-endswith` |
| `[Includes]` | `zod-includes` |
| `[UUID]` (optionally versioned) | `zod-uuid` |
| `[Cuid2]`, `[ULID]` | `zod-cuid2`, `zod-ulid` |
| `[IPAddress]`, `[Cidr]` | `zod-ip`, `zod-cidr` |
| `[Regex]` | `zod-regex` (a match anywhere, like `Regex.IsMatch`) |
| `[Int]`, `[MultipleOf]` | `zod-int`, `zod-multipleof` |
| `[MinValue]`, `[MaxValue]`, `[GreaterThanOrEqual]`, `[LessThanOrEqual]` | `zod-min`, `zod-max` |
| `[GreaterThan]`, `[LessThan]` | `zod-gt`, `zod-lt` |

Custom rule attributes get the same client rules when their rule reports the same `IZodRule` metadata (format
parameters and bounds); a custom attribute can also implement `IClientValidationRuleProvider` with
`ZodClientValidationRules.For(rule)`. Runtime schemas have no attributes: register them with
`AddZodSharpClientValidation(o => o.AddSchema<Order>(schema))` and the rules are derived from the schema's JSON
Schema (`minLength`, `maxLength`, `pattern`, bounds, `multipleOf`, `integer`, `uuid`/`ipv4`/`ipv6`).

The browser validators live in `_content/Purview.ZodSharp.Blazor/zodsharp-validation.js` and register through a
Blazor JavaScript initializer, so no `<script>` tag is needed. Each mirrors its C# rule; a parity suite runs them
against the C# verdicts on the same inputs.

### How it composes, and its limits

- ASP.NET Core 11 exposes `ClientValidationProvider` but no API to add rules to its internal DataAnnotations
  provider. `AddZodSharpClientValidation` therefore **decorates** the registered provider: it renders the
  decorated provider's fragment, reads its `data-rules` JSON and merges the ZodSharp rules into the same field
  entries. The payload format is undocumented; contract tests pin it.
- `AddRazorComponents` registers the default with `TryAdd`. `AddZodSharpClientValidation` replaces whatever is
  registered (or, when called first, the default `AddRazorComponents` would register), so either order works. A
  provider registered **after** it replaces ZodSharp's; in Development a startup check logs a warning.
- ASP.NET Core only emits its own DataAnnotations rules for fields the validation resolver reports as validatable,
  which ZodSharp's generated validators do for every member with rules.
- Async, service-aware and culture-sensitive rules, and rules without a JavaScript equivalent, are server-only.
  .NET and JavaScript regular expressions differ at the edges.
- **Client rules only improve the user experience. The server always validates the posted form.**

## Blazor Server

- Validation runs next to the app's services, so async rules can query a database. `ValidateContext.ServiceProvider`
  (and the services `ZodValidator` passes) is the **circuit** scope, which lives as long as the connection: a
  scoped `DbContext` resolved there is shared by every validation in the circuit. Use `IDbContextFactory<T>` or an
  explicitly created scope inside async validators.
- Every field change is a SignalR round trip anyway; debounce expensive async checks with `Input` +
  `DebounceMilliseconds`.
- `AddZodSharpBlazor` registers no `IZodValidationReportSink`. If the same app calls `AddZodSharpProblemDetails`
  (minimal APIs), its scoped sink is only read by the HTTP ProblemDetails callback. `ZodValidator` never writes to
  it; Route 1 (`DataAnnotationsValidator`, through the bridge) does append to the circuit's instance, which nothing
  reads, so in long-lived circuits of such an app prefer `ZodValidator`.
- Prerendering: don't validate in `OnInitialized`; it runs once per render phase. Use `PersistentComponentState`
  for server-only error state if needed.

## Blazor WebAssembly

- Validation in the browser is for UX; the server stays authoritative.
- Registration should be the generated `AddZodSharp{Assembly}Schemas()`; `AddZodSharpAssembly*` and
  `RegisterFromAssembly` are `[RequiresUnreferencedCode]`.
- Generated validators resolve members and validate single members without reflection. Reading problem details uses
  a source-generated `JsonSerializerContext`. The package is `IsTrimmable` and `IsAotCompatible`.
- Codes, parameters and default messages are culture-invariant, so `InvariantGlobalization` changes nothing;
  localized messages need `AddLocalization()`, satellite assemblies and a startup culture.
- WebAssembly is single-threaded: the obsolete synchronous `EditContext.Validate()` throws for models with
  async-only rules instead of blocking. Use `ValidateAsync`.

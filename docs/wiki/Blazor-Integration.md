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

`AddZodSharpBlazor(configureValidation, configure)` registers the Microsoft.Extensions.Validation bridge for **type
validation only** (`AddZodSharpValidation(configureValidation, validateParameters: false)`), registers
`ZodBlazorOptions` and registers `TimeProvider.System` when no `TimeProvider` is registered. It never registers an
`IZodValidationReportSink`.

> [!IMPORTANT]
> **`AddZodSharpBlazor` does not turn on minimal-API validation.** Forms and minimal APIs share one
> `ValidationOptions`, and its endpoint filter validates every handler parameter a registered resolver claims, before
> the handler runs. So the bridge `AddZodSharpBlazor` registers claims no parameters: a Blazor Server host that also
> maps endpoints keeps them exactly as they were. To have ZodSharp-typed endpoint parameters validated by the .NET 11
> pipeline as well (an invalid request answered with `400` before the handler), opt in:
>
> ```csharp
> builder.Services.AddZodSharpBlazor();
> builder.Services.AddZodSharpValidation();   // either order
> ```
>
> or `AddZodSharpBlazor(validation => validation.ValidateParameters = true)`. Once it is on, opt single endpoints or
> groups out with `DisableValidation()`, or every parameter with `ValidateParameters = false`. A WebAssembly client
> maps no endpoints, so this only matters on the server.

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
    <LiveInputText @bind-Value="order.Customer.Email" />
    <ZodValidationMessage For="() => order.Customer.Email" ShowCode="true" />
    <ZodValidationSummary GroupByPath="true" />
</EditForm>
```

`Input` debounces changes, so the input has to report one per keystroke. `InputText` commits on `onchange`, and a
component binding has no event to choose (`@bind-Value:event="oninput"` on `InputText` fails at runtime), so derive
from it:

```razor
@* LiveInputText.razor *@
@inherits InputText
<input @attributes="AdditionalAttributes" class="@CssClass" @bind="CurrentValueAsString" @bind:event="oninput" />
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

### Value objects and other struct members

A struct cannot be a `FieldIdentifier` model (Blazor rejects a value-type model, and a boxed copy is not the instance
the form binds), so an error located inside a struct member is placed on the nearest reference-type owner and its
member. Given a scalar value object on the form model:

```csharp
[Scalar]
[ZodSchema]
public readonly partial record struct EmailAddress
{
    [Required]
    [EmailAddress]
    public string Value { get; }
}

[ZodSchema]
public sealed class Registration
{
    public EmailAddress Email { get; set; }
}
```

the error at `Email.Value` is shown by `<ValidationMessage For="() => model.Email" />` (or `ZodValidationMessage`),
next to the value object's input. Both routes, both hosts (Server and WebAssembly) and `AddZodProblemDetails` place
it the same way; `GetZodErrors(() => model.Email)` returns it, with its full path `Email.Value` unchanged. A struct
inside a collection (`Stops[1].Latitude`) is placed on the collection member (`Stops`).

A scalar value object's own member is an implementation detail, so its messages use the **owning member's display
name**: `"Email is required"`, `"Field 'Contact email' must be a valid email address."` with
`[Display(Name = "Contact email")]`, rather than `"Value is required"`. The `displayName` parameter carries the
owner's name too; `member` and `declaringType` still identify the value object's member, for localization. A rule's
own message text (one not produced from its template) is kept. A struct that is not a scalar keeps its members'
names (`"Field 'Latitude' must be greater than or equal to -90 and less than or equal to 90."`), since it may have several.
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
  (minimal APIs), that sink stores errors on the current HTTP request only and discards writes made from an
  interactive circuit, so neither route accumulates anything for the life of a circuit.
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

## Sample

`src/src/Examples.Blazor` (server) and `src/src/Examples.Blazor.Client` (WebAssembly) are a Blazor Web App with every
render mode enabled. Both validate the `[ZodSchema]` models of `src/src/Examples.Shared` - the same types
`Examples.MinimalApi` binds - and both `Program.cs` files register them the same way:

```csharp
builder.Services.AddZodSharpZodSharpExamplesSharedSchemas();   // generated, trim-safe
builder.Services.AddZodSharpBlazor();
```

| Page | Render mode | Shows |
| --- | --- | --- |
| `/orders` | Interactive Server | Route 1: `<DataAnnotationsValidator />` over `CreateOrderRequest` - a nested `ShippingAddress`, a list of `Lines` and a dictionary of `AdditionalAddresses` - with field-level validation and each message next to its input. |
| `/register` | Interactive WebAssembly | Route 2: `<ZodValidator ValidateOn="Change \| Input" DebounceMilliseconds="300">`, `ZodValidationMessage ShowCode`, the service-aware async email-uniqueness rule (its registry calls the server from the browser) with a pending indicator from `IsValidationPending`, and a button that posts without validating so the server's ProblemDetails are mapped back with `AddZodProblemDetails`. |
| `/returns` | Static SSR | `FormName` + `[SupplyParameterFromForm]`, server validation of the posted form, and browser-side checks of `[StartsWith]` and `[UUID]` from `AddZodSharpClientValidation()`. |

The server also exposes the minimal API the WebAssembly form posts to (`POST /api/customers`, validated by the .NET 11
pipeline because the server also calls `AddZodSharpValidation()`) and a culture switch (`/culture/set?culture=fr`). One
`StringLocalizerZodMessageFormatter` over `SampleMessages.fr.resx` localizes the ProblemDetails, both form routes and
the WebAssembly client, which adopts the server-rendered `<html lang>` at startup so `RunAsync` loads the French
satellite assembly.

```bash
dotnet run --project src/src/Examples.Blazor
just wasm-size          # trimmed WebAssembly publish (fails on IL2xxx/IL3xxx) + the size budget
```

### Verification

- `Examples.Blazor.IntegrationTests` hosts the sample in memory (`WebApplicationFactory`): the static-SSR page's
  `data-rules` carry `zod-startswith` and `zod-uuid`, an invalid nested and collection POST re-renders each message
  next to its input (AngleSharp), and `POST /api/customers` returns the ZodValidationProblemDetails contract,
  localized for `Accept-Language: fr`.
- The `wasm-trimmed` PR job publishes the client trimmed with `-warnaserror` and checks the Brotli size of each
  `Purview.ZodSharp*` assembly against `.config/wasm-size-baseline.json` (more than 10% growth fails;
  `scripts/check-wasm-size.ps1 -Update` rewrites the baseline after an intended change).
- The nightly `wasm-nightly.yml` workflow publishes the client with `RunAOTCompilation=true` (the `wasm-tools`
  workload) and runs `tests/e2e/blazor-sample.e2e.mjs` against the published sample in Chromium.
- The client does not reference Microsoft.Extensions.Validation directly and has no `[ValidatableType]`; nested,
  collection and dictionary validation still works in the browser because the ZodSharp resolver supplies the type
  info.
- In a trimmed or AOT publish the `ZodSharp.Reflection.IsSupported` feature switch is off. Generated `[ZodSchema]`
  models place every error exactly; a runtime schema's errors attach to the nearest object the trimmed app can still
  resolve.

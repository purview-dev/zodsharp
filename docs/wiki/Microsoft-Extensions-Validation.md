# Microsoft.Extensions.Validation

`Purview.ZodSharp.Extensions.Validation` connects ZodSharp to .NET 11's
[Microsoft.Extensions.Validation](https://www.nuget.org/packages/Microsoft.Extensions.Validation), the validation
pipeline that minimal APIs, Blazor Server and Blazor WebAssembly share. It registers ZodSharp as a validation
resolver, so every type that has a ZodSharp validator is validated by ZodSharp wherever the platform validates.
That includes nested objects, collections and dictionaries, and it works without `[ValidatableType]` or the
Microsoft.Extensions.Validation source generator.

- Targets `net11.0` only.
- References the `Microsoft.Extensions.Validation` package, never the ASP.NET Core shared framework, so it works in
  a Blazor WebAssembly client and in worker or console hosts.
- `Purview.ZodSharp.AspNetCore` depends on it on `net11.0`. That package turns the pipeline's response into the full
  ZodSharp ProblemDetails contract (see [ASP.NET Core Integration](AspNetCore-Integration.md)).

## Install

```bash
dotnet add package Purview.ZodSharp.Extensions.Validation
```

## Register

```csharp
using ZodSharp.Core;

builder.Services.AddZodSharpFactory(factory => factory.Register(new OrderSchemaValidator()));
builder.Services.AddZodSharpValidation();

// Keep calling AddValidation() in the application when DataAnnotations types should be validated too:
// the Microsoft.Extensions.Validation generator only intercepts calls in your own code.
builder.Services.AddValidation();
```

`AddZodSharpValidation(Action<ZodValidationOptions>? configure = null)` does the following:

- Registers `IZodSchemaFactory` when none is registered yet, as `AddZodSharpFactory()` does. Any registration
  works: `AddZodSharpFactory`, `AddZodSharp` from the ASP.NET Core package, or the generated
  `AddZodSharp{Assembly}Schemas()` (see [Source Generator](Source-Generator.md)).
- Calls `AddValidation()`.
- Registers `ZodValidatableInfoResolver` as a singleton.
- Places the resolver in `ValidationOptions.Resolvers` from a post-configure step. The generated
  DataAnnotations resolver inserts itself at index 0 inside its own configure step, so placing ZodSharp afterwards
  keeps it where `Placement` says. It does not matter whether `AddValidation()` is called before
  `AddZodSharpValidation()`, after it, or not at all.

Calling `AddZodSharpValidation()` more than once registers the resolver once, and every `configure` callback is
applied.

### Endpoint validation

The endpoint filter Microsoft.Extensions.Validation adds to minimal APIs validates every handler parameter that a
registered resolver claims, before the handler runs; it is not switched on by `AddValidation()` itself (called from a
library, `AddValidation()` adds no resolver at all). So `AddZodSharpValidation()` **turns on minimal-API validation
for every ZodSharp-typed parameter**: an invalid body is answered with `400` and the handler never runs. That is
usually the point, but it applies to the whole application:

- Opt an endpoint or a route group out with `DisableValidation()`.
- Register type validation only with `AddZodSharpValidation(configure, validateParameters: false)`, or set
  `ZodValidationOptions.ValidateParameters = false`: Blazor forms and `ValidationOptions.ValidateAsync<T>` still use
  ZodSharp, and no endpoint changes. This is what `AddZodSharpBlazor()` registers.
- Any registration that asks for parameters wins (so `AddZodSharpBlazor()` plus `AddZodSharpValidation()`, in either
  order, validates endpoints), and an explicit `ValidateParameters` overrides both.

> [!IMPORTANT]
> Register every validator before the first validation. The resolver caches lookups per type, including misses, so
> a validator registered with the factory after its type was first looked up is never seen.

## Resolver ordering and DataAnnotations

A type is looked up in `ZodValidationOptions.Sources` first, then in `IZodSchemaFactory`. A type found in neither
falls through to the next resolver, which is usually the generated DataAnnotations resolver.

| `Placement` | Effect |
|---|---|
| `ZodResolverPlacement.First` (default) | ZodSharp runs ahead of the generated DataAnnotations resolvers, so a type with a ZodSharp validator is always validated by ZodSharp. |
| `ZodResolverPlacement.AfterGenerated` | ZodSharp is placed after the generated type resolvers and ahead of the runtime parameter resolvers they append. A type the Microsoft.Extensions.Validation generator knows is validated with DataAnnotations, and ZodSharp handles the rest. |

Generated `[ZodSchema]` validators already apply DataAnnotations attributes, so a ZodSharp type is validated
by ZodSharp only. Set `AlsoRunDataAnnotations = true` to also run the next resolver that knows the type: ZodSharp
runs first, and an identical `(Path, ErrorMessage)` pair is reported once. Nested ZodSharp types are not
validated twice.

## Options

| `ZodValidationOptions` | Default | Effect |
|---|---|---|
| `Placement` | `First` | Where the resolver sits; see above. |
| `AlsoRunDataAnnotations` | `false` | Also run the next resolver for ZodSharp types, reporting identical messages once. |
| `PathNamer` | `null` (C# member names) | Renames member segments in `ValidationError.Path`, for example `ZodPathNamer.FromFunc(JsonNamingPolicy.CamelCase.ConvertName)`. Blazor ignores `Path`, so this never affects field placement. |
| `MessageFormatter` | `null` | Formats every `ErrorMessage`. When `null`, a `StringLocalizerZodMessageFormatter` is used whenever the validation's service provider resolves an `IStringLocalizerFactory`, and the ZodSharp message otherwise. See [Localization](Localization.md). |
| `Sources` | empty | Trim-safe `IZodValidatableInfoSource`s consulted before `IZodSchemaFactory`. |
| `ValidateParameters` | `null` (by registration) | Whether the resolver claims parameters, which turns on minimal-API endpoint validation for ZodSharp types; see [Endpoint validation](#endpoint-validation). `null`: claimed after `AddZodSharpValidation()`, not when only `AddZodSharpBlazor()` registered the bridge. |

```csharp
using System.Text.Json;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

builder.Services.AddZodSharpValidation(options =>
{
    options.Placement = ZodResolverPlacement.First;
    options.PathNamer = ZodPathNamer.FromFunc(JsonNamingPolicy.CamelCase.ConvertName);
});
```

## What each error carries

Each ZodSharp error becomes one `Microsoft.Extensions.Validation.ValidationError`:

| `ValidationError` | Value |
|---|---|
| `Path` | The canonical ZodSharp path prefixed with `ValidateContext.CurrentValidationPath`: `Customer.Address.Street`, `Lines[0].Sku`, `ByCode[abc].Sku`. See [Core Concepts](Core-Concepts.md#paths). |
| `Name` | The failing member's name (`Street`); empty for a model-level error. |
| `Container` | The object that owns the member (`order.Customer.Address`). Blazor uses it to place the message on the right field. Never a struct: an error inside a struct member (`Email.Value`) is attributed to the nearest reference-type owner and its member (`signUp`, `Email`), because a value type cannot be a `FieldIdentifier` model. |
| `ErrorMessage` | The ZodSharp message, formatted by `MessageFormatter` or localized (see [Localization](Localization.md)). |

`Container` and `Name` come from the generated `TryResolveMember`, so no reflection is needed. Validators that are
not generated fall back to a property walker. That walk runs only when validation fails, and it never throws.

Parameters follow Microsoft.Extensions.Validation's own conventions:

- A complex parameter is not prefixed (`Name`, `Address.Street`).
- A collection parameter is prefixed with the parameter name (`items[0].Name`).
- An error about the value itself (a missing body, a root refinement) is keyed by the parameter name and has no
  container.

A nullable or optional parameter that is `null` is not validated. `[SkipValidation]` and `[FromServices]`
parameters and primitive types are never claimed by ZodSharp.

## Full-fidelity errors

A Microsoft.Extensions.Validation error holds only a name, path, message and container. The full ZodSharp errors
(code, category, origin, bounds, parameters) travel alongside on one of two side channels:

- **`ValidateContext.ZodReport`** returns the `ZodValidationReport` attached to the context, or `null` when
  ZodSharp reported nothing. The report is held weakly, so it lives exactly as long as the context.
- **`IZodValidationReportSink`** is used when the validation's service provider resolves one. The bridge appends
  every validated value's errors to it as well. Register it with a lifetime equal to one validation run, which is
  scoped in ASP.NET Core. `AddZodSharpProblemDetails()` registers one (`ZodRequestValidationReport`) on
  `net11.0`. Do not register a sink in Blazor Server, where the scope is the circuit and errors would accumulate.

```csharp
using Microsoft.Extensions.Validation;
using ZodSharp.Extensions.Validation;

ValidateContext context = new() { ValidationOptions = options, ServiceProvider = services };
await typeInfo.ValidateAsync(order, context, cancellationToken);

ZodValidationReport? report = context.ZodReport;
```

Every error in a report has a canonical `FullPath` that already includes `CurrentValidationPath`. It therefore
renders to the same key as the matching `Microsoft.Extensions.Validation.ValidationError.Path`.

When ZodSharp runs nested under a DataAnnotations type whose sibling members complete asynchronously, the platform
validates those members on cloned contexts and merges back only its own errors. The `ZodReport` can then be on a
clone. Register a sink where that matters. When ZodSharp validates the root value (minimal API parameters, Blazor
forms of ZodSharp models), the report is always on the caller's context.

## Validating outside HTTP and Blazor

`ValidationOptions.ValidateAsync<T>(value, services, cancellationToken)` validates through the configured resolvers
in worker services, console hosts and tests, and returns a `ZodValidationReport`:

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

var options = serviceProvider.GetRequiredService<IOptions<ValidationOptions>>().Value;
var report = await options.ValidateAsync(order, serviceProvider, cancellationToken);

foreach (var error in report.Errors)
    Console.WriteLine($"{error.FullPath.ToDottedString()}: {error.Message} ({error.Code})");
```

The runtime type of `value` is used when it differs from `T`. ZodSharp errors keep full fidelity. Errors from other
resolvers (DataAnnotations) are converted with code and category `custom`, and their path is parsed from the
Microsoft.Extensions.Validation key with `ZodPath.ParseDotted`.

> [!NOTE]
> `ZodSharp.Core.ValidationError` and `Microsoft.Extensions.Validation.ValidationError` have the same simple name.
> In a file that imports both namespaces, qualify one or add an alias such as
> `using ZodValidationError = ZodSharp.Core.ValidationError;`.

## Synchronous and asynchronous validation

The bridge behaves like the platform's own type info:

- **Sync with async rules throws.** Synchronous validation of a type with asynchronous rules throws
  `InvalidOperationException`, as `EditContext.Validate()` does. Use the async APIs: minimal APIs already do, and in
  Blazor use `EditContext.ValidateAsync()`. A type has asynchronous rules when its `RequiresAsyncValidation` is
  `true`.
- **Async rules can use services.** Asynchronous validation passes `ValidateContext.ServiceProvider` through to
  service-aware rules (see [Source Generator](Source-Generator.md)).
- **Field-level validation does not recurse.** It runs only the changed member's own rules (`ValidateMember`).
  Nested schemas, type-level rules and refinements are not run, because Blazor attributes every error of a field
  run to the changed field. Members without rules are not offered for field-level validation
  (`IsValidatableMember`).

## Depth limit

The adapter throws `InvalidOperationException` when `ValidateContext.CurrentDepth` reaches
`ValidationOptions.MaxDepth`, like Microsoft.Extensions.Validation. Generated validators also recurse internally with
their own guard, `ZodValidationDepth.MaxDepth` (default `32`). The bridge sets that value from
`ValidationOptions.MaxDepth`, so a cyclic graph fails at the same depth however it is validated.

## Unions

A C# union that has no validator of its own is validated through its active case, the way
Microsoft.Extensions.Validation does. The case is reported under `Value` (`Value.Name`, `Value[0].Name`) and
validated by whichever resolver knows the case type. For a union minimal-API parameter, the runtime parameter
resolver that the application's own `AddValidation()` call adds claims the parameter. See
[Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md).

## Trim-safe sources

`ZodValidationOptions.Sources` supplies validators by type without the factory:

```csharp
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

sealed class OrderValidators : IZodValidatableInfoSource
{
    public bool TryGetValidator(Type type, out IZodUntypedValidator? validator)
    {
        validator = type == typeof(Order) ? new OrderSchemaValidator() : null;
        return validator is not null;
    }
}

builder.Services.AddZodSharpValidation(options => options.Sources.Add(new OrderValidators()));
```

Generated `{Type}SchemaValidator`s implement `IZodUntypedValidator`. For a hand-written
`IZodSchemaValidator<T>`, wrap it in `new ZodUntypedValidator<T>(validator)`. The generated per-assembly
registration (`AddZodSharp{Assembly}Schemas()`) is also trim-safe. Assembly scanning is not.

## Limitations

- **Runtime dictionary schemas have no owning object.** A runtime `Z.Object()` schema validating a dictionary or
  JSON value has no CLR object a field can be bound to. Its errors carry `Container = null` and the full path as
  `Name`, so Blazor shows them in the validation summary rather than next to a field.
- **Models declared in `.razor` files are not generated.** The Razor compiler generates C# from `@code` blocks
  after source generators run, so the ZodSharp generator never sees a `[ZodSchema]` type declared there. The
  generator reports `ZODSGEN051`; move the type to a `.cs` file (see
  [Source Generator Diagnostics](Source-Generator-Diagnostics.md)).
- **Lookups are cached.** Register validators before the first validation (see above).

## See also

- [ASP.NET Core Integration](AspNetCore-Integration.md): the minimal-API pipeline and the ProblemDetails contract.
- [Blazor Integration](Blazor-Integration.md): forms, field placement and `<ZodValidator />`.
- [OpenAPI Integration](OpenAPI-Integration.md)
- [Localization](Localization.md)
- [Core Concepts](Core-Concepts.md#error-fields): the error fields and the canonical path format.

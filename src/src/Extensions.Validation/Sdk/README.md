# Purview.ZodSharp.Extensions.Validation

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.Extensions.Validation.svg)](https://www.nuget.org/packages/Purview.ZodSharp.Extensions.Validation)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

[Microsoft.Extensions.Validation](https://www.nuget.org/packages/Microsoft.Extensions.Validation) integration for
[Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp). It registers ZodSharp as the first .NET 11
validation resolver, so **minimal APIs, Blazor Server and Blazor WebAssembly** validate every type that has a
ZodSharp validator with ZodSharp - nested objects, collections and dictionaries included - without
`[ValidatableType]` and without the Microsoft.Extensions.Validation source generator.

Targets `net11.0`. It references the `Microsoft.Extensions.Validation` package, never the ASP.NET Core shared
framework, so it works in a Blazor WebAssembly client and in worker or console hosts.

## Installation

```bash
dotnet add package Purview.ZodSharp.Extensions.Validation
```

## Usage

```csharp
builder.Services.AddZodSharpFactory(factory => factory.Register(new OrderSchemaValidator()));
builder.Services.AddZodSharpValidation();

// Keep calling AddValidation() in the application when DataAnnotations types should be validated too:
// the Microsoft.Extensions.Validation generator only intercepts calls in your own code.
builder.Services.AddValidation();
```

ZodSharp's resolver ends up first whether `AddValidation()` is called before `AddZodSharpValidation()`, after it,
or not at all. Types without a ZodSharp validator fall through to the next resolver.

For trimmed and Native AOT apps (including Blazor WebAssembly), register the generated validators with the
per-assembly extension the source generator emits instead of scanning: `builder.Services.AddZodSharpMyAppSchemas();`
(`AddZodSharp{Assembly}Schemas()`, the assembly name in PascalCase). `[ValidatableType]` is not needed on a
`[ZodSchema]` type, and is reported as redundant (`ZODSGEN047`).

### What each error carries

| `ValidationError` | Value |
| --- | --- |
| `Path` | The ZodSharp path, prefixed with `ValidateContext.CurrentValidationPath`: `Customer.Address.Street`, `Lines[0].Sku`, `ByCode[abc].Sku` |
| `Name` | The failing member's name (`Street`); empty for a model-level error |
| `Container` | The object that owns the member (`order.Customer.Address`), which is what Blazor uses to place the message on the right field |
| `ErrorMessage` | The ZodSharp message, localized when an `IStringLocalizerFactory` is registered (see below) |

Runtime `Z.Object()` schemas over dictionaries have no owning object, so their errors carry `Container = null`
and the full path as `Name`: Blazor shows them in the validation summary.

Parameters follow Microsoft.Extensions.Validation's conventions: a complex parameter is not prefixed, a
collection parameter is (`items[0].Name`), and an error about the value itself is keyed by the parameter name.

### Full-fidelity errors

Microsoft.Extensions.Validation keeps only a name, path, message and container. The full ZodSharp errors - code,
category, origin, bounds and parameters - are available alongside:

```csharp
ValidateContext context = new() { ValidationOptions = options, ServiceProvider = services };
await typeInfo.ValidateAsync(order, context, cancellationToken);

ZodValidationReport? report = context.ZodReport;
```

Register an `IZodValidationReportSink` with a per-run lifetime (a scoped service in ASP.NET Core) to receive the
same errors where the `ValidateContext` is created for you. Do not register one in Blazor Server, where the scope is
the circuit. Outside HTTP and Blazor, `ValidationOptions.ValidateAsync(value, services)` validates and returns the
report directly:

```csharp
var options = services.GetRequiredService<IOptions<ValidationOptions>>().Value;
ZodValidationReport report = await options.ValidateAsync(order, services, cancellationToken);

foreach (var error in report.Errors)
    Console.WriteLine($"{error.FullPath.ToDottedString()}: {error.Message} ({error.Code})");
```

Errors from other resolvers (DataAnnotations types) are included with code `custom`, their path parsed from the
Microsoft.Extensions.Validation key.

### Localization

When the validation's service provider resolves an `IStringLocalizerFactory`, messages are localized by
`StringLocalizerZodMessageFormatter`, honouring `ValidationOptions.LocalizerProvider`. Keys are tried in order:
the message template, `{Type}_{Member}_{Code}`, `{Type}_{Code}`, `{Code}`; otherwise the ZodSharp message is
kept. Set `ZodValidationOptions.MessageFormatter` to replace it.

### Options

| Option | Default | Effect |
| --- | --- | --- |
| `Placement` | `First` | `AfterGenerated` places ZodSharp after the generated DataAnnotations resolvers |
| `AlsoRunDataAnnotations` | `false` | Also runs the next resolver for ZodSharp types, reporting identical messages once |
| `PathNamer` | `null` (C# names) | Renames members in `Path`, for example to camelCase |
| `MessageFormatter` | `null` | Formats every message |
| `Sources` | empty | Trim-safe validator sources consulted before `IZodSchemaFactory` |

Register every validator before the first validation: lookups are cached, including misses. Synchronous
validation of a type with asynchronous rules throws, as `EditContext.Validate()` does; use the async APIs.

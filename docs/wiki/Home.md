# Purview.ZodSharp Wiki

Purview.ZodSharp is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It uses struct-based rules and `Span<T>` to minimise allocations, ships a fluent API that mirrors Zod, exports and imports JSON Schema, and includes a compile-time source generator for maximum performance.

This wiki is the documentation hub for the core API, the source generator, the JSON and hosting integrations, and the cross-platform TypeScript tooling. Packages are published under the `Purview.*` package IDs.

## How this wiki is organised

The wiki is one folder per topic area, and seven sections, in reading order. The sidebar carries the same sections in the same order, and `_Sidebar.md` is what the published documentation site reads for page order - a page that is not listed there has no place in the sidebar, so new pages are added to it as part of adding the page. A page's folder is part of its published URL (`schemas/Custom-Rules.md` is `/docs/zodsharp/schemas/custom-rules/`), so a page moves folder only when it changes topic.

| Section | Where it lives |
| --- | --- |
| Get started | the wiki root and `concepts/` |
| Validation reference | `schemas/`, plus `concepts/Composition-and-Transforms.md` |
| Source generator | `generator/` |
| JSON and serialization | `serialization/` |
| ASP.NET Core, Blazor and .NET 11 | `hosting/`, plus the root-level `AspNetCore-Integration.md` |
| Guarantees, performance and deployment | `quality/` |
| Contributing and releases | `development/` |

Three pages stay at the wiki root: `Getting-Started.md`, which the site publishes as the documentation landing page, and `Fluent-Schema-API.md` and `AspNetCore-Integration.md`, which the site's project catalogue deep-links by slug. A deep link is a single path segment, so those two keep root-level URLs; see [Contributing](development/Contributing.md).

## Get started

Read these first; they are the vocabulary the reference pages assume. `Getting-Started.md` and `Fluent-Schema-API.md` sit at the wiki root, `Core-Concepts.md` in `concepts/`.

| Page | What it answers |
| --- | --- |
| [Getting Started](Getting-Started.md) | Which packages to install, and a first schema, object, generated validator and JSON round-trip. |
| [Core Concepts](concepts/Core-Concepts.md) | The two-phase pipeline, `ValidationResult`, the error model (codes, categories, paths, bounds, parameters), and coercion. |
| [Fluent Schema API](Fluent-Schema-API.md) | The `Z` factory, and the methods every schema type exposes. |

## Validation reference

`schemas/` holds one page per schema family and the rule catalogue; `concepts/Composition-and-Transforms.md` covers composition. Start with the page for the type you are validating; the rules themselves are all listed in the reference.

| Page | What it answers |
| --- | --- |
| [String Validation](schemas/String-Validation.md) | `ZodString`: length bounds, formats, regex, trimming and casing. |
| [Number Validation](schemas/Number-Validation.md) | `ZodNumber` and `ZodBigInt`: bounds, integer/safe/finite checks, multiples and sign. |
| [Object Validation](schemas/Object-Validation.md) | `ZodObject` and `ZodObjectBuilder`: fields, optional/strict/catchall, dictionary input. |
| [Arrays and Other Schemas](schemas/Arrays-and-Other-Schemas.md) | Arrays, tuples, records, maps, literals, enums and dates. |
| [Unions and Discriminated Unions](schemas/Unions-and-Discriminated-Unions.md) | `Z.Union`, discriminated unions, `Z.NativeUnion` and exhaustive matching. |
| [Composition and Transforms](concepts/Composition-and-Transforms.md) | `And`/`Or`/`Refine`/`Transform`/`Coerce`, and the value-first `Apply*` methods. |
| [Validation Rules Reference](schemas/Validation-Rules-Reference.md) | Every built-in rule, with its error code, message template and parameters. |
| [Custom Rules](schemas/Custom-Rules.md) | Writing a rule, exposing it as an attribute, extending the fluent interface, scalar value objects. |
| [Value Objects Integration](schemas/Value-Objects-Integration.md) | Validating a `Purview.ValueObjects` `[Scalar]` as a unit, with an empty failure path. |
| [Compiled Validators and Caching](schemas/Compiled-Validators-and-Caching.md) | Cached delegates for hot paths. |

## Source generator

`generator/` - `[ZodSchema]` turns a class, struct or record into a static, zero-allocation validator at compile time. Read the overview first; the other two pages are references you will come back to.

| Page | What it answers |
| --- | --- |
| [Source Generator](generator/Source-Generator.md) | What the generator emits (`Validate`/`Parse`, `Paths`, `JsonNames`, trim-safe registration, the async graph), its options and hooks. |
| [Source Generator DataAnnotations](generator/Source-Generator-DataAnnotations.md) | Which `System.ComponentModel.DataAnnotations` attributes map to which rules. |
| [Source Generator Diagnostics](generator/Source-Generator-Diagnostics.md) | Every `ZODSGEN` diagnostic: severity, cause and fix. |

## JSON and serialization

`serialization/` - JSON Schema is the interchange format with TypeScript/Zod; the two library packages add deserialize-and-validate on top of it. Export lives in the core package, import in whichever JSON package you reference.

| Page | What it answers |
| --- | --- |
| [JSON Schema Export](serialization/JsonSchema-Export.md) | `Z.ToJsonSchema`, `ToJsonSchemaOptions`, dictionaries, generated schemas and the temporal wire forms. |
| [JSON Schema Import](serialization/JsonSchema-Import.md) | `Z.FromJsonSchema`, and which package provides it. |
| [System.Text.Json Integration](serialization/SystemTextJson-Integration.md) | Deserialize-and-validate, validating converters, generated serialization contexts. |
| [Newtonsoft.Json Integration](serialization/NewtonsoftJson-Integration.md) | The same surface for Newtonsoft.Json. |

## ASP.NET Core, Blazor and .NET 11

`hosting/`, plus the root-level `AspNetCore-Integration.md` - everything that plugs ZodSharp into a host. On `net11.0` the platform's own validation runs through ZodSharp, so minimal APIs and Blazor report the same error contract as direct calls.

| Page | What it answers |
| --- | --- |
| [ASP.NET Core Integration](AspNetCore-Integration.md) | ProblemDetails, minimal-API validation in both modes, `IZodSchemaFactory` registration. |
| [Microsoft.Extensions.Validation](hosting/Microsoft-Extensions-Validation.md) | The .NET 11 bridge that makes minimal APIs and Blazor validate with ZodSharp. |
| [OpenAPI Integration](hosting/OpenAPI-Integration.md) | Transformers that describe ZodSharp's constraints and the error contract in OpenAPI 3.1 and 3.2. |
| [Blazor Integration](hosting/Blazor-Integration.md) | `ZodValidator`, message components, server errors onto fields, static-SSR client rules. |
| [HttpClient Integration](hosting/Http-Integration.md) | Validated request and response bodies, and reading validation problems as ZodSharp errors. |
| [Dependency Injection](hosting/Dependency-Injection.md) | Resolving validators through `IZodSchemaFactory`, and `IValidateOptions<T>`. |
| [Localization](hosting/Localization.md) | Message formatting, `IStringLocalizer` lookup, culture and PII. |

## Guarantees, performance and deployment

`quality/` - the limits of the performance claims, and what the publish gates prove before a package ships.

| Page | What it answers |
| --- | --- |
| [Guarantees and Limitations](quality/Guarantees-and-Limitations.md) | What is guaranteed (allocation-free valid paths, trim safety) and what is not. |
| [Performance](quality/Performance.md) | The BenchmarkDotNet suite and the committed measurements. |
| [Trimming and Native AOT](quality/Trimming-and-AOT.md) | `IsTrimmable`/`IsAotCompatible`, and the rooted and consumer publish checks. |
| [Cross-Platform Interop](quality/Cross-Platform-Interop.md) | The shared TypeScript/Zod fixture pipeline that pins C# behaviour to Zod. |

## Contributing and releases

`development/` - repository conventions (including how this wiki is maintained) and the release flow.

| Page | What it answers |
| --- | --- |
| [Contributing](development/Contributing.md) | Repository layout, commands, the testing bar, and how this wiki is maintained. |
| [Release Flow](development/Release-Flow.md) | The pipeline, its gates and the pre-release checklist. |

## Feature highlights

- **Zero-allocation validation** — validation rules are `readonly record struct`s and hot paths use `Span<T>`; every valid input path validates without allocating.
- **Fluent API** — `Z.String().Min(3).Max(50).Email()`, composable objects, arrays, unions, tuples, records, discriminators, and more.
- **Native C# 15 unions (.NET 11+)** — `Z.NativeUnion<T1, T2>` returns an allocation-free native union for reference-type cases, with exhaustive pattern matching; `Z.Union` stays the zero-allocation choice for value-type cases. The analyzer reports `ZODSGEN041` and offers a code fix.
- **Structured issues** — failures carry a machine-readable `Code`, a Zod-compatible `Category`, a typed `FullPath` (rendered `Items[0].Name`), `Origin`, typed `Lower`/`Upper` bounds, `Parameters` and the `MessageFormat` template, in addition to a human message. See [Core Concepts](concepts/Core-Concepts.md#error-fields) and [Localization](hosting/Localization.md).
- **Test-friendly rule identity** — every rule exposes its code and message template as public `ErrorCode`/`MessageFormat` constants (the analyzer reports `ZODSGEN042` when one is missing), so tests assert against the rule instead of duplicating literals. `NonSentinelRule<T>` rejects the ORM sentinel values (`Guid.Empty`, `DateTime.MinValue`/`MaxValue`, and more).
- **JSON Schema interoperability** — export via `Z.ToJsonSchema` (core package) and import via `Z.FromJsonSchema` (in either JSON integration package), enabling cross-language reuse with TypeScript/Zod.
- **Compile-time source generation** — the `[ZodSchema]` attribute turns a class, struct, or record into a zero-allocation static validator, honouring DataAnnotations attributes such as `[Required]`, `[Length]`, `[Range]`, and `[EmailAddress]`.
- **Value object scalars** — a `Purview.ValueObjects` `[Scalar]` can be validated as a unit with `[ZodSchema]`; a rule written against the scalar's underlying value is adapted automatically. See [Value Objects Integration](schemas/Value-Objects-Integration.md).
- **Integration packages** — `Purview.ZodSharp.SystemTextJson`, `Purview.ZodSharp.NewtonsoftJson`, `Purview.ZodSharp.Http` (`HttpClient`, WebAssembly-safe), and `Purview.ZodSharp.AspNetCore` (ProblemDetails); on .NET 11, `Purview.ZodSharp.Extensions.Validation` (minimal APIs and Blazor through Microsoft.Extensions.Validation), `Purview.ZodSharp.AspNetCore.OpenApi` (OpenAPI transformers) and `Purview.ZodSharp.Blazor`.
- **Cross-platform tests** — a shared TypeScript/Zod fixture set is generated into the repo and asserted against from both the C# test suite and a vitest suite.
- **Multi-target** — packages target `net10.0` and `net11.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.

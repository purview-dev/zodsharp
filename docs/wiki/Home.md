# Purview.ZodSharp Wiki

Purview.ZodSharp is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It uses struct-based rules and `Span<T>` to minimise allocations, ships a fluent API that mirrors Zod, exports and imports JSON Schema, and includes a compile-time source generator for maximum performance.

This wiki is the project documentation hub for the core API, source generator, JSON integration packages, and the cross-platform TypeScript tooling. Packages are published under the `Purview.*` package IDs.

## Start here

- [Getting Started](Getting-Started.md)
- [Core Concepts](Core-Concepts.md)
- [Fluent Schema API](Fluent-Schema-API.md)
- [Source Generator](Source-Generator.md)
- [Guarantees and Limitations](Guarantees-and-Limitations.md)
- [Performance](Performance.md)
- [Contributing](Contributing.md)

## Core validation

- [String Validation](String-Validation.md)
- [Number Validation](Number-Validation.md)
- [Object Validation](Object-Validation.md)
- [Arrays and Other Schemas](Arrays-and-Other-Schemas.md)
- [Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md)
- [Composition and Transforms](Composition-and-Transforms.md)
- [Custom Rules](Custom-Rules.md)
- [Validation Rules Reference](Validation-Rules-Reference.md)
- [Value Objects Integration](Value-Objects-Integration.md)
- [Compiled Validators and Caching](Compiled-Validators-and-Caching.md)
- [Dependency Injection](Dependency-Injection.md)

## JSON integration

- [JSON Schema Export](JsonSchema-Export.md)
- [JSON Schema Import](JsonSchema-Import.md)
- [System.Text.Json Integration](SystemTextJson-Integration.md)
- [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md)
- [ASP.NET Core Integration](AspNetCore-Integration.md)

## .NET 11 validation

- [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md): the bridge that makes minimal APIs and Blazor validate with ZodSharp
- [ASP.NET Core Integration](AspNetCore-Integration.md): the ProblemDetails contract and both minimal-API modes
- [OpenAPI Integration](OpenAPI-Integration.md): describing ZodSharp constraints and the error contract in OpenAPI 3.1 and 3.2
- [Blazor Integration](Blazor-Integration.md): forms on Blazor Server, WebAssembly and Auto
- [Localization](Localization.md): message formatting, `IStringLocalizer` lookup, culture and PII

## Source generator

- [Source Generator](Source-Generator.md)
- [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md)
- [Source Generator Diagnostics](Source-Generator-Diagnostics.md)

## Cross-platform and workflow

- [Cross-Platform Interop](Cross-Platform-Interop.md)
- [Performance](Performance.md)
- [Release Flow](Release-Flow.md)
- [Contributing](Contributing.md)

## Feature highlights

- **Zero-allocation validation** — validation rules are `readonly record struct`s and hot paths use `Span<T>`; every valid input path validates without allocating.
- **Fluent API** — `Z.String().Min(3).Max(50).Email()`, composable objects, arrays, unions, tuples, records, discriminators, and more.
- **Native C# 15 unions (.NET 11+)** — `Z.NativeUnion<T1, T2>` returns an allocation-free native union for reference-type cases, with exhaustive pattern matching; `Z.Union` stays the zero-allocation choice for value-type cases. The analyzer reports `ZODSGEN041` and offers a code fix.
- **Structured issues** — failures carry a machine-readable `Code`, a Zod-compatible `Category`, a typed `FullPath` (rendered `Items[0].Name`), `Origin`, typed `Lower`/`Upper` bounds, `Parameters` and the `MessageFormat` template, in addition to a human message. See [Core Concepts](Core-Concepts.md#error-fields) and [Localization](Localization.md).
- **Test-friendly rule identity** — every rule exposes its code and message template as public `ErrorCode`/`MessageFormat` constants (the analyzer reports `ZODSGEN042` when one is missing), so tests assert against the rule instead of duplicating literals. `NonSentinelRule<T>` rejects the ORM sentinel values (`Guid.Empty`, `DateTime.MinValue`/`MaxValue`, and more).
- **JSON Schema interoperability** — export via `Z.ToJsonSchema` (core package) and import via `Z.FromJsonSchema` (in either JSON integration package), enabling cross-language reuse with TypeScript/Zod.
- **Compile-time source generation** — the `[ZodSchema]` attribute turns a class, struct, or record into a zero-allocation static validator, honouring DataAnnotations attributes such as `[Required]`, `[Length]`, `[Range]`, and `[EmailAddress]`.
- **Value object scalars** — a `Purview.ValueObjects` `[Scalar]` can be validated as a unit with `[ZodSchema]`; a rule written against the scalar's underlying value is adapted automatically. See [Value Objects Integration](Value-Objects-Integration.md).
- **Integration packages** — `Purview.ZodSharp.SystemTextJson`, `Purview.ZodSharp.NewtonsoftJson`, and `Purview.ZodSharp.AspNetCore` (ProblemDetails); on .NET 11, `Purview.ZodSharp.Extensions.Validation` (minimal APIs and Blazor through Microsoft.Extensions.Validation), `Purview.ZodSharp.AspNetCore.OpenApi` (OpenAPI transformers) and `Purview.ZodSharp.Blazor`.
- **Cross-platform tests** — a shared TypeScript/Zod fixture set is generated into the repo and asserted against from both the C# test suite and a vitest suite.
- **Multi-target** — packages target `net8.0`, `net9.0`, `net10.0`, and `net11.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.
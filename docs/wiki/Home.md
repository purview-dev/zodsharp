# ZodSharp Wiki

ZodSharp is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It uses struct-based rules and `Span<T>` to minimise allocations, ships a fluent API that mirrors Zod, exports and imports JSON Schema, and includes a compile-time source generator for maximum performance.

This wiki is the project documentation hub for the core API, source generator, JSON integration packages, and the cross-platform TypeScript tooling. It is a fork of [guinhx/ZodSharp](https://github.com/guinhx/ZodSharp), maintained under the `Purview.*` package IDs.

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
- [Compiled Validators and Caching](Compiled-Validators-and-Caching.md)
- [Dependency Injection](Dependency-Injection.md)

## JSON integration

- [JSON Schema Export](JsonSchema-Export.md)
- [JSON Schema Import](JsonSchema-Import.md)
- [System.Text.Json Integration](SystemTextJson-Integration.md)
- [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md)
- [ASP.NET Core Integration](AspNetCore-Integration.md)

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
- **Structured issues** — failures carry machine-readable `Code`, `Path`, `Origin`, `Minimum`/`Maximum`, and `Inclusive` metadata in addition to a human message.
- **JSON Schema interoperability** — export via `Z.ToJsonSchema` (core package) and import via `Z.FromJsonSchema` (in either JSON integration package), enabling cross-language reuse with TypeScript/Zod.
- **Compile-time source generation** — the `[ZodSchema]` attribute turns a class, struct, or record into a zero-allocation static validator, honouring DataAnnotations attributes such as `[Required]`, `[Length]`, `[Range]`, and `[EmailAddress]`.
- **Integration packages** — `Purview.ZodSharp.SystemTextJson`, `Purview.ZodSharp.NewtonsoftJson`, and `Purview.ZodSharp.AspNetCore` (ProblemDetails).
- **Cross-platform tests** — a shared TypeScript/Zod fixture set is generated into the repo and asserted against from both the C# test suite and a vitest suite.
- **Multi-target** — packages target `net8.0`, `net9.0`, and `net10.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.
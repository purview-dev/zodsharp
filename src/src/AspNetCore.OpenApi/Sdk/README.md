# Purview.ZodSharp.AspNetCore.OpenApi

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.AspNetCore.OpenApi.svg)](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore.OpenApi)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

OpenAPI integration for [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp): ASP.NET Core 11
OpenAPI transformers that make the document describe exactly what ZodSharp enforces — constraints, formats,
unions, maps and the validation error contract — with one line of setup.

`net11.0` only: ASP.NET Core 11 builds documents on the Microsoft.OpenApi 3.x object model.

## Installation

```bash
dotnet add package Purview.ZodSharp.AspNetCore.OpenApi
```

## Usage

```csharp
builder.Services.AddZodSharp(options => options.ScanAssemblies.Add(typeof(Program).Assembly));
builder.Services.AddOpenApi(options => options.AddZodSharp());

var app = builder.Build();
app.MapOpenApi();
```

Options:

```csharp
builder.Services.AddOpenApi(options => options.AddZodSharp(zod =>
{
    zod.EmitVendorExtensions = true;       // x-zod-* for rules without a JSON Schema keyword
    zod.DiscriminatedUnionsAsOneOf = true; // oneOf + discriminator for polymorphic types
    zod.DescribeValidationProblems = true; // 400 ZodValidationProblemDetails on validated endpoints
}));
```

All three default to `true`.

## What it describes

The source of truth is ZodSharp's own JSON Schema for a type: the generated `BuildJsonSchema()` of a
`[ZodSchema]` type, or a runtime schema registered in `IZodSchemaFactory`. A type ZodSharp does not know is left
exactly as ASP.NET Core produced it.

| ZodSharp | OpenAPI keyword |
| --- | --- |
| `MinLength`/`MaxLength`/`Length`/`StringLength` | `minLength`/`maxLength` (strings), `minItems`/`maxItems` (collections), `minProperties`/`maxProperties` (maps) |
| `MinValue`/`MaxValue`/`GreaterThanOrEqual`/`LessThanOrEqual` | `minimum`/`maximum` |
| `GreaterThan`/`LessThan` | `exclusiveMinimum`/`exclusiveMaximum` |
| `MultipleOf` | `multipleOf` |
| `Int` | `type: integer` |
| `SafeInteger` | `type: integer` with ZodSharp's safe range |
| `Regex`/`RegularExpression` | `pattern` (several become `allOf`) |
| `StartsWith`/`EndsWith`/`Includes` | `pattern`, plus `x-zod-startsWith`/`x-zod-endsWith`/`x-zod-includes` |
| `Email`/`EmailAddress` | `format: email` |
| `Url` | `format: uri` |
| `UUID(version)` | `format: uuid`, a version pattern and `x-zod-uuidVersion` |
| `IPAddress(IPv4/IPv6)` | `format: ipv4`/`ipv6` |
| `Cidr`, `JWT` | `x-zod-cidr`, `x-zod-jwt` (listed under `x-zodsharp-unsupported`) |
| `DatetimeString`/`DateString` | `format: date-time` (with the UTC pattern)/`format: date` |
| `TimeString` | a local-time `pattern` and `x-zod-format: time` |
| `Duration` | `format: duration` |
| `Base64String` | `contentEncoding: base64` |
| `AllowedValues`/enum members | `enum` |
| `DeniedValues` | `not: { enum: [...] }` |
| `[Display(Description)]` | `description` |

Members ZodSharp rejects when absent (non-nullable references, `[Required]`, `[RequiredZod]`) are listed in
`required`, and nullability uses `type: [x, "null"]`. When a DataAnnotation the platform mapped disagrees with
ZodSharp, ZodSharp's value is written — it is the validator that enforces the request — and in Development the
override is logged at `Debug`.

Polymorphic types (`[JsonPolymorphic]`) are described with `oneOf` and the platform's `discriminator`. A C# union
keeps the platform's `anyOf`; ZodSharp only constrains each case.

## Validation problem responses

Every operation that binds a ZodSharp-validated parameter (a body, an `[AsParameters]` container, or a union of
ZodSharp cases) lists `400` — and `422` when an `ErrorType` registered in the `ErrorTypeRegistry` uses it — as
`application/problem+json` with the shared `ZodValidationProblemDetails` and `ZodValidationIssue` component
schemas. A `400` the endpoint already declares with `ProducesValidationProblem()` is merged into rather than
duplicated.

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.AspNetCore.OpenApi` on NuGet.

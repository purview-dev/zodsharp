# OpenAPI Integration

`Purview.ZodSharp.AspNetCore.OpenApi` adds ASP.NET Core 11 OpenAPI transformers so the generated document
describes exactly what ZodSharp enforces: constraints, formats, unions, maps, required members and the validation
error contract.

- `net11.0` only. ASP.NET Core 11 builds documents on the Microsoft.OpenApi 3.x object model.
- Kept separate from `Purview.ZodSharp.AspNetCore`, so the core ASP.NET Core package never forces a Microsoft.OpenApi
  dependency.

## Install

```bash
dotnet add package Purview.ZodSharp.AspNetCore.OpenApi
```

## Register

```csharp
using Microsoft.AspNetCore.OpenApi;

builder.Services.AddZodSharp(options => options.ScanAssemblies.Add(typeof(Program).Assembly));
builder.Services.AddOpenApi(options => options.AddZodSharp());

var app = builder.Build();
app.MapOpenApi();
```

`AddZodSharp` on `OpenApiOptions` is an extension in the `Microsoft.AspNetCore.OpenApi` namespace. Web projects
do not import that namespace implicitly, so add the `using`.

It registers three transformers:

| Transformer | Role |
|---|---|
| `ZodOpenApiSchemaTransformer` | Applies ZodSharp's constraints, required members, nullability and union shape to each schema. |
| `ZodOpenApiOperationTransformer` | Adds the validation problem responses to operations that bind a ZodSharp-validated parameter. |
| `ZodOpenApiDocumentTransformer` | Adds the shared `ZodValidationProblemDetails` and `ZodValidationIssue` component schemas, and rewrites marked unions from `anyOf` to `oneOf`. |

## Options

```csharp
using Microsoft.AspNetCore.OpenApi;

builder.Services.AddOpenApi(options => options.AddZodSharp(zod =>
{
    zod.EmitVendorExtensions = true;
    zod.DiscriminatedUnionsAsOneOf = true;
    zod.DescribeValidationProblems = true;
}));
```

| `ZodOpenApiOptions` | Default | Effect |
|---|---|---|
| `EmitVendorExtensions` | `true` | Describes rules that have no JSON Schema keyword, or that a keyword only approximates, with `x-zod-*` vendor extensions, and lists rules that cannot be represented under `x-zodsharp-unsupported`. |
| `DiscriminatedUnionsAsOneOf` | `true` | Describes discriminated unions and polymorphic types with `oneOf` plus a `discriminator` instead of `anyOf`. |
| `DescribeValidationProblems` | `true` | Lists the validation problem responses on operations whose parameters ZodSharp validates. |

The component ids and the media type are constants: `ZodOpenApiOptions.ValidationProblemDetailsSchemaId`
(`ZodValidationProblemDetails`), `ZodOpenApiOptions.ValidationIssueSchemaId` (`ZodValidationIssue`) and
`ZodOpenApiOptions.ProblemJsonContentType` (`application/problem+json`).

## Source of truth

For each type, the transformer uses ZodSharp's own JSON Schema (see [JSON Schema Export](JsonSchema-Export.md)):

- the generated `BuildJsonSchema()` of a `[ZodSchema]` type (no reflection), or
- a runtime schema registered in `IZodSchemaFactory`, converted with `ToJsonSchemaConverter`.

A type ZodSharp does not know is left exactly as ASP.NET Core produced it.

When a DataAnnotation that the platform mapped disagrees with ZodSharp, ZodSharp's value is written, because
ZodSharp is the validator that enforces the request. In Development each override is logged at `Debug`. Identical
values are left alone.

## Keyword mapping

The platform maps a few DataAnnotations by itself (`[Range]`, `[RegularExpression]`, `[MinLength]`, `[MaxLength]`,
`[Length]`, `[StringLength]`, `[Url]`, `[Base64String]`, `[Required]`, `[Description]`). It maps no ZodSharp rule
attribute and not `[EmailAddress]`. With the transformers, the document says:

| ZodSharp rule or attribute | OpenAPI keyword |
|---|---|
| `MinLength`/`MaxLength`/`Length`/`StringLength` on a string | `minLength`/`maxLength` |
| the same on a collection | `minItems`/`maxItems` |
| the same on a map | `minProperties`/`maxProperties` |
| `MinValue`/`MaxValue`/`GreaterThanOrEqual`/`LessThanOrEqual` | `minimum`/`maximum` |
| `GreaterThan`/`LessThan` | `exclusiveMinimum`/`exclusiveMaximum` (numeric, the 3.1+ form) |
| `MultipleOf` | `multipleOf` |
| `Int` | `type: integer` |
| `SafeInteger` | `type: integer`, with `minimum`/`maximum` set to ZodSharp's safe range (`int.MinValue`..`int.MaxValue`) |
| `Regex`/`RegularExpression` | `pattern`; several patterns become `allOf: [{ pattern }, ...]` |
| `StartsWith`/`EndsWith`/`Includes` | an escaped `pattern` (`^ab`, `yz$`, `mid`), plus `x-zod-startsWith`/`x-zod-endsWith`/`x-zod-includes` |
| `Email`/`EmailAddress` | `format: email` |
| `Url` | `format: uri` |
| `UUID(version)` | `format: uuid` and `x-zod-uuidVersion` |
| `IPAddress` (IPv4/IPv6) | `format: ipv4`/`format: ipv6` (an any-version rule records `x-zod-ipVersion`) |
| `Cidr` | `x-zod-cidr` (`v4`, `v6` or `any`), and listed under `x-zodsharp-unsupported` |
| `JWT` | `x-zod-jwt: true`, and listed under `x-zodsharp-unsupported` |
| `DatetimeString` | `format: date-time` |
| `DateString` | `format: date` |
| `TimeString` | a local-time `pattern` and `x-zod-format: time` |
| `Duration` | `format: duration` |
| `Base64String` | `contentEncoding: base64` |
| `AllowedValues`, enum members | `enum` |
| `DeniedValues` | `not: { enum: [...] }` |
| `[Display(Description)]` | `description` (the platform does not map it) |
| `[Required]`, `[RequiredZod]`, non-nullable reference members | listed in the type's `required` |

A `TimeSpan` member keeps the platform's description. System.Text.Json writes a `TimeSpan` in its constant (`c`)
format, which is not an ISO 8601 duration, so `format: duration` is not applied.

An enum member's schema becomes the enum's shared component, so only the enum's own value set is written there.

Nested types, collection elements and map values are constrained through their own component schemas. Constraints
on `[AsParameters]`, query and route parameters of ZodSharp-known types come from the containing type's schema.

## Nullability and required members

Nullability uses the JSON Schema form `type: [x, "null"]`, never the OpenAPI 3.0 `nullable` flag. Members ZodSharp
rejects when absent are added to `required`: non-nullable references, `[Required]` and `[RequiredZod]`. A
`[RequiredZod]` member typed as nullable is described as non-nullable, because ZodSharp rejects `null`.

## Unions and polymorphism

| Shape | Description |
|---|---|
| A C# union | The platform's `anyOf` over the case schemas is kept. ZodSharp only constrains each case it knows and never restructures the union. |
| A polymorphic type (`[JsonPolymorphic]`/`[JsonDerivedType]`, or a closed hierarchy) | `oneOf` with the platform's `discriminator`, because exactly one case matches. |
| A runtime `ZodDiscriminatedUnion` | `oneOf` with a `discriminator` whose `propertyName` is the union's discriminator. |

With `DiscriminatedUnionsAsOneOf = false`, all three keep `anyOf`. See
[Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md).

## Validation problem responses

Every operation that binds a ZodSharp-validated parameter lists `400` as `application/problem+json`, referencing
`#/components/schemas/ZodValidationProblemDetails`. A ZodSharp-validated parameter is a body, an `[AsParameters]`
container, or a C# union whose cases ZodSharp knows. It also lists `422` when an `ErrorType` registered in the
`ErrorTypeRegistry` uses that status.

- Both minimal-API modes are covered: the built-in validation pipeline (`AddZodSharpValidation()`) and the
  `WithZodSharpValidation()` filter.
- Services and special parameters (`HttpContext`, `CancellationToken`, and so on) are skipped.
- A response the endpoint already declares, typically with `ProducesValidationProblem()`, is merged into rather than
  duplicated. Its description is kept, and its `application/problem+json` schema is replaced with the ZodSharp
  contract.

The two component schemas describe the body that [ASP.NET Core Integration](AspNetCore-Integration.md#the-error-contract)
documents:

- `ZodValidationProblemDetails` is RFC 9457 `HttpValidationProblemDetails` plus `issues` (an array of
  `ZodValidationIssue`) and `traceId`.
- `ZodValidationIssue` has these properties:
  - `path`, `code` and `message`, which are required.
  - `segments`, an array of `string | integer`.
  - `category`, `origin` and `inclusive`.
  - `minimum`/`maximum`, each a number or a `date-time` string.
  - `parameters` and `unionErrors`, an array of arrays of `ZodValidationIssue`.

## OpenAPI 3.1 and 3.2

ASP.NET Core 11 writes OpenAPI 3.2 by default. The output is the same for 3.1 and 3.2, and both are covered by the
test suite. To write 3.1:

```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.AddZodSharp();
});
```

The transformers always write the 3.1+ JSON Schema forms: numeric `exclusiveMinimum`/`exclusiveMaximum`, `type`
arrays for nullability, and `contentEncoding`. OpenAPI 3.0 is not a supported target.

## Vendor extensions

With `EmitVendorExtensions` on, rules that no standard keyword captures are recorded as `x-zod-*` extensions:

| Extension | Rule |
|---|---|
| `x-zod-startsWith`, `x-zod-endsWith`, `x-zod-includes` | `StartsWith`, `EndsWith`, `Includes` (the literal; the `pattern` is the escaped form) |
| `x-zod-uuidVersion` | `UUID(version)`, as an integer |
| `x-zod-ipVersion` | an any-version `IPAddress` rule (`any`) |
| `x-zod-cidr` | `Cidr` (`v4`, `v6` or `any`) |
| `x-zod-jwt` | `JWT` |
| `x-zod-format` | `TimeString` (`time`) |
| `x-zodsharp-unsupported` | the names of rules that have no JSON Schema representation (`CidrRule`, `JWTRule`, ...) |

The same extensions are available outside OpenAPI from `JsonSchemaDefinition.GetVendorExtensions()`. They never
change the serialized JSON Schema (see [JSON Schema Export](JsonSchema-Export.md)).

## See also

- [ASP.NET Core Integration](AspNetCore-Integration.md)
- [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md)
- [JSON Schema Export](JsonSchema-Export.md)

# Getting Started

## Install

The packages target `net10.0` and `net11.0` (the .NET 11 integrations target `net11.0` only). Applications on
`net8.0` or `net9.0` stay on the 2.4.x line.

```bash
dotnet add package Purview.ZodSharp
```

Add the integration packages you need:

```bash
# System.Text.Json integration + JSON Schema import
dotnet add package Purview.ZodSharp.SystemTextJson

# Newtonsoft.Json integration + JSON Schema import
dotnet add package Purview.ZodSharp.NewtonsoftJson

# HttpClient integration: validated request and response bodies (WebAssembly-safe)
dotnet add package Purview.ZodSharp.Http

# ASP.NET Core ProblemDetails integration
dotnet add package Purview.ZodSharp.AspNetCore

# .NET 11 only: the platform validation bridge, OpenAPI transformers and Blazor forms
dotnet add package Purview.ZodSharp.Extensions.Validation
dotnet add package Purview.ZodSharp.AspNetCore.OpenApi
dotnet add package Purview.ZodSharp.Blazor
```

- `Purview.ZodSharp` — core library, source generator, and JSON Schema **export**.
- `Purview.ZodSharp.SystemTextJson` — System.Text.Json deserialize-and-validate, validating converters, and JSON Schema **import**.
- `Purview.ZodSharp.NewtonsoftJson` — Newtonsoft.Json deserialize-and-validate, validating converters, and JSON Schema **import**.
- `Purview.ZodSharp.Http` — `HttpClient` integration: validates request and response bodies, and reads a server's validation problem back as ZodSharp errors; trim-, AOT- and WebAssembly-safe.
- `Purview.ZodSharp.AspNetCore` — failed validation results converted to standard `ProblemDetails` / `HttpValidationProblemDetails` payloads.
- `Purview.ZodSharp.Extensions.Validation` (`net11.0`) — registers ZodSharp as the first `Microsoft.Extensions.Validation` resolver, so minimal APIs, Blazor Server and Blazor WebAssembly validate with ZodSharp.
- `Purview.ZodSharp.AspNetCore.OpenApi` (`net11.0`) — OpenAPI transformers that describe the constraints, formats, unions and validation error contract ZodSharp enforces.
- `Purview.ZodSharp.Blazor` (`net11.0`) — `ZodValidator`, message components and static-SSR client rules for Blazor Server, WebAssembly and Auto render mode.

> [!TIP]
> JSON Schema import (`Z.FromJsonSchema`) is provided by whichever JSON integration package you reference, so pick one, and import its JSON Schema namespace (`ZodSharp.JsonSchema.SystemTextJson` or `ZodSharp.JsonSchema.NewtonsoftJson`). Export (`Z.ToJsonSchema`) lives in the core package.

## First schema

```csharp
using ZodSharp;

var nameSchema = Z.String().Min(3).Max(50);
var result = nameSchema.Validate("John");

if (result.IsSuccess)
    Console.WriteLine($"Valid name: {result.Value}");
```

## Validate, SafeParse, and Parse

- `Validate` returns a `ValidationResult<T>` — no exceptions.
- `SafeParse` is an alias of `Validate`.
- `Parse` throws `ZodException` on failure.

```csharp
var value = nameSchema.Parse("AB"); // throws ZodException

var result = nameSchema.SafeParse("AB"); // non-throwing
if (!result.IsSuccess)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"  - {error.FullPath.ToDottedString()}: {error.Message}");
}
```

## Object validation

```csharp
var userSchema = Z.Object()
    .Field("name", Z.String().Min(1))
    .Field("age", Z.Number().Min(0).Max(120).Int())
    .Field("email", Z.String().Email())
    .Build();

var userData = new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "age", 30.0 },
    { "email", "john@example.com" }
};

var result = userSchema.Validate(userData);
```

## Source-generated validators

Mark a class, struct, or record with `[ZodSchema]` and a zero-allocation static validator is generated at compile time:

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

[ZodSchema]
public class User
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int Age { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

var result = UserSchema.Validate(user);
var validated = UserSchema.Parse(user); // throws on failure
```

See the [Source Generator](generator/Source-Generator.md) and [Source Generator DataAnnotations](generator/Source-Generator-DataAnnotations.md) pages for the full feature set.

## JSON integration

```csharp
// System.Text.Json or Newtonsoft.Json
var json = """{ "name": "John", "age": 30 }""";
var result = userSchema.DeserializeAndValidate(json);
```

```csharp
// JSON Schema export (core)
var jsonSchema = Z.ToJsonSchema(userSchema, new ToJsonSchemaOptions { Title = "User" });

// JSON Schema import (requires an integration package)
// using ZodSharp.JsonSchema.SystemTextJson; // or ZodSharp.JsonSchema.NewtonsoftJson
var imported = Z.FromJsonSchema(jsonSchemaString);
```

See [System.Text.Json Integration](serialization/SystemTextJson-Integration.md), [Newtonsoft.Json Integration](serialization/NewtonsoftJson-Integration.md), [JSON Schema Export](serialization/JsonSchema-Export.md), and [JSON Schema Import](serialization/JsonSchema-Import.md).

## Where to go next

This page is the entry point of the wiki, so the rest of it is grouped by what you are trying to do. Each group below is a section of the sidebar, in reading order; [Home](Home.md) lists every page in all seven sections.

- **Learn the model** — [Core Concepts](concepts/Core-Concepts.md) explains the pipeline, the result and the error model; [Fluent Schema API](Fluent-Schema-API.md) is the surface every schema type shares.
- **Pick a schema or a rule** — [Validation Rules Reference](schemas/Validation-Rules-Reference.md) lists every built-in rule, and each schema type has its own page: [String Validation](schemas/String-Validation.md), [Number Validation](schemas/Number-Validation.md), [Object Validation](schemas/Object-Validation.md), [Arrays and Other Schemas](schemas/Arrays-and-Other-Schemas.md), [Unions and Discriminated Unions](schemas/Unions-and-Discriminated-Unions.md), [Composition and Transforms](concepts/Composition-and-Transforms.md). Write your own in [Custom Rules](schemas/Custom-Rules.md).
- **Generate validators** — [Source Generator](generator/Source-Generator.md), then [Source Generator DataAnnotations](generator/Source-Generator-DataAnnotations.md), with [Source Generator Diagnostics](generator/Source-Generator-Diagnostics.md) for the codes the build reports.
- **Move data across a boundary** — [JSON Schema Export](serialization/JsonSchema-Export.md) and [JSON Schema Import](serialization/JsonSchema-Import.md) for interoperability, [System.Text.Json Integration](serialization/SystemTextJson-Integration.md) and [Newtonsoft.Json Integration](serialization/NewtonsoftJson-Integration.md) for deserialize-and-validate.
- **Host it** — [ASP.NET Core Integration](AspNetCore-Integration.md), [Blazor Integration](hosting/Blazor-Integration.md), [HttpClient Integration](hosting/Http-Integration.md), [Dependency Injection](hosting/Dependency-Injection.md), and [Localization](hosting/Localization.md) for messages. On .NET 11, [Microsoft.Extensions.Validation](hosting/Microsoft-Extensions-Validation.md) and [OpenAPI Integration](hosting/OpenAPI-Integration.md) cover the platform pipeline.
- **Ship it** — [Guarantees and Limitations](quality/Guarantees-and-Limitations.md) states what is promised, [Performance](quality/Performance.md) what it costs, and [Trimming and Native AOT](quality/Trimming-and-AOT.md) what the publish gates check. [Cross-Platform Interop](quality/Cross-Platform-Interop.md) covers the Zod parity fixtures.
- **Work on it** — [Contributing](development/Contributing.md) and [Release Flow](development/Release-Flow.md).
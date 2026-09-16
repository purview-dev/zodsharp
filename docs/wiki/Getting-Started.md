# Getting Started

## Install

```bash
dotnet add package Purview.ZodSharp
```

Add the integration packages you need:

```bash
# System.Text.Json integration + JSON Schema import
dotnet add package Purview.ZodSharp.SystemTextJson

# Newtonsoft.Json integration + JSON Schema import
dotnet add package Purview.ZodSharp.NewtonsoftJson

# ASP.NET Core ProblemDetails integration
dotnet add package Purview.ZodSharp.AspNetCore
```

- `Purview.ZodSharp` — core library, source generator, and JSON Schema **export**.
- `Purview.ZodSharp.SystemTextJson` — System.Text.Json deserialize-and-validate, validating converters, and JSON Schema **import**.
- `Purview.ZodSharp.NewtonsoftJson` — Newtonsoft.Json deserialize-and-validate, validating converters, and JSON Schema **import**.
- `Purview.ZodSharp.AspNetCore` — failed validation results converted to standard `ProblemDetails` / `HttpValidationProblemDetails` payloads.

> [!TIP]
> JSON Schema import (`Z.FromJsonSchema`) is provided by whichever JSON integration package you reference, so pick one. Export (`Z.ToJsonSchema`) lives in the core package.

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
        Console.WriteLine($"  - {string.Join(".", error.Path)}: {error.Message}");
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

See the [Source Generator](Source-Generator.md) and [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) pages for the full feature set.

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
var imported = Z.FromJsonSchema(jsonSchemaString);
```

See [System.Text.Json Integration](SystemTextJson-Integration.md), [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md), [JSON Schema Export](JsonSchema-Export.md), and [JSON Schema Import](JsonSchema-Import.md).

## Next pages

- [Core Concepts](Core-Concepts.md)
- [Fluent Schema API](Fluent-Schema-API.md)
- [Source Generator](Source-Generator.md)
- [Dependency Injection](Dependency-Injection.md)
- [Cross-Platform Interop](Cross-Platform-Interop.md)
- [Performance](Performance.md)
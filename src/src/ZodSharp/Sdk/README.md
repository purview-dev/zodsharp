# Purview.ZodSharp

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

A high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It features zero-allocation validation, struct-based rules, a fluent API, and a compile-time source generator for maximum performance.

This package is the core library. It also ships the `[ZodSchema]` source generator and JSON Schema **export** (`Z.ToJsonSchema`). JSON Schema **import** (`Z.FromJsonSchema`) and JSON serialization integrations are available in the companion packages:

- [Purview.ZodSharp.SystemTextJson](https://www.nuget.org/packages/Purview.ZodSharp.SystemTextJson) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.SystemTextJson.svg)](https://www.nuget.org/packages/Purview.ZodSharp.SystemTextJson)
- [Purview.ZodSharp.NewtonsoftJson](https://www.nuget.org/packages/Purview.ZodSharp.NewtonsoftJson) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.NewtonsoftJson.svg)](https://www.nuget.org/packages/Purview.ZodSharp.NewtonsoftJson)
- [Purview.ZodSharp.AspNetCore](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.AspNetCore.svg)](https://www.nuget.org/packages/Purview.ZodSharp.AspNetCore)

## Installation

```bash
dotnet add package Purview.ZodSharp
```

## Quick start

```csharp
using ZodSharp;

var nameSchema = Z.String().Min(3).Max(50);
var result = nameSchema.Validate("John");

if (result.IsSuccess)
    Console.WriteLine($"Valid name: {result.Value}");
```

Composable schemas for objects, arrays, unions, discriminators, records, tuples and more:

```csharp
var userSchema = Z.Object()
    .Field("name", Z.String().Min(1))
    .Field("age", Z.Number().Min(0).Max(120).Int())
    .Field("email", Z.String().Email())
    .Build();
```

## Source generator

Mark a class, struct, or record with `[ZodSchema]` and a zero-allocation validator is generated at compile time:

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

// Value-first composition methods (enable via EnableComposition, on by default):
var adult = UserSchema.ApplyRefine(user, u => u.Age >= 18, "Must be adult");
```

DataAnnotations attributes such as `[Required]`, `[Length]`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[Range]`, `[RegularExpression]`, `[AllowedValues]`, `[DeniedValues]`, `[EmailAddress]`, and `[Compare]` are validated with direct, typed codegen (no reflection).

## JSON Schema export

```csharp
var jsonSchema = Z.ToJsonSchema(userSchema, new ToJsonSchemaOptions { Title = "User" });
```

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp` on NuGet.

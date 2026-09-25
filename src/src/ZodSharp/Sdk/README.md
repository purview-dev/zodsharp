# ZodSharp

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

## Custom rules

A rule is any struct implementing `ZodSharp.Core.IValidationRule<T>`; attach it to a schema with the public `Rule`/`AddRule` API, or expose it as a DataAnnotations-style attribute that the source generator honours exactly like the built-ins:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
{
    public bool IsValid(in string value) => value.IndexOf(' ') < 0;
    public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
}

[ZodRule(typeof(NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NoWhitespaceAttribute : ValidationAttribute
{
    public string? Message { get; set; }
}

// Fluent usage:
var schema = Z.String().Rule(new NoWhitespaceRule());

[ZodSchema]
public class User
{
    [Required]
    [NoWhitespace(Message = "No spaces allowed.")]
    public string Name { get; set; } = string.Empty;
}
```

Marking the rule itself with the parameterless `[ZodRule]` makes the generator emit a matching `NoWhitespaceAttribute` whose properties mirror the rule's constructor parameters.

Rules can be **generic**: map the unbound generic rule type and the generator closes it with the property type, so one rule serves every primitive. Implementing `IZodRule` lets the rule supply a per-member error code:

```csharp
public readonly record struct NotEmptyRule<T>(string? Code = null, string? Message = null)
    : IValidationRule<T>, IZodRule
    where T : struct, IEquatable<T>
{
    public bool IsValid(in T value) => !value.Equals(default(T));
    public string GetErrorMessage(in T value) => Message ?? "Value must not be empty.";
    string? IZodRule.Code => Code;
    string? IZodRule.Origin => "value_object";
}

[ZodRule(typeof(NotEmptyRule<>))]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

[ZodSchema]
public partial record struct AssetId
{
    [NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
    public Guid Value { get; init; }
}
```

`[NotEmpty]` on a `Guid` property emits `NotEmptyRule<Guid>`; on an `int` property it emits `NotEmptyRule<int>`. See the [Custom Rules](https://purview.dev/docs/zodsharp/custom-rules/) page for the full precedence rules and the scalar value-object walkthrough.

## Type-level rules (validating the value object)

Rules can be attached to the **`[ZodSchema]` type itself**; they validate the whole value with an empty path, which is the right shape for a scalar whose single value *is* the value object:

```csharp
public readonly record struct NotEmptyRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, Guid>
{
    public bool IsValid(in TSelf value) => value.Value != Guid.Empty;
    public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
    string? IZodRule.Code => Code;
    string? IZodRule.Origin => "value_object";
}

[ZodRule(typeof(NotEmptyRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

[Scalar]
[ZodSchema]
[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

The generated validator runs the rule against the value object (`NotEmptyRule<AssetId>`) and reports `Code`, `Message`, and `Origin` with an empty path. A rule attribute on a type that gets no schema is ignored, and the analyzer warns (`ZODSGEN033`) rather than failing silently.

## Error factory

`ErrorType` lets you define a user-facing error (code, optional category, description, optional
message template with named placeholders, and — purely for convenience — an HTTP status code) and the
bundled source generator turns each `[ErrorType]` field in a `partial` class into strongly typed
`Create`/`Throw` helpers:

```csharp
using ZodSharp.Core;

public static partial class ConcurrentErrorType
{
    [ErrorType]
    public static readonly ErrorType SaveFailed = new(
        Code: "aggregate_save_failed",
        Category: "invalid_value",
        Description: "The aggregate could not be saved.",
        HttpStatus: 409,
        MessageFormat: "Aggregate '{AggregateId}' (of type {AggregateType}) failed to save",
        Parameters:
        [
            new("AggregateId", typeof(string)),
            ErrorType.Param<string>("AggregateType")
        ]);
}

// Returns a ValidationError with the code, category, the formatted message, and the typed parameters:
var error = ConcurrentErrorType.CreateSaveFailed("agg-123", "Invoice");

// Throws a ZodException carrying the same ValidationError:
ConcurrentErrorType.ThrowSaveFailed("agg-123", "Invoice");
```

The generated `Create` sets `ValidationError.Category` from the error type's `Category` — a broad
grouping that can span many specific codes (for example code `invalid_tenant_id` with category
`invalid_value`).

The bundled `ZODSASP001`/`ZODSASP002`/`ZODSASP003` analyzers warn when a `MessageFormat` placeholder
is not declared in `Parameters`, when the containing class is not `partial`, or when the field is not
`static readonly`. The `Purview.ZodSharp.AspNetCore` package consumes this factory to map errors to
`ProblemDetails` responses.

## Dependency injection

`AddZodSharpFactory` registers `IZodSchemaFactory` as a singleton. The factory is registered only if
one is not already present — the first call wins and later calls (including their `configure`
callbacks) are ignored, so state is never overwritten:

```csharp
builder.Services.AddZodSharpFactory(factory => factory.RegisterFromAssembly(typeof(User).Assembly));
```

`AddZodSchemaOptionsValidator<T>` registers an `IValidateOptions<T>` that resolves the factory and
validates `T` when options are instantiated. The factory must already be registered. When no validator
exists for `T`, the default `MissingValidatorBehavior.Throw` throws via `ResolveRequired<T>`; pass
`MissingValidatorBehavior.Ignore` to pass through untouched:

```csharp
builder.Services.AddZodSharpFactory(factory => factory.RegisterFromAssembly(typeof(UserOptions).Assembly));
builder.Services.AddZodSchemaOptionsValidator<UserOptions>();

// Or chain through the options builder, optionally failing at startup:
builder.Services.AddOptions<UserOptions>().AddZodSchemaValidator().ValidateOnStart();
```

The `Purview.ZodSharp.AspNetCore` package offers `AddZodSharp` with assembly auto-discovery.

## JSON Schema export

```csharp
var jsonSchema = Z.ToJsonSchema(userSchema, new ToJsonSchemaOptions { Title = "User" });
```

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp` on NuGet.

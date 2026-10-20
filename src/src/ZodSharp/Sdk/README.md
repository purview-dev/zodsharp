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

## Native unions (.NET 11+)

When targeting `net11.0` or later, `Z.NativeUnion` returns a native C# 15 union instead of the hand-rolled `ZodSharp.Unions.Union<T1, T2>`:

```csharp
var schema = Z.NativeUnion(Z.String().Min(1), Z.Object().Build());
var result = schema.Validate("hello");

if (result.IsSuccess)
{
    var length = result.Value switch
    {
        string s => s.Length,
        Dictionary<string, object?> o => o.Count,
        _ => -1,
    };
}
```

Use it when **both option types are reference types**: the native union is allocation-free and supports exhaustive pattern matching. Value-type cases box, so keep `Z.Union` when a case is a value type, or when you need `Tag`/`Match`/`Switch`/`TryGetValue`/equality. The analyzer reports `ZODSGEN041` and offers a code fix when a reference-type-only `Z.Union` can be switched to `Z.NativeUnion`.

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

Enum properties are validated automatically: a value that is not a defined member of the enum type is rejected with `invalid_enum_value`. Mark an enum member `[ZodIgnore]` to exclude it everywhere the enum is validated, or list it in a property's `[DeniedValues]` to exclude it for that property only. `[Flags]` enums are skipped, and `[ZodSchema(ValidateEnumValues = false)]` opts the type out.

Collection elements reject `null` by default: a null element reports `invalid_type` at its own path (for example `["Items", 0]`). Opt in per property with `[CollectionBehavior(AllowNulls = true)]`, per element type with `T?` (for example `List<Child?>`), per type with `[ZodSchema(AllowNulls = true)]`, or per assembly with `[assembly: ZodSharpSchemaDefaults(AllowNulls = true)]` or the `ZodSharpAllowNulls` MSBuild property. The most specific statement wins; `[CollectionBehavior]` on a property that cannot hold null elements is reported as `ZODSGEN046`.

## Custom rules

A rule is any struct implementing `ZodSharp.Core.IValidationRule<T>`; attach it to a schema with the public `Rule`/`AddRule` API, or expose it as a DataAnnotations-style attribute that the source generator honours exactly like the built-ins.

Every rule should also expose its error identity as public `const string ErrorCode` and `const string MessageFormat` constants — the analyzer reports `ZODSGEN042` when a rule omits them, so tests can assert against the rule rather than duplicating literals:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Whitespace is not allowed in '{0}'.";

    public bool IsValid(in string value) => value.IndexOf(' ') < 0;
    public string GetErrorMessage(in string value) =>
        Message ?? string.Format(MessageFormat, value);
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

Marking the rule itself with the parameterless `[ZodRule]` makes the generator emit a matching `NoWhitespaceAttribute`: its properties mirror the rule's constructor parameters, and its value parameters also become a constructor — a parameter the rule declares without a default is required at the call site, while one with a default keeps that default.

Rules can be **generic**: map the unbound generic rule type and the generator closes it with the property type, so one rule serves every primitive. Implementing `IZodRule` lets the rule supply a per-member error code:

```csharp
public readonly record struct NotEmptyRule<T>(string? Code = null, string? Message = null)
    : IValidationRule<T>, IZodRule
    where T : struct, IEquatable<T>
{
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in T value) => !value.Equals(default(T));
    public string GetErrorMessage(in T value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, MessageFormat);
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

`[NotEmpty]` on a `Guid` property emits `NotEmptyRule<Guid>`; on an `int` property it emits `NotEmptyRule<int>`. See the [Custom Rules](https://purview.dev/docs/zodsharp/custom-rules/) page for the full precedence rules, and [Value Objects Integration](https://purview.dev/docs/zodsharp/value-objects-integration/) for the scalar value-object walkthrough and the error code / message definitions.

### One attribute for a primitive and a scalar value object

A constraint can be *self-referential* (`where TSelf : IScalarValueObject<TSelf, string>`), which a primitive can never satisfy. Declare both halves of the rule side by side and the generator resolves the member that fits the annotated type — so a single attribute works on a `string` member (non-generic sibling) and on a scalar value object (generic closed with that type):

```csharp
public readonly record struct NonWhiteSpaceStringRule(string? Message = null)
    : IValidationRule<string?>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in string? value) => value != null && !string.IsNullOrWhiteSpace(value);
    public string GetErrorMessage(in string? value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, MessageFormat);
}

public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);
    public string GetErrorMessage(in TSelf value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, MessageFormat);
    string? IZodRule.Code => Code;
    string? IZodRule.Origin => "value_object";
}

[ZodRule(typeof(NonWhiteSpaceStringRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
```

Resolution is symmetric (mapping the attribute to the non-generic rule still finds the generic member for a scalar target). When nothing in the family can validate the target type, `ZODSGEN030` is reported and no rule is emitted. Marking an arity-1 **generic** rule with the parameterless `[ZodRule]` generates the attribute mapped to the open generic for you; if a hand-authored type already claims the derived name, the generated attribute is suppressed and reported as `ZODSGEN037` rather than silently re-mapping every usage; a hand-authored attribute whose mapping does not address the whole family is reported as `ZODSGEN038`; and a rule that accepts a `code`/`origin` constructor parameter without implementing `IZodRule` is reported as `ZODSGEN039`, because the value would be supplied and never read back.

Implement `ZodSharp.Core.IZodRuleAttribute` on a hand-authored attribute to declare its `Code`/`Origin` as the reported error identity (the compiler then guarantees the properties the generator reads exist). Any argument an attribute supplies that the resolved rule cannot consume — no matching constructor parameter and not identity — is reported as `ZODSGEN040` instead of being dropped silently.

Add `[ZodRule(AllowMultiple = true)]` to emit `AttributeUsage(..., AllowMultiple = true)` so the generated attribute may be applied more than once. Each application becomes its own rule, configured from that application's arguments and evaluated in source order:

```csharp
[ZodRule(AllowMultiple = true)]
public readonly record struct MultipleOfRule(int Factor = 1, string? Message = null) : IValidationRule<int>
{
    public const string ErrorCode = "not_multiple_of";
    public const string MessageFormat = "Number must be a multiple of {0}, but got {1}";

    public bool IsValid(in int value) => Factor != 0 && value % Factor == 0;
    public string GetErrorMessage(in int value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, MessageFormat, Factor, value);
}

[ZodSchema]
public partial class Sample
{
    [MultipleOf(Factor = 3)]
    [MultipleOf(Factor = 5)]
    public int Value { get; set; }   // must be a multiple of both 3 and 5
}
```

## Type-level rules (validating the value object)

Rules can be attached to the **`[ZodSchema]` type itself**; they validate the whole value with an empty path, which is the right shape for a scalar whose single value *is* the value object:

```csharp
public readonly record struct NotEmptyRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, Guid>
{
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in TSelf value) => value.Value != Guid.Empty;
    public string GetErrorMessage(in TSelf value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, MessageFormat);
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

The generated validator runs the rule against the value object (`NotEmptyRule<AssetId>`) and reports `Code`, `Message`, and `Origin` with an empty path. A rule written against the underlying value (for example `NonSentinelRule<Guid>`) is instead adapted automatically when applied to a `[Scalar]` type, so one rule serves every scalar backed by the same primitive. A rule attribute on a type that gets no schema is ignored, and the analyzer warns (`ZODSGEN033`) rather than failing silently. See [Value Objects Integration](https://purview.dev/docs/zodsharp/value-objects-integration/) for the full `[Scalar]` walkthrough and the error code / message definitions.

`Purview.ValueObjects` 1.0.1 (or later) adds an **automatic** scalar form where the value-object generator declares the underlying property: `[Scalar<TValue>]` or `[Scalar(typeof(TValue))]` (for example `[Scalar<Guid>]` / `[Scalar(typeof(Guid))]`). The property is invisible to ZodSharp, so the schema is generated from the attribute and supports **type-level** rules only (property-level DataAnnotations have no host — use the manual `[Scalar]` form when you need them). A nullable reference scalar is written `[Scalar<string>(Nullable = true)]` (or `[Scalar(typeof(string), Nullable = true)]`) and round-trips JSON `null`; `[RequiredZod]` still rejects `null`, so use the shipped null-tolerant `[NullOrNonWhiteSpace]` (`NullOrNonWhiteSpaceRule`) to accept `null` while rejecting whitespace. A suppressed `Validate` method on a scalar schema is reported as `ZODSGEN044`.

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

Generated schemas export `[NonSentinel]` for `DateOnly`, `TimeOnly`, `DateTime`, and `DateTimeOffset` as exact
patterns. The `DateTimeOffset` pattern is offset-aware, so the default preserves offsets without an
`x-zodsharp-unsupported` marker. An optional narrower UTC contract is available with
`<ZodSharpDateTimeOffsetSerialization>Utc</ZodSharpDateTimeOffsetSerialization>` (or
`[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc)]`) and the
JSON integration package's `UtcDateTimeOffsetConverter`. See the
[JSON Schema Export](https://purview.dev/docs/zodsharp/json-schema-export/#temporal-sentinels-and-the-datetimeoffset-wire-form)
documentation.

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp` on NuGet.

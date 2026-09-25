# Custom Rules

A **rule** is a `readonly record struct` (or class) implementing `ZodSharp.Core.IValidationRule<T>`. Rules are evaluated after a schema's type/structural check succeeds; each failing rule adds a `ValidationError` to the result.

Custom rules are first class:

- attach them to a schema with the public `AddRule`/`Rule` API, or
- surface them as a `System.ComponentModel.DataAnnotations`-style attribute (for example `[NoWhitespace]`) that the `[ZodSchema]` source generator honours exactly like `[EmailAddress]` or `[Range]`.

## The rule contract

```csharp
namespace ZodSharp.Core;

public interface IValidationRule<T>
{
    bool IsValid(in T value);
    string GetErrorMessage(in T value);
}
```

Implementations should be structs so validation does not allocate. `IsValid` is called only when the surrounding schema succeeded and (for nullable properties) the value is not `null`.

For a **string** rule, also implement `ZodSharp.Core.IStringValidationRule` (`bool IsValid(ReadOnlySpan<char> value)` / `string GetErrorMessage(ReadOnlySpan<char> value)`) so the rule participates in `ZodString.ValidateSpan`/`IsValidSpan` without materialising the input. Rules that only implement `IValidationRule<T>` are still fully supported; they simply fall back to the string pipeline for span validation.

## Defining a custom rule

```csharp
using ZodSharp.Core;

namespace MyRules;

/// <summary>Rejects strings that contain whitespace.</summary>
public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
{
    public bool IsValid(in string value)
    {
        if (value is null)
            return false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
                return false;
        }

        return true;
    }

    public string GetErrorMessage(in string value) =>
        Message ?? $"Whitespace is not allowed in '{value}'.";
}
```

The rule can be used standalone:

```csharp
var rule = new NoWhitespaceRule();
if (!rule.IsValid("John Doe"))
    Console.WriteLine(rule.GetErrorMessage("John Doe"));
```

## Attaching a rule to a schema

`ZodType<TOutput, TInput>.AddRule(IValidationRule<TOutput>)` and the generic `Rule<TRule>(TRule)` helper are public, so a custom rule can be composed directly:

```csharp
using ZodSharp;
using MyRules;

var schema = Z.String().Rule(new NoWhitespaceRule("No spaces allowed."));

var result = schema.Validate("John Doe");
// result.IsSuccess         == false
// result.Errors[0].Code    == "validation_failed"
// result.Errors[0].Message == "No spaces allowed."
// result.Errors[0].Path    is empty
```

Both methods mutate the receiver and return it for chaining; see [Guarantees and Limitations](Guarantees-and-Limitations.md#fluent-rule-methods-mutate-the-receiver).

## Exposing a rule as a DataAnnotations attribute

Built-in rules map to `System.ComponentModel.DataAnnotations` attributes (`EmailRule` ↔ `[EmailAddress]`). A custom rule gets the same treatment in two steps:

1. Author a `ValidationAttribute` whose properties mirror the rule's constructor parameters.
2. Map it to the rule with `[ZodRule(typeof(...))]`.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using ZodSharp.Core;
using MyRules;

[ZodRule(typeof(NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NoWhitespaceAttribute : ValidationAttribute
{
    /// <summary>Overrides the rule's default error message.</summary>
    public string? Message { get; set; }
}
```

Apply it to a `[ZodSchema]` model like any other annotation:

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

[ZodSchema]
public class User
{
    [Required]
    [NoWhitespace(Message = "No spaces allowed.")]
    public string Name { get; set; } = string.Empty;
}
```

The generator emits rule-based validation, so `UserSchema.Validate(user)` fails for `"John Doe"` with:

```text
Code    = "invalid_string"
Message = "No spaces allowed."
Origin  = "string"
Path    = ["Name"]
```

Because the attribute derives from `ValidationAttribute`, the property participates in the same "carries a data annotation" discovery as the built-in attributes. The default error code is `validation_failed` when `Code` is not set.

> [!NOTE]
> The attribute's constructor arguments are mapped positionally and its named arguments by name (case-insensitive) to the rule's public constructor parameters. A parameter named `message` is supplied from the attribute's `ErrorMessage` when one is set.

### Error identity: code and origin precedence

One attribute type can serve many members that each need a different error code. The generator resolves `Code`/`Origin` in this order (first match wins):

1. **Rule-owned** — the rule implements `ZodSharp.Core.IZodRule`, so `IZodRule.Code`/`IZodRule.Origin` are used at runtime (the mapped values are only a fallback when the rule returns `null`).
2. **Attribute-declared** — a `Code` / `Origin` named argument on the applied attribute (for example `[NoWhitespace(Code = "invalid_asset_id")]`).
3. **Attribute-type mapping** — `[ZodRule(typeof(X), Code = "…", Origin = "…")]`.
4. **Default** — `validation_failed` with no origin.

```csharp
using ZodSharp.Core;

public readonly record struct NotEmptyRule<T>(string? Code = null, string? Message = null)
    : IValidationRule<T>, IZodRule
    where T : struct, IEquatable<T>
{
    public bool IsValid(in T value) => !value.Equals(default(T));

    public string GetErrorMessage(in T value) => Message ?? "Value must not be empty.";

    // The rule owns its identity, so callers can pass a per-member error code.
    string? IZodRule.Code => Code;

    string? IZodRule.Origin => "value_object";
}
```

## Generic rules

Map an **unbound generic** rule type and the generator closes it with the property type, so one rule serves every underlying primitive:

```csharp
[ZodRule(typeof(NotEmptyRule<>))]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }

    public string? Message { get; set; }
}
```

- `[NotEmpty]` on a `Guid` property instantiates `NotEmptyRule<Guid>`; on an `int` property it instantiates `NotEmptyRule<int>`.
- The rule must expose exactly one type parameter. A type argument that cannot satisfy the rule's constraints (for example `NotEmptyRule<T> where T : struct` applied to a `string`) is reported as `ZODSGEN030` and no rule is emitted, so the generated code always compiles.

## Generating the attribute from the rule

If you do not want to hand-write the attribute, mark the rule itself with the parameterless `[ZodRule]` and the generator emits a matching attribute:

```csharp
using ZodSharp.Core;

namespace MyRules;

[ZodRule(Code = "invalid_string", Origin = "string")]
public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
    : IValidationRule<string>
{
    public bool IsValid(in string value) => AllowEmpty || value.IndexOf(' ') < 0;

    public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
}
```

This produces a `NoWhitespaceAttribute` in the rule's namespace, shaped like:

```csharp
/// <summary>Validation attribute that applies NoWhitespaceRule.</summary>
[global::System.AttributeUsage(
    global::System.AttributeTargets.Property
        | global::System.AttributeTargets.Field
        | global::System.AttributeTargets.Parameter,
    Inherited = true,
    AllowMultiple = false)]
[global::ZodSharp.Core.ZodRule(typeof(global::MyRules.NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
public sealed class NoWhitespaceAttribute
    : global::System.ComponentModel.DataAnnotations.ValidationAttribute
{
    public bool AllowEmpty { get; set; } = true;
}
```

Mapping rules:

- The attribute name is the rule name with a trailing `Rule` replaced by `Attribute` (`NoWhitespaceRule` → `NoWhitespaceAttribute`). Override it with `[ZodRule(AttributeName = "…")]`.
- Each public constructor parameter becomes a settable property, Pascal-cased, with the parameter's default value preserved. A parameter named `message` is omitted — use the inherited `ValidationAttribute.ErrorMessage` instead.
- The rule must be non-generic, non-nested, and non-abstract, and every parameter type must be a legal attribute-argument type (primitive, `string`, `enum`, `System.Type`).

> [!IMPORTANT]
> The generated attribute lives in the same assembly as the rule, but Roslyn generators cannot read another generator's output as a symbol. To *consume* the generated attribute with `[ZodSchema]`, reference the rule from a separate assembly (a rules library) — or hand-author the attribute and mark it with `[ZodRule(typeof(...))]`.

## Type-level rules

Rules can also be attached to the **`[ZodSchema]` type itself** instead of a property. They validate the whole value (the value object as a unit) and report an **empty path**, which is what you want for a scalar whose single `Value` *is* the value:

```csharp
[ZodRule(typeof(NotEmptyRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
[ZodSchema]
public partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

The generator emits the rule against the value itself — no property local, and `EmptyPath` rather than a property path:

```csharp
var assetIdCustomRule0 = new global::MyRules.NotEmptyRule<global::ChangeOps.AssetId>("invalid_asset_id", "AssetId must not be empty.");
if (!assetIdCustomRule0.IsValid(value))
{
    (errors ??= new List<ValidationError>()).Add(
        ValidationError.Create(
            ((global::ZodSharp.Core.IZodRule)assetIdCustomRule0).Code ?? "invalid_asset_id",
            assetIdCustomRule0.GetErrorMessage(value),
            EmptyPath,
            origin: ((global::ZodSharp.Core.IZodRule)assetIdCustomRule0).Origin ?? null));
}
```

- **Generic closure:** a type-level attribute closes an unbound generic rule with the **target type** (`NotEmptyRule<AssetId>`), so the rule sees the value object and can read its state through its own constraints.
- **Ordering** in the generated `Validate`: property rules → **type-level rules** → the synchronous `Validate()` refinement.
- Type-level attributes need `AttributeTargets.Class`/`Struct` on the attribute declaration; the property-level attributes above only need `Property`/`Field`.

### When a type-level rule does not run

- **The type gets no schema.** The generator is driven by `[ZodSchema]` and then walks nested complex property types. A rule attribute on a type that gets no schema is **ignored**, and the analyzer reports the warning **`ZODSGEN033`** so the mistake is visible. A type without `[ZodSchema]` that is referenced as a complex property of a schema *does* get a (secondary) schema, so its type-level rules run and no warning is raised.
- **`[ZodSchema(GenerateValidateMethod = false)]`.** Type-level rules live inside `Validate`, so they are omitted along with it.
- **`DisableZodSharpSourceGenerator`.** The generator — and therefore every rule — is skipped.

## Validating scalar value objects

A `Purview.ValueObjects` scalar **is** a single value, so validate it as a unit rather than through its `Value` property. Scalars implement the two-type-parameter contract:

```csharp
public interface IScalarValueObject<TSelf, TValue> : IValueObject, IComparable<TSelf>, IComparable
    where TSelf : IScalarValueObject<TSelf, TValue>
{
    TValue Value { get; }
    static abstract TSelf Create(TValue value);
    static abstract TSelf Hydrate(TValue value);
    int CompareTo(TValue other);
}
```

so `AssetId` is `IScalarValueObject<AssetId, Guid>`. Today the check is normally repeated on every scalar:

```csharp
// repeated on every Guid scalar
internal IEnumerable<ValidationError> Validate()
{
    if (Value == Guid.Empty)
        yield return ErrorFactory.InvalidAssetId;
}
```

Type **one** rule on the value object and put the attribute on the **scalar type**:

```csharp
// MyRules/NotEmptyRule.cs — a rules library that references Purview.ValueObjects
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
```

```csharp
// Purview.ChangeOps
using Purview.ValueObjects.Serialization;
using ZodSharp;

[Scalar]
[ZodSchema]
[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}

[Scalar]
[ZodSchema]
[NotEmpty(Code = "invalid_external_identity_id", Message = "ExternalIdentityId must not be empty.")]
public readonly partial record struct ExternalIdentityId
{
    public Guid Value { get; init; }
}
```

The per-scalar `Validate()` refinements disappear, each scalar keeps its own `Code`/`Message`, and the reported error has an **empty path** because the rule applies to the value object itself:

```text
Code    = "invalid_asset_id"
Message = "AssetId must not be empty."
Origin  = "value_object"
Path    = []
```

Because the rule is closed with `TSelf` (`NotEmptyRule<AssetId>`), it *sees the value object* and reads `Value` through the `IScalarValueObject<TSelf, Guid>` constraint. A generic rule must have exactly one type parameter, so the underlying value type is pinned by the constraint — define one rule per primitive (`NotEmptyRule<TSelf> where TSelf : IScalarValueObject<TSelf, Guid>`, a `long` variant, and so on).

> [!NOTE]
> If the type has no value-object contract (a plain class with a `Guid` property), the property-level form still works: map the attribute to a `IValidationRule<Guid>` and put it on `Value`. See [Exposing a rule as a DataAnnotations attribute](#exposing-a-rule-as-a-dataannotations-attribute) and [Generic rules](#generic-rules).

> [!TIP]
> If the non-empty policy should be implicit rather than an attribute, the value-objects layer is the natural place to emit `[NotEmpty]` on the scalar type (it already knows about ZodSharp through `ZodSchemaMode`).

## Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| ZODSGEN030 | Error | The mapped rule does not implement `IValidationRule<T>` for the property type (or the rule target type), or an unbound generic rule could not be closed with it. |
| ZODSGEN031 | Error | A rule constructor parameter could not be mapped from the attribute. |
| ZODSGEN032 | Error | A validation attribute could not be generated for the rule. |
| ZODSGEN033 | Warning | A rule-mapped attribute is applied to a type that gets no generated schema (no `[ZodSchema]` and not referenced as a complex property), so the rule never runs. |

See [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the full list.

## Related

- [Fluent Schema API](Fluent-Schema-API.md) — `AddRule`/`Rule` live on `ZodType`.
- [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) — built-in attribute coverage.
- [Guarantees and Limitations](Guarantees-and-Limitations.md) — allocation and mutation semantics.


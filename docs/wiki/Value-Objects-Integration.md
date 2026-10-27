# Value Objects Integration

A [Purview.ValueObjects](https://www.nuget.org/packages/Purview.ValueObjects) **scalar** is a strongly typed wrapper around a single value (`AssetId` around a `Guid`, `TenantId` around a `Guid`, `EmailAddress` around a `string`, and so on). Because a scalar *is* one value, ZodSharp validates it as a unit — not through its `Value` property — and reports the failure with an **empty path**.

This page shows how to wire the `[Scalar]` generation to the `[ZodSchema]` generation, the two shapes a custom rule can take, and how the rule's error code and message are defined. For the general rule contract, see [Custom Rules](Custom-Rules.md).

## Prerequisites and wiring

Reference both packages:

```bash
dotnet add package Purview.ValueObjects
dotnet add package Purview.ZodSharp
```

A scalar is declared with the `[Scalar]` attribute (from `Purview.ValueObjects.Serialization`) and given a ZodSharp schema with `[ZodSchema]`. The type must be `partial`:

```csharp
using Purview.ValueObjects.Serialization;
using ZodSharp;

namespace ChangeOps;

[Scalar]
[ZodSchema]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

Each attribute brings in a different generator:

| Attribute | Generator | What it emits |
|---|---|---|
| `[Scalar]` | **Purview.ValueObjects** | the `IScalarValueObject<TSelf, TValue>` implementation (and, when `Purview.ZodSharp` is referenced, the `ScalarRuleAdapter<TSelf, TValue, TRule>` used below) |
| `[ZodSchema]` | **Purview.ZodSharp** | the static `AssetIdSchema` validator (`Validate`/`Parse`, and so on) |

The underlying member defaults to a public property named `Value`. Rename it with the attribute argument and the value-objects generator follows:

```csharp
[Scalar("Id")]
[ZodSchema]
public readonly partial record struct TenantId
{
    public Guid Id { get; init; }
}
```

Scalars implement the two-type-parameter contract:

```csharp
namespace Purview.ValueObjects;

public interface IScalarValueObject<TSelf, TValue> : IValueObject, IComparable<TSelf>, IComparable
    where TSelf : IScalarValueObject<TSelf, TValue>
{
    TValue Value { get; }
    static abstract TSelf Create(TValue value);
    static abstract TSelf Hydrate(TValue value);
    int CompareTo(TValue other);
}
```

so `AssetId` is `IScalarValueObject<AssetId, Guid>`.

> [!IMPORTANT]
> The ZodSharp generator discovers a scalar through the source-visible `[Scalar]` attribute and the public property it names — it does not depend on the `IScalarValueObject<TSelf, TValue>` interface, because that interface is contributed by another generator and may not be visible as a symbol in the same compilation pass.

> [!NOTE]
> The `ScalarRuleAdapter<TSelf, TValue, TRule>` type is emitted into your compilation by the **Purview.ValueObjects** generator whenever the project references both `Purview.ValueObjects` and `Purview.ZodSharp`. Do not declare it yourself. If the value-objects generator is disabled (`DisableValueObjectsSourceGenerator`), the adapter is not emitted and the generated validator will not compile.

## Automatic scalar forms

`Purview.ValueObjects` 1.0.1 (or later) also supports an **automatic** form, where the value-object generator declares the underlying property for you. Both spellings are equivalent:

```csharp
[Scalar<Guid>]                                 [Scalar(typeof(Guid))]
[Scalar<string>(Nullable = true)]              [Scalar(typeof(string), Nullable = true)]
[Scalar<int?>]                                 [Scalar(typeof(int?))]
[Scalar<Guid>("Id")]                           [Scalar(typeof(Guid), "Id")]
```

A nullable reference type cannot be a generic attribute argument (`[Scalar<string?>]` reports `CS8970`) or a `typeof` operand (`typeof(string?)` reports `CS8639`), so a nullable reference scalar is written with `Nullable = true`. For a value type, `Nullable = true` wraps the underlying type in `Nullable<T>`, so `[Scalar<DateTimeOffset>(Nullable = true)]` emits `public DateTimeOffset? Value`. Nullable value types can also be written directly (`[Scalar<int?>]` / `[Scalar(typeof(int?))]`). A nullable scalar round-trips JSON `null`, so its schema accepts `null`.

When the underlying value is nullable, a type-level rule written against that value is applied only when a value is present. The rule is closed over the non-nullable value type (so constrained rules such as `MinValueRule<T>` work), and `null` is treated as absent. Use `[RequiredZod]` when `null` must itself fail, exactly as for a nullable member.

The property is emitted by the value-object generator, which ZodSharp cannot observe, so the automatic form has no source member for ZodSharp to read. ZodSharp resolves the underlying type from the attribute instead, so the schema is generated exactly as for the manual form:

```csharp
[Scalar<Guid>]
[ZodSchema]
[NonSentinel(Message = "InstallationId must not be empty.")]
public readonly partial record struct InstallationId { }
```

Because there is no property to carry DataAnnotations, the automatic form supports **type-level** rules only:

- `[RequiredZod]`, `[NullOrNonWhiteSpace]`, `[NonSentinel]`, and any custom `[ZodRule(typeof(...))]`-mapped attribute applied to the type are read from the type and applied to the scalar's underlying value, reporting an **empty path** exactly as for the manual form. For a nullable value scalar the rule runs only when the value is present; for a nullable reference scalar the rule receives the nullable value so it can decide (hence the null-tolerant `[NullOrNonWhiteSpace]`).
- A **property-level DataAnnotation has no host** on the generated property, so it is never observed. Use a Zod-native type-level rule, or switch to the manual `[Scalar]` form when property-level DataAnnotations are required.
- `[RequiredZod]` always rejects `null`; use `[NullOrNonWhiteSpace]` (or a custom null-tolerant rule) when a nullable scalar must accept `null` while rejecting whitespace.

`[ZodSchema(GenerateValidateMethod = false)]` is incompatible with a scalar form, because the value-object generator's generated `Create` always calls `{Type}Schema.Validate(instance)`; ZodSharp reports **`ZODSGEN044`** rather than leaving a dangling reference, and it also reports it when the declared underlying type cannot be represented.

## Two ways to attach a rule to a scalar

There are two shapes, and the generator picks the right one from the rule's own constraints:

1. **A rule written against the value object** (`where TSelf : IScalarValueObject<TSelf, TValue>`) closes over the scalar itself, so it can read state through the value-object contract.
2. **A rule written against the underlying value** (`IValidationRule<TValue>`) is **adapted automatically**: the generator reads the scalar's property, closes the rule with the underlying value, and wraps it in `ScalarRuleAdapter` so the rule still runs against the value object as a unit. One rule then serves every scalar backed by the same primitive.

In both cases the rule is attached at the **type level** — put the attribute on the scalar type, not on its `Value` property — and the reported error has an **empty path**:

```text
Code    = "invalid_asset_id"
Message = "AssetId must not be empty."
Origin  = "value_object"
Path    = []
```

> [!TIP]
> `ZodSharp.Rules.NonSentinelRule<T>` and its generated `ZodSharp.Rules.NonSentinelAttribute` ship with `Purview.ZodSharp`, so the canonical non-sentinel case is available as `[NonSentinel(Message = "…")]` on a `[Scalar]` type without defining anything yourself. The walkthroughs below use local rules (`NotEmptyRule<T>`, `MyRules.NonSentinelRule<T>`) to show the full shape.

Type-level attributes need `AttributeTargets.Class | AttributeTargets.Struct` (and usually `Property`/`Field` too, so the same attribute can validate a primitive member). A generated attribute already carries all five targets:

```csharp
[ZodRule(typeof(NotEmptyRule<>))]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
```

## Error code and message definitions

Every rule **should** expose its error identity as public constants so consumers and tests can assert against the rule rather than re-typing literals:

```csharp
public const string ErrorCode = "invalid_value";
public const string MessageFormat = "Value must not be empty.";
```

- `ErrorCode` is the rule's canonical code — the value a test compares against (`Assert.That(error.Code).IsEqualTo(NotEmptyRule<AssetId>.ErrorCode)`).
- `MessageFormat` is a `string.Format` template. `{0}` (and `{1}`, …) are the offending value and any rule-specific arguments; format it with `RuleMessage.Format(MessageFormat, …)` (invariant culture, never throws).
- The constants may be inherited from a base rule class, and **abstract bases are exempt**, so a shared base can host them for its concrete derivations.

A rule that omits either constant is reported as **ZODSGEN042** (a warning).

### Runtime identity: `IZodRule`

The constants are the *static* default. When a single attribute must produce a different code per annotated scalar, the rule implements `ZodSharp.Core.IZodRule` and supplies its own code/origin at runtime:

```csharp
public interface IZodRule
{
    string? Code { get; }     // null falls back to the attribute-mapped code
    string? Origin { get; }   // null falls back to the attribute-mapped origin
}
```

The generated code reads the identity through an `IZodRule` cast, preferring the rule over the mapped attribute values. A rule that accepts a `code`/`origin` constructor parameter **without** implementing `IZodRule` never reaches the reported error identity and is reported as **ZODSGEN039**.

The full precedence for the reported code and origin (first match wins):

1. **Rule-owned** — the rule implements `IZodRule`, so its `Code`/`Origin` are used at runtime (the mapped values are only a fallback when the rule returns `null`).
2. **Attribute-declared** — a `Code` / `Origin` named argument on the applied attribute (for example `[NotEmpty(Code = "invalid_asset_id")]`).
3. **Attribute-type mapping** — `[ZodRule(typeof(X), Code = "…", Origin = "…")]`.
4. **Default** — `validation_failed`, with no origin.

### Message overrides

The reported message is resolved in this order:

1. A `message` argument on the rule (typically a `Message` constructor parameter), when set.
2. The attribute's `ErrorMessage` / `ErrorMessageResourceName` / `ErrorMessageResourceType`, mapped to a constructor parameter named `message`.
3. `GetErrorMessage(value)`, which formats `MessageFormat` with the offending value.

`MessageFormat` is therefore the fallback, not the only message.

When the fallback is used by a **type-level** rule on a scalar, the generator rewords it: the leading subject word `Value` in `MessageFormat` is replaced with the scalar's name, so the error still identifies its owner even though it reports an empty path. A rule whose `MessageFormat` does not contain `Value` (for example `"Number must be even, but got {0}"`) is left unchanged, and an explicit `message`/`ErrorMessage` is never rewritten. This is why `NonSentinelRule<T>` reports `TenantId has a sentinel value, but got an empty GUID` for `[NonSentinel]` on a `[Scalar] TenantId`, but `Value has a sentinel value, but got an empty GUID` for a member or the fluent API.

### Rule against the value object

Define one generic rule per underlying primitive, close it over the scalar, and implement `IZodRule` so each scalar keeps its own code:

```csharp
// MyRules/NotEmptyRule.cs — a rules library that references Purview.ValueObjects
using System.Globalization;
using ZodSharp.Core;

namespace MyRules;

public readonly record struct NotEmptyRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, Guid>
{
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

    public string GetErrorMessage(in TSelf value) =>
        Message ?? string.Format(CultureInfo.CurrentCulture, MessageFormat);

    string? IZodRule.Code => Code;
    string? IZodRule.Origin => "value_object";
}
```

```csharp
// Purview.ChangeOps
using MyRules;
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

Because the rule closes over `TSelf` (`NotEmptyRule<AssetId>`), it sees the value object and reads `Value` through the `IScalarValueObject<TSelf, Guid>` constraint. A generic rule must have exactly one type parameter, so the underlying value type is pinned by the constraint — define one rule per primitive (`NotEmptyRule<TSelf> where TSelf : IScalarValueObject<TSelf, Guid>`, a `long` variant, and so on). This rule is **not** adapted: a rule whose constraint asks for the scalar keeps closing over the value object.

### Rule against the underlying value (adapted)

A rule written against the value (`IValidationRule<Guid>`) is adapted automatically when applied to a `[Scalar]` type, so one rule serves every scalar backed by that primitive:

```csharp
// MyRules/NonSentinelRule.cs — a rule written against the value, not the value object
using System;
using System.Globalization;
using ZodSharp.Core;

namespace MyRules;

public readonly record struct NonSentinelRule<T>(string? Message = null)
    : IValidationRule<T>, IZodRule
    where T : IEquatable<T>
{
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value is a sentinel value, but got {0}";

    public bool IsValid(in T value) => !value.Equals(default(T)!);

    public string GetErrorMessage(in T value) =>
        Message ?? string.Format(CultureInfo.CurrentCulture, MessageFormat, value);

    string? IZodRule.Code => ErrorCode;
    string? IZodRule.Origin => "value_object";
}
```

```csharp
[Scalar]
[ZodSchema]
[NonSentinel(Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

The generator emits the rule against `Guid` and wraps it in the adapter the value-objects generator emits:

```csharp
var assetIdCustomRuleInner0 = new global::MyRules.NonSentinelRule<global::System.Guid>("AssetId must not be empty.");
var assetIdCustomRule0 = new global::Purview.ValueObjects.ScalarRuleAdapter<global::ChangeOps.AssetId, global::System.Guid, global::MyRules.NonSentinelRule<global::System.Guid>>(assetIdCustomRuleInner0);
if (!assetIdCustomRule0.IsValid(value))
{
    (errors ??= new List<ValidationError>()).Add(
        ValidationError.Create(
            ((global::ZodSharp.Core.IZodRule)assetIdCustomRuleInner0).Code ?? "validation_failed",
            assetIdCustomRule0.GetErrorMessage(value),
            EmptyPath,
            origin: ((global::ZodSharp.Core.IZodRule)assetIdCustomRuleInner0).Origin));
}
```

The reported error keeps the **empty path**, and the error identity (`Code`/`Origin`) is read from the **wrapped** rule, not the adapter.

> [!NOTE]
> If the type has no value-object contract (a plain class with a `Guid` property), the property-level form still works: map the attribute to an `IValidationRule<Guid>` and put it on `Value`. See [Exposing a rule as a DataAnnotations attribute](Custom-Rules.md#exposing-a-rule-as-a-dataannotations-attribute) and [Generic rules](Custom-Rules.md#generic-rules).

> [!NOTE]
> A `Guid`-backed scalar is validated as a canonical Guid, and exports to JSON Schema as a `pattern`, not
> `format: uuid` — a `Guid` accepts any 128-bit value, and a time-bound identifier generated by
> `FastGuid.NewSqlServerGuid()`/`NewPostgreSqlGuid()` is not an RFC UUID. When an identifier *must* be a
> specific RFC version (for example a `Guid.CreateVersion7()` key), declare it with the UUID rule:
>
> ```csharp
> [Scalar<Guid>]
> [ZodSchema]
> [UUID(UuidVersion.V7)]
> public readonly partial record struct AssetId { }
> ```
>
> `UUIDRule` validates a `Guid`'s bytes directly, so the rule adapts onto the scalar; the export is
> `format: "uuid"` plus a version-pinning `pattern`. Generation strategy alone never implies a version
> constraint — it is only applied when the runtime validator applies it.

## Testing

Assert against the rule's constants instead of duplicating literals, and remember that an adapted scalar rule reads its identity from the wrapped rule:

```csharp
// Direct scalar rule — the runtime identity comes from IZodRule or the attribute mapping.
var directError = AssetIdSchema.Validate(default).Errors[0];

await Assert.That(directError.Code).IsEqualTo("invalid_asset_id");
await Assert.That(directError.Message).IsEqualTo("AssetId must not be empty.");
await Assert.That(directError.Origin).IsEqualTo("value_object");
await Assert.That(directError.Path.Length).IsEqualTo(0);

// Adapted underlying-value rule — the canonical identity is the wrapped rule's constant.
var adaptedError = TenantIdSchema.Validate(default).Errors[0];

await Assert.That(adaptedError.Code).IsEqualTo(NonSentinelRule<Guid>.ErrorCode); // "invalid_value"
await Assert.That(adaptedError.Message).IsEqualTo("TenantId must not be empty."); // attribute override

// With no Message override, the rule's MessageFormat is the message. When the rule runs at the type
// level of a scalar, the subject ("Value") is replaced with the scalar's name and the sentinel is
// described rather than printed raw:
await Assert.That(TenantIdSchema.Validate(default).Errors[0].Message)
    .IsEqualTo("TenantId has a sentinel value, but got an empty GUID");

// Anywhere else (a member, or the fluent API), the generic subject is kept:
await Assert.That(
    string.Format(CultureInfo.CurrentCulture, NonSentinelRule<Guid>.MessageFormat, "an empty GUID"))
    .IsEqualTo("Value has a sentinel value, but got an empty GUID");
```

## Diagnostics and troubleshooting

| ID | Severity | Meaning |
|---|---|---|
| ZODSGEN030 | Error | The mapped rule does not implement `IValidationRule<T>` for the target type, or an unbound generic rule could not be closed with it. |
| ZODSGEN033 | Warning | A rule-mapped attribute is applied to a type that gets no generated schema (no `[ZodSchema]` and not reachable as a complex property), so the rule never runs. |
| ZODSGEN039 | Warning | A rule accepts a `code`/`origin` constructor parameter but does not implement `IZodRule`, so the value never reaches the reported error identity. |
| ZODSGEN040 | Warning | An attribute argument has no effect: the resolved rule has no matching constructor parameter and the value is not part of the reported error identity. |
| ZODSGEN042 | Warning | A rule does not expose public `const string ErrorCode` / `MessageFormat` constants. |
| ZODSGEN044 | Error | A `[ZodSchema]` type uses an automatic `[Scalar]` form but cannot produce a usable schema: the generated `Validate` method is suppressed, or the declared underlying type cannot be represented. |

Common causes:

- **The rule never runs.** The scalar is missing `[ZodSchema]` (ZODSGEN033), or `[ZodSchema(GenerateValidateMethod = false)]` omitted the `Validate` method that hosts type-level rules.
- **The validator does not compile.** `DisableValueObjectsSourceGenerator` disabled the adapter; re-enable it, or express the rule against the value object so no adapter is needed.
- **The wrong rule shape resolved.** A rule constrained to `IScalarValueObject<TSelf, TValue>` is never adapted; a rule written against the underlying value is always adapted. If a family member cannot validate the target type, ZODSGEN030 is reported and nothing is emitted.

## Related

- [Custom Rules](Custom-Rules.md) — the rule contract, the public `AddRule`/`Rule` API, rule families, and attribute generation.
- [Source Generator](Source-Generator.md) — the `[ZodSchema]` generator, generated types, and the refinement hook.
- [Source Generator Diagnostics](Source-Generator-Diagnostics.md) — the full `ZODSGEN*` catalog.

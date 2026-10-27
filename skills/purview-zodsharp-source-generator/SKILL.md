---
name: purview-zodsharp-source-generator
description: >-
  Use when generating a compile-time validator with Purview.ZodSharp's [ZodSchema] attribute - which
  types it emits, the attribute options, DataAnnotations coverage, the OnZodValidate refinement hook,
  async and service-aware validation, per-member validation and path resolution, typed Paths and
  JsonNames, trim-safe registration, IValidatableObject, automatic enum validation, IValidateOptions
  support, and the MSBuild switches that control it.
license: MIT
metadata:
  product: Purview.ZodSharp
  repository: purview-dev/zodsharp
---

# The `[ZodSchema]` source generator

Mark a class, struct or record with `[ZodSchema]` and a static, zero-allocation validator is emitted
at compile time — no reflection at runtime. The attribute itself is generated into the `ZodSharp`
namespace by `Purview.ZodSharp.SourceGenerators`, so nothing beyond `Purview.ZodSharp` is needed.

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
var validated = UserSchema.Parse(user);   // throws ZodException on failure
```

## What gets emitted

| Artifact | Shape |
| --- | --- |
| `{TypeName}Schema` | static partial class — the validator. Accessibility mirrors the target. Holds `Validate`, `ValidateAsync(value, services, ct)`, `RequiresAsyncValidation`, the member API below, `Parse`, `BuildJsonSchema`, and (with composition on) `ApplyAnd`, `ApplyOr`, `ApplyRefine`. Renamed by `SchemaName`. |
| `{TypeName}Schema.Paths` | one `ZodPath` constant per member (`OrderSchema.Paths.Lines`) for refinements and tests |
| `{TypeName}Schema.JsonNames` | `Get(memberName)` returns the `[JsonPropertyName]` name, or `null`; `ToJsonNamedPath(path, namingPolicy)` renames a whole path without reflection |
| `{TypeName}SchemaValidator` | `partial class` implementing `IZodSchemaValidator<T>`, `IZodMemberValidator<T>`, `IZodMemberResolver<T>`, `IZodUntypedValidator`, `IZodJsonPathMapper` and `IJsonSchemaProvider` — the adapter hosts use. Primary schema only; `{SchemaName}Validator` when renamed. |
| `{TypeName}Validator` | `sealed partial class : IValidateOptions<{TypeName}>` (and, on .NET 11, `IAsyncValidateOptions<T>` when the type has async rules) — only when `IValidateOptions` support is on, and the target is a class. |
| `ZodSharp.Generated.{Assembly}ZodSharpSchemas.Register(factory)` | registers every generated validator in the assembly by name — trim- and AOT-safe |
| `services.AddZodSharp{Assembly}Schemas()` | the DI form of `Register`, emitted when Microsoft.Extensions.DependencyInjection is referenced. `{Assembly}` is the assembly name in PascalCase (`Contoso.Orders.Api` → `AddZodSharpContosoOrdersApiSchemas()`) |
| `[assembly: ZodSchemaGenerated(typeof({TypeName}))]` | the marker `IZodSchemaFactory` assembly scanning consumes. Primary, non-nested schemas only. |

`BuildJsonSchema` is built from the same metadata as `Validate`, so a generated schema exports through
`Z.ToJsonSchema` without rebuilding its rules by hand.

```csharp
var adult  = UserSchema.ApplyRefine(user, u => u.Age >= 18, "Must be adult");
var both   = UserSchema.ApplyAnd(user, u => u.Name.Length > 5, "Name too short");
var either = UserSchema.ApplyOr(user, u => u.Age < 18, "Must be an adult, or a minor with consent");
```

## Attribute options

| Property | Default | Purpose |
| --- | --- | --- |
| `SchemaName` | `null` | Renames `{TypeName}Schema`; the DI adapter becomes `{SchemaName}Validator`. |
| `GenerateValidateMethod` | `true` | `false` omits `Validate` **and everything that depends on it**. |
| `GenerateParseMethod` | `true` | `false` omits `Parse`. |
| `EnableComposition` | `true` | Emits `ApplyAnd`/`ApplyOr`/`ApplyRefine`. |
| `CustomValidationMethodName` | `null` | Names the async custom validation method (default lookup `CustomValidationAsync`). Mutually exclusive with `OnZodValidate`. |
| `GenerateIValidateOptions` | `false` | Forces `IValidateOptions<T>` generation. |
| `SuppressIValidateOptions` | `false` | Opts out even when auto-detection would enable it. |
| `ValidateEnumValues` | `true` | `false` skips automatic enum validation. |
| `GenerateJsonSchema` | `true` | `false` omits `BuildJsonSchema` and `IJsonSchemaProvider`. A type referenced by an exported type is `ZODSGEN045`. |
| `AllowNulls` | unset | Whether this type's collection properties may contain `null` elements. Unset defers to the assembly default. |

`Parse`, the composition methods, the `IZodSchemaValidator` adapter and the `IValidateOptions`
validator all depend on `Validate` — `GenerateValidateMethod = false` removes them together.

## What is validated

- Properties must be public, non-static, non-indexer.
- A property gets validation when it carries **any** DataAnnotations attribute, when its type is a
  source-defined complex type with a nested schema, or when its type is an enum.
- Nested complex types are discovered recursively and get their own `{TypeName}Schema`, **even
  without** their own `[ZodSchema]`.
- Nullable properties are null-guarded before value-set/type validation; a nullable target rejects
  `null` with `invalid_type`.
- Collection elements reject `null` by default: the error is `invalid_type` at the element's own path
  (`["Items", 0]`). Control it with `[CollectionBehavior(AllowNulls = ...)]` on the property, a `T?`
  element annotation (implies allow), `[ZodSchema(AllowNulls = ...)]` on the containing type,
  `[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]`, then the `ZodSharpAllowNulls` MSBuild
  property. The most specific statement wins; an unset level defers to the next. An explicit
  `AllowNulls = true` exports a `null` branch on the collection's `items` schema.
- **Dictionaries are maps, not arrays.** A property whose type implements `IDictionary<TKey, TValue>`
  or `IReadOnlyDictionary<TKey, TValue>` (for example `Dictionary`, `SortedDictionary`,
  `ConcurrentDictionary`, `ImmutableDictionary`) validates every **value** against the value type's
  nested schema and reports a failure at `["Items", "key"]`. Keys are used only in error paths, and
  `AllowNulls` governs a null value (`Dictionary<string, Child>` rejects it by default; `Child?` or
  `[CollectionBehavior(AllowNulls = true)]` accepts it). A dictionary exports as
  `{ "type": "object", "additionalProperties": … }` and `[Length]`/`[MinLength]`/`[MaxLength]` map to
  `minProperties`/`maxProperties` — never `z.array`/`items`.
- Classes, structs and records are supported. Structs get no `IValidateOptions` (`ZODSGEN028` if
  requested).

- `IValidatableObject.Validate` runs after the members and type rules, as in DataAnnotations; its
  results report code `validation_failed`, category `custom`, at the member names they list. On .NET 11
  `IAsyncValidatableObject` and `AsyncValidationAttribute` members run in `ValidateAsync`; a
  synchronous `Validate` of such a type throws `InvalidOperationException` (`ZODSGEN048`).
- **C# 15:** a member typed as a union dispatches to the active case's schema, and a member typed as a
  closed class to the derived type's schema (`ZODSGEN049` when a derived type has no `[ZodSchema]`).
  Paths never include the union's `Value`.
- Nested validation is depth-guarded: `ZodValidationDepth.MaxDepth` (32) throws
  `InvalidOperationException` instead of overflowing the stack on a cycle.
- Paths are typed: `Items[0].Name`, `Map[en].Value`, `Map["0"].Value` from `FullPath.ToDottedString()`.

DataAnnotations coverage, codes and categories, positional-record attribute targets, structured size
issues, error messages and the `[Range]` shapes are in
[references/dataannotations.md](references/dataannotations.md). The member API, the async graph and
registration are in [references/generated-api.md](references/generated-api.md).

## Member validation and resolution

```csharp
OrderSchema.IsValidatableMember("CustomerName");          // has rules of its own?
var field = OrderSchema.ValidateMember(order, "CustomerName");
OrderSchema.TryResolveMember(order, error.FullPath, out var container, out var memberName);
```

`ValidateMember`/`ValidateMemberAsync` run **one member's own rules** - no nested recursion, type
rules, `IValidatableObject` or refinement - which is what field-level validation (a Blazor field
change) needs. `TryResolveMember` walks an error path to the object that owns the failing member,
without reflection.

## The `OnZodValidate` refinement hook

Rules DataAnnotations cannot express are written as a **generator-declared partial method**. The
generator emits the declaration, so the IDE offers the implementation with the right signature and
nothing is resolved by naming convention:

```csharp
[ZodSchema]
public partial class Order
{
    public decimal Total { get; set; }

    partial void OnZodValidate(RefineCtx<Order> context)
    {
        if (context.Value.Total < 0)
            context.AddIssue("invalid_total", "Total cannot be negative", OrderSchema.Paths.Total);
    }
}
```

- The type **and every containing type** must be `partial` (`ZODSGEN034`). That is the only shape
  requirement the hook adds.
- The signature must be `partial void OnZodValidate(RefineCtx<T> context)`, `T` being the model
  (`ZODSGEN035`). The parameter is a plain by-value `RefineCtx<T>`.
- The hook runs for **every** entry point — `Validate`, `Parse`, the `IZodSchemaValidator` adapter,
  `IValidateOptions`, any factory that validates through the schema — so a rule written here behaves
  exactly like an attribute rule.
- A type that does not implement it allocates nothing: the generated `Validate` only builds a
  `RefineCtx<T>` and calls the hook when a body exists.
- The retired `IEnumerable<ValidationError> Validate()` contract is no longer bound, and is reported
  as `ZODSGEN036`.

Alongside the hook the generator emits one `internal static`
`InvokeZodRefinementHook(T value, RefineCtx<T> context)` bridge on the target type. A classic partial
method is private and `{Type}Schema` is a different type, so the bridge is what lets `Validate` reach
the hook while keeping the hook optional. **It is not part of your API; never implement it by hand.**

## Async custom validation

Mutually exclusive with `OnZodValidate` — declaring both is `ZODSGEN029`.

```csharp
[ZodSchema]
public partial class SignUp
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [ZodAsyncMember(nameof(Email))]   // also run by ValidateMemberAsync(value, "Email", ...)
    internal static async ValueTask<ValidationResult<SignUp>> CustomValidationAsync(
        SignUp value, IServiceProvider services, CancellationToken cancellationToken)
    {
        var directory = services.GetRequiredService<IUserDirectory>();
        return await directory.IsEmailTakenAsync(value.Email, cancellationToken)
            ? ValidationResult<SignUp>.Failure(
                ValidationError.Create("email_taken", "Email is already registered", SignUpSchema.Paths.Email))
            : ValidationResult<SignUp>.Success(value);
    }
}

var result = await SignUpSchema.ValidateAsync(model, serviceProvider, cancellationToken);
```

Signature `ValueTask<ValidationResult<T>> Name(T value, CancellationToken ct)`, or the service-aware
`Name(T value, IServiceProvider services, CancellationToken ct)`, which receives the services passed
to `ValidateAsync` (the bridge, ProblemDetails and options hosts pass theirs). The default lookup name
is `CustomValidationAsync` unless `CustomValidationMethodName` overrides it. A method on the
**model type** must be `static` and not `private` (`ZODSGEN013`, `ZODSGEN014`); one on the generated
`{TypeName}SchemaValidator` partial may be an instance method.

`ValidateAsync` runs the synchronous rules, awaits nested schemas' async validation (re-pathed under
the member), then the custom method, and merges the errors. It completes synchronously when nothing in
the graph is asynchronous; `{Type}Schema.RequiresAsyncValidation` is that compile-time answer.
Without `[ZodAsyncMember]`, the custom method runs only on full validation.
## Automatic enum validation

Every non-`[Flags]` enum property is validated automatically: a value that is not a defined member is
rejected with `invalid_enum_value`, emitted as an `EnumRule<TEnum>`.

```csharp
public enum ExampleEnum
{
    [ZodIgnore]          // never a valid value, for every property of this type
    Unspecified,
    AValidValue,
    AnotherValidValue,
}

[ZodSchema]
public class Model
{
    public ExampleEnum Status { get; set; }

    [DeniedValues(ExampleEnum.AnotherValidValue)]   // excluded for this property only
    public ExampleEnum SecondaryStatus { get; set; }
}
```

It is skipped when the enum is `[Flags]` (a combination is valid without being a defined member),
when the property declares an explicit `[AllowedValues]` allow-list (which governs instead), or with
`[ZodSchema(ValidateEnumValues = false)]`. A nullable enum is validated only when not `null`.

## `IValidateOptions` support

Enabled by `GenerateIValidateOptions = true`, by auto-detection (unset, target is not a value type,
and the type name ends with a configured suffix — `Options` or `Settings` by default), or by MSBuild.
Each failure reads `"{path}: {message} ({code})"`. A hand-authored `[OptionsValidator]` for the same
type conflicts (`ZODSGEN050`).

| MSBuild property | Default | Behaviour |
| --- | --- | --- |
| `DisableZodSharpSourceGenerator` | unset | truthy disables the generator entirely |
| `ZodSharpAutoGenerateOptionsValidators` | `true` | only an explicit `false` disables auto-detection |
| `ZodSharpAutoGenerateOptionsValidatorSuffixes` | `Options;Settings` | semicolon/comma-separated |
| `ZodSharpGenerateJsonSchemas` | `true` | explicit `false` omits `BuildJsonSchema` and `IJsonSchemaProvider` assembly-wide |
| `ZodSharpEnumSerialization` | `Number` | `String` exports enums as member names, matching a global `JsonStringEnumConverter` |
| `ZodSharpEnumNamingPolicy` | `Unchanged` | `CamelCase` applies `JsonNamingPolicy.CamelCase` to string-serialized enum names |
| `ZodSharpAllowNulls` | `false` | assembly-wide default for whether collections may contain `null` elements; explicit `true` accepts them |
| `ZodSharpDateTimeOffsetSerialization` | `PreserveOffset` | `Utc` narrows `[NonSentinel] DateTimeOffset` to a zero-offset wire contract; pair with the integration package's `UtcDateTimeOffsetConverter`. Both modes export an exact sentinel pattern |

`DateTimeOffset` precedence: `[DateTimeOffsetBehavior]` on the property/parameter, then on the type, then
`[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = ...)]`, then the MSBuild property, then
`PreserveOffset`. The attribute shapes the schema only; `Utc` requires a converter that also rejects non-UTC
input on read.

## Value objects

A `Purview.ValueObjects` scalar marked `[Scalar]` can carry `[ZodSchema]` on the same type. The
generated validator validates the scalar **as a unit** and reports an empty path, and a rule written
against the underlying value is adapted automatically.

## Related skills

`purview-zodsharp-custom-rules` for `[ZodRule]` and the built-in rule attributes,
`purview-zodsharp-diagnostics` for the `ZODSGEN` catalogue, and `purview-zodsharp-core` for the
runtime API the generated code reports through.

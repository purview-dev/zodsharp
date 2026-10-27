# Source Generator

Mark a class, struct, or record with `[ZodSchema]` and the generator emits a static, zero-allocation validator at compile time. The `[ZodSchema]` attribute is generated into the `ZodSharp` namespace by the generator itself (assembly `Purview.ZodSharp.SourceGenerators`), so no extra package is needed beyond `Purview.ZodSharp`.

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
var validated = UserSchema.Parse(user); // throws ZodException on failure
```

## Generated types

For a `[ZodSchema]` target type `{TypeName}`, the generator emits:

| Artifact | Shape |
|---|---|
| `{TypeName}Schema` | static partial class — the validator; access mirrors the target (public/internal/private for private nested types); the name is overridable with `SchemaName`; contains `Validate`, `ValidateAsync`, `Parse`, `BuildJsonSchema`, the member-level API (see [Generated members](#generated-members)), and (when composition is enabled) `ApplyAnd`, `ApplyOr`, `ApplyRefine` |
| `{TypeName}SchemaValidator` | `sealed partial class {TypeName}SchemaValidator : IZodSchemaValidator<{TypeName}>, IZodMemberValidator<{TypeName}>, IZodMemberResolver<{TypeName}>, IZodUntypedValidator, IZodJsonPathMapper, IJsonSchemaProvider` — DI-friendly adapter that forwards to `{TypeName}Schema`; emitted only for the primary schema, and named `{SchemaName}Validator` when `SchemaName` is set |
| `{TypeName}Validator` | `sealed partial class {TypeName}Validator : IValidateOptions<{TypeName}>` — emitted only when `IValidateOptions` support is enabled (and the target is a class). On .NET 11 it also implements `IAsyncValidateOptions<{TypeName}>` when the type has asynchronous rules |
| `[assembly: ZodSchemaGenerated(typeof({TypeName}), typeof({TypeName}SchemaValidator))]` | registration marker consumed by `IZodSchemaFactory` assembly scanning; emitted only for primary, non-nested schemas |
| `ZodSharp.Generated.{Assembly}ZodSharpSchemas` | one per assembly: `Register(IZodSchemaFactory)` registers every generated validator by name — trim- and AOT-safe (see [Trim-safe registration](#trim-safe-registration)) |
| `{Assembly}ZodSharpServiceCollectionExtensions` | one per assembly, in `Microsoft.Extensions.DependencyInjection`, when the project references Microsoft.Extensions.DependencyInjection: `services.AddZodSharp{Assembly}Schemas()` |

`BuildJsonSchema` returns a `ZodSharp.JsonSchema.JsonSchemaDefinition` built from the same metadata as
`Validate`, so a generated `[ZodSchema]` schema can be exported through `Z.ToJsonSchema` without rebuilding its
rules by hand. See [JSON Schema Export](JsonSchema-Export.md#export-a-generated-zodschema).

```csharp
// Value-first composition methods (EnableComposition, default true):
var adult = UserSchema.ApplyRefine(user, u => u.Age >= 18, "Must be adult");
var both = UserSchema.ApplyAnd(user, u => u.Name.Length > 5, "Name too short");
var either = UserSchema.ApplyOr(user, u => u.Age < 18, "Must be an adult or a minor with consent");
```

A failed composition reports code `refinement_failed` with category `custom`, the same identity as a runtime
`Refine`.

## Generated members

Besides `Validate`, `Parse` and `BuildJsonSchema`, every `{TypeName}Schema` exposes:

| Member | Purpose |
|---|---|
| `Paths.{Member}` | A `ZodPath` per member (`OrderSchema.Paths.CustomerName`). Use it to place an issue on a member (`RefineCtx.AddIssue`, a custom async method) or to match an error's `FullPath`, instead of hand-writing member names. |
| `JsonNames.Get(memberName)` | The explicit `[JsonPropertyName]` of a member, or `null` when it has none. |
| `ToJsonNamedPath(path, namingPolicy)` | Renames every member segment of an error path to its JSON name: the `[JsonPropertyName]` when present, otherwise `namingPolicy` (for example `JsonNamingPolicy.CamelCase.ConvertName`), otherwise the C# name. Recurses into nested schemas, so `Lines[0].Sku` becomes `lines[0].sku`. Reflection-free. |
| `RequiresAsyncValidation` | A `const bool`: `true` when some rule in the graph only runs asynchronously (a custom async method, an `AsyncValidationAttribute`, an `IAsyncValidatableObject` target, or a nested schema that has one). |
| `ValidateAsync(value, services, cancellationToken)` | Validates the whole graph asynchronously. Nested schemas are awaited and receive the same `services`; completes synchronously when `RequiresAsyncValidation` is `false`. |
| `ValidateMember(value, memberName)` / `ValidateMemberAsync(value, memberName, services, cancellationToken)` | Validates one member — see [Validating a single member](#validating-a-single-member). |
| `IsValidatableMember(memberName)` | `true` when the member has rules `ValidateMember` runs. |
| `TryResolveMember(value, path, out container, out memberName)` | Walks `value` along an error path to the object that owns the failing member — see [Resolving the owning object](#resolving-the-owning-object). |

`ValidateCore`, `ValidateCoreAsync`, `AppendJsonNamedPath` and the `TryResolveMember` overload with an
offset are public only so nested schemas can call each other; they are hidden from IntelliSense and are not
part of the supported API.

```csharp
using System.Text.Json;
using ZodSharp.Core;

var result = OrderSchema.Validate(order);
foreach (var error in result.Errors)
{
    // "Lines[0].Sku" - the canonical, C#-named path.
    var key = error.FullPath.ToDottedString();

    // "lines[0].sku" - the JSON-named path, for a client that sees the JSON shape.
    var jsonKey = OrderSchema.ToJsonNamedPath(error.FullPath, JsonNamingPolicy.CamelCase.ConvertName).ToDottedString();

    if (error.FullPath == OrderSchema.Paths.CustomerName)
    {
        // The customer name failed.
    }
}
```

Generated validators locate collection and dictionary errors with typed index and key segments, so
`ToDottedString()` renders `Lines[0].Sku` and `ByCode[abc].Sku` (an all-digit or punctuated key is quoted:
`ByCode["0"]`, `ByCode["a.b"]`). Keys are rendered with the invariant culture. See
[Core Concepts](Core-Concepts.md) for the path format.

### Validating a single member

`ValidateMember(value, memberName)` runs one member's **own** rules: its attributes, its `[Compare]`, and a
collection's null-element checks. It does not recurse into a nested schema, and it does not run type-level
rules, `IValidatableObject`, the `OnZodValidate` refinement or the custom async method. Every error it returns
therefore belongs to that member, which is what a host doing field-level validation (a Blazor field change)
needs. Member names are matched ordinally on the C# name; an unknown member succeeds.

```csharp
var nameOnly = OrderSchema.ValidateMember(order, nameof(Order.CustomerName));
var linesOnly = OrderSchema.ValidateMember(order, nameof(Order.Lines)); // null elements only, not each line's rules

if (OrderSchema.IsValidatableMember(nameof(Order.CustomerName)))
{
    // The member has rules.
}
```

`ValidateMemberAsync` also runs the member's asynchronous rules: `AsyncValidationAttribute`s on the member,
and the custom async method when it is marked `[ZodAsyncMember]` for that member (see
[Custom async validation](#custom-async-validation)).

### Resolving the owning object

`TryResolveMember` walks a value along an error path and returns the object that owns the last member
segment, without reflection:

```csharp
var error = OrderSchema.Validate(order).Errors[0]; // Lines[0].Sku
if (OrderSchema.TryResolveMember(order, error.FullPath, out var container, out var memberName))
{
    // container is order.Lines[0], memberName is "Sku".
}
```

A path that ends in an index or key (a null element at `Lines[0]`) resolves to the owner of the collection
member (`order`, `"Lines"`). A path that cannot be followed resolves to the nearest object reached and
returns `false`; a model-level error resolves to the root with an empty member name. The
Microsoft.Extensions.Validation bridge uses this to give every error the `Container` and `Name` Blazor needs to
place the message on the right field.

### Depth guard

Generated validators count the nesting depth as they walk a graph and throw `InvalidOperationException`
when it reaches `ZodValidationDepth.MaxDepth` (default `32`, matching Microsoft.Extensions.Validation's
`ValidationOptions.MaxDepth`). A graph that deep is almost always a cycle, so it fails clearly instead of
overflowing the stack. The root is validated at depth `0`. `ZodValidationDepth.MaxDepth` is process-wide;
`AddZodSharpValidation()` (`Purview.ZodSharp.Extensions.Validation`) sets it from `ValidationOptions.MaxDepth`,
so a cycle fails at the same depth however it is validated.

### Messages

Generated errors are formatted at runtime from a named template, kept in `ValidationError.MessageFormat`.
`Parameters` carries `displayName`, `member`, `declaringType` and the rule's values (`minimum`, `maximum`,
`count`, …), so a host can re-format or localize the message without reflection. A custom `ErrorMessage` keeps
its DataAnnotations positional meaning (`{0}` is the display name) and is formatted with the never-throw
`ZodMessageFormat` engine, so a stray brace is shown literally rather than throwing. A placeholder the
attribute does not supply is reported at build time as `ZODSGEN052`. See
[Source Generator DataAnnotations](Source-Generator-DataAnnotations.md#error-message-customization).

## Attribute options

All options are optional.

| Property | Default | Purpose |
|---|---|---|
| `SchemaName` | `null` | Overrides the generated schema class name (default `{TypeName}Schema`). The DI adapter becomes `{SchemaName}Validator`. |
| `GenerateValidateMethod` | `true` | Set to `false` to omit `Validate` (and the members that depend on it). |
| `GenerateParseMethod` | `true` | Set to `false` to omit `Parse`. `Parse` requires `Validate`, so it is also omitted when `GenerateValidateMethod = false`. |
| `EnableComposition` | `true` | Emits `ApplyAnd`, `ApplyOr`, `ApplyRefine` value-first composition methods. |
| `CustomValidationMethodName` | `null` | Name of an async custom validation method; default lookup name `CustomValidationAsync`. Mutually exclusive with the synchronous `OnZodValidate` refinement hook. |
| `GenerateIValidateOptions` | `false` | Force `IValidateOptions<T>` generation. |
| `SuppressIValidateOptions` | `false` | Opt out even when auto-detection would enable it. |
| `ValidateEnumValues` | `true` | Set to `false` to skip the automatic enum validation for the type's enum properties. |
| `GenerateJsonSchema` | `true` | Set to `false` to omit the JSON Schema export members (`BuildJsonSchema` and the `IJsonSchemaProvider` on the adapter), keeping the type out of the cross-platform contract surface. A type referenced by an exported type is reported as `ZODSGEN045`. |
| `AllowNulls` | unset | Whether this type's collection properties may contain `null` elements (or `null` dictionary values). Unset applies the assembly default; `true`/`false` overrides it for this type. See [Collection null items](#collection-null-items). |

> [!NOTE]
> `Parse`, the value-first composition methods (`ApplyAnd`/`ApplyOr`/`ApplyRefine`), the `IZodSchemaValidator` adapter and the `IValidateOptions` validator all depend on `Validate`. Setting `GenerateValidateMethod = false` omits them together.

## Custom async validation

Declare a partial `{TypeName}SchemaValidator` (or a static method on the model type):

```csharp
public partial class UserSchemaValidator
{
    public async ValueTask<ValidationResult<User>> CustomValidationAsync(User value, CancellationToken ct)
    {
        await Task.Delay(1, ct);
        return ValidationResult<User>.Success(value);
    }
}
```

Requirements:

- Signature `ValueTask<ValidationResult<T>> Name(T value, CancellationToken ct)`, or — to use the caller's
  services — `ValueTask<ValidationResult<T>> Name(T value, IServiceProvider services, CancellationToken ct)`.
- Default lookup name `CustomValidationAsync` unless overridden with
  `CustomValidationMethodName` on the `[ZodSchema]` attribute.
- A method declared on the model type must be `static`; a method on the generated
  `{TypeName}SchemaValidator` partial may be an instance method.
- The generated `ValidateAsync` runs every member's rules (awaiting nested schemas' `ValidateAsync`), then
  the type rules, then awaits the custom method, and merges the error sets. Error paths returned by the
  method are relative to the type; nested types' custom methods are reported under the member's path.
- A synchronous `Validate` never runs the custom method.

### Service-aware rules and `[ZodAsyncMember]`

A service-aware method receives the `services` passed to `ValidateAsync` (the request's services under ASP.NET
Core, the circuit's under Blazor Server). Calling `ValidateAsync` without services on such a type throws
`InvalidOperationException` naming the type. The same `services` are threaded through nested schemas.

`[ZodAsyncMember(nameof(X))]` declares which members the method validates. `ValidateMemberAsync` then runs
the method only for those members and keeps only the errors it reports under that member's path; without the
attribute the method runs only on full validation.

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

[ZodSchema]
public partial class Account
{
    [Required]
    public string? Handle { get; set; }

    [ZodAsyncMember(nameof(Handle))]
    internal static async ValueTask<ValidationResult<Account>> CustomValidationAsync(
        Account value,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        var handles = (IHandleStore)services.GetService(typeof(IHandleStore))!;
        return await handles.IsTakenAsync(value.Handle!, ct)
            ? ValidationResult<Account>.Failure(
                ValidationError.Create("handle_taken", "Handle is taken", AccountSchema.Paths.Handle)
            )
            : ValidationResult<Account>.Success(value);
    }
}

public interface IHandleStore
{
    ValueTask<bool> IsTakenAsync(string handle, CancellationToken ct);
}
```

```csharp
var full = await AccountSchema.ValidateAsync(account, services, cancellationToken);
var handleOnly = await AccountSchema.ValidateMemberAsync(account, nameof(Account.Handle), services, cancellationToken);
```

`RequiresAsyncValidation` is `true` for `Account`, and for every type that nests it. A type whose graph has no
asynchronous rule completes `ValidateAsync` synchronously, so awaiting it costs nothing.

> [!WARNING]
> The async custom validation method is mutually exclusive with the `OnZodValidate` refinement hook. A
> model must declare exactly one of the two — declaring both is an error (ZODSGEN029).

## Refinement hook (`OnZodValidate`)

Refinement rules are written as a **generator-declared partial method** on the model. The generator emits the
declaration, so the IDE offers the implementation with the correct signature and no name is resolved by
convention:

```csharp
using ZodSharp;
using ZodSharp.Schemas; // RefineCtx<T>

[ZodSchema]
public partial class Order
{
    public decimal Total { get; set; }

    partial void OnZodValidate(RefineCtx<Order> context)
    {
        if (context.Value.Total < 0)
            context.AddIssue("invalid_range", "Total cannot be negative", OrderSchema.Paths.Total);
    }
}
```

Requirements:

- The target type **and every containing type** must be declared `partial` (ZODSGEN034). This is the only
  type-shape requirement the hook adds.
- The signature must be `partial void OnZodValidate(RefineCtx<T> context)`, where `T` is the model type
  (ZODSGEN035). The parameter is a plain by-value `RefineCtx<T>`.
- The hook runs for **every** entry point into the generated schema — `Validate`, `Parse`, the
  `IZodSchemaValidator` adapter, `IValidateOptions`, and any factory that validates through the schema — so
  a rule written here behaves exactly like an attribute rule.
- A type that does not implement the hook allocates nothing: the generated `Validate` only builds a
  `RefineCtx<T>` and calls the hook when a body exists.
- Issues are reported through `context.AddIssue(code, message, path)` — `path` is a `ZodPath` (use the
  generated `{TypeName}Schema.Paths` constants) or a `string[]` — or `context.AddIssue(ValidationError)`, and
  are merged with the attribute-rule issues into one result. An issue added with only a message has code
  `refinement_failed` and category `custom`.
- Refinements state is reported by **ZODSGEN036** if a member still uses the retired
  `IEnumerable<ValidationError> Validate()` contract, which the generator no longer binds.

> [!NOTE]
> Alongside the hook, the generator emits one `internal static` bridge member,
> `InvokeZodRefinementHook(T value, RefineCtx<T> context)`, on the target type. A classic `partial` method is
> private and the generated `{Type}Schema` is a different type, so the bridge is what lets `Validate` reach
> the hook while keeping the hook itself optional. It is not part of the type's API and must not be
> implemented by hand.

## IValidateOptions support

Generated options validators are enabled by:

1. `GenerateIValidateOptions = true` on the attribute, or
2. auto-detection: `GenerateIValidateOptions` unset, target is not a value type, and the type name ends with a configured suffix (default `Options` or `Settings`), or
3. MSBuild override.

MSBuild switches:

| Property | Default | Behaviour |
|---|---|---|
| `DisableZodSharpSourceGenerator` | unset | disables the generator entirely when truthy |
| `ZodSharpAutoGenerateOptionsValidators` | `true` | auto-detect `IValidateOptions` (only explicit `false` disables) |
| `ZodSharpAutoGenerateOptionsValidatorSuffixes` | `Options;Settings` | semicolon/comma-separated suffix list |
| `ZodSharpGenerateJsonSchemas` | `true` | assembly-wide JSON Schema export switch; explicit `false` omits `BuildJsonSchema` and `IJsonSchemaProvider` for every type |
| `ZodSharpEnumSerialization` | `Number` | `String` makes enums export as their member names, matching a global `JsonStringEnumConverter` |
| `ZodSharpEnumNamingPolicy` | `Unchanged` | `CamelCase` applies `JsonNamingPolicy.CamelCase` to string-serialized enum names |
| `ZodSharpAllowNulls` | `false` | assembly-wide default for whether collection properties may contain `null` elements; explicit `true` accepts them |
| `ZodSharpDateTimeOffsetSerialization` | `PreserveOffset` | `Utc` narrows `[NonSentinel] DateTimeOffset` to a zero-offset wire contract; the matching converter must also reject non-UTC input. Both modes export an exact sentinel pattern |

Each failure of a generated options validator reads `{path}: {message} ({code})` (`Bucket: Bucket is required
(missing_field)`), or `{message} ({code})` for a root error.

On .NET 11, when the type has asynchronous rules (a custom async method, an `AsyncValidationAttribute` or
`IAsyncValidatableObject`), the generated `{TypeName}Validator` also implements `IAsyncValidateOptions<T>`.
The platform calls the asynchronous path only from `ValidateOnStart()`; synchronous options access
(`IOptions<T>.Value`, reloads) runs the synchronous rules only.

A type that also has a hand-authored `[OptionsValidator]` class (Microsoft.Extensions.Options' own generator)
would get two validators for the same options (SYSLIB1205), so the ZodSharp validator is not generated and
`ZODSGEN050` is reported. Keep one: remove `[OptionsValidator]`, or set
`[ZodSchema(SuppressIValidateOptions = true)]`. For options validated through the factory instead, see
[Dependency Injection](Dependency-Injection.md#validating-options-objects).

## Trim-safe registration

Every assembly that declares `[ZodSchema]` types gets one registration class that names each generated
validator directly — no attribute scanning and no `Activator` — so it is safe under trimming and Native AOT
(including Blazor WebAssembly):

```csharp
// ZodSharp.Generated.{Assembly}ZodSharpSchemas, generated
var factory = new ZodSchemaFactory();
ZodSharp.Generated.ContosoOrdersZodSharpSchemas.Register(factory);
```

When the project references Microsoft.Extensions.DependencyInjection, a matching extension is generated too:

```csharp
builder.Services.AddZodSharpContosoOrdersSchemas();
```

`{Assembly}` is the assembly name in PascalCase with separators removed: `Contoso.Orders` becomes
`ContosoOrders`, `my-app` becomes `MyApp`, and a name that starts with a digit gains a leading `_`. The
extension adds a `ZodSchemaRegistration` and ensures `AddZodSharpFactory()` has registered the factory, which
applies every registration when it is first resolved — so calls from several model assemblies compose. Prefer
it to `RegisterFromAssembly`, which discovers validators through metadata and is not trim-safe. See
[Dependency Injection](Dependency-Injection.md#registering-source-generated-validators).

## Unions and closed hierarchies (C# 15)

With C# 15 (`net11.0`), a member typed as a `union` is validated with the schema of the **active case**, and a
member typed as a `closed` class with the schema of the derived type it holds. The union's `Value` is not part
of the path:

```csharp
[ZodSchema]
public class Cat
{
    [MinLength(2)]
    public string? Name { get; set; }
}

[ZodSchema]
public class Dog
{
    [Range(1, 100)]
    public int Weight { get; set; } = 1;
}

public union Pet(Cat, Dog);

[ZodSchema]
public class Owner
{
    public Pet Pet { get; set; }
}

// A Cat named "A" reports too_small at "Pet.Name"; TryResolveMember resolves it to the Cat instance.
```

Every derived type of a closed class needs its own `[ZodSchema]`; one without is not validated and is reported
as `ZODSGEN049`.

## Razor components

Razor compiles `.razor` files with its own source generator, and source generators cannot see each other's
output. A `[ZodSchema]` type declared in a `@code` block therefore gets no schema; the generator reports
`ZODSGEN051`. Declare form models in `.cs` files.

## Collection null items

A collection property rejects `null` elements by default: the generated validator reports `invalid_type` at the element's own path (`["Items", 0]`), matching Zod's per-element issue path. The behaviour is controlled by the same setting at several levels, and the most specific statement wins:

| Level | Declaration |
|---|---|
| Property (or positional record parameter) | `[CollectionBehavior(AllowNulls = true)]` / `[CollectionBehavior(AllowNulls = false)]` |
| Element type | an explicitly nullable element (`T?`, for example `List<Child?>` or `Child?[]`) allows nulls |
| Containing type | `[ZodSchema(AllowNulls = true)]` / `[ZodSchema(AllowNulls = false)]` |
| Assembly | `[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]`, then the `ZodSharpAllowNulls` MSBuild property |

Precedence, highest first: the property attribute, the element's `T?` annotation, the containing type's `[ZodSchema]` option, the assembly attribute, the `ZodSharpAllowNulls` MSBuild property, then the built-in default (reject).

### Dictionaries

A property whose type implements `IDictionary<TKey, TValue>` or `IReadOnlyDictionary<TKey, TValue>` is a
**map**, not a sequence: `Dictionary`, `SortedDictionary`, `ConcurrentDictionary`, `ImmutableDictionary`
and custom implementations all qualify. The generated validator iterates the entries, validates each
**value** against the value type's nested schema (discovered recursively, with or without its own
`[ZodSchema]`), and reports a failure at the entry's key — `["Variants", "blue"]` — matching Zod's
`z.record` issue path. Keys are used only in error paths; they are not validated.

The null-items setting above applies to a map's **values**: `Dictionary<string, Child>` rejects a null
value by default, while `Dictionary<string, Child?>` or `[CollectionBehavior(AllowNulls = true)]` accepts
one. `Dictionary<string, int>` has no null case at all, so `[CollectionBehavior]` on it is reported as
`ZODSGEN046`. `[Length]`/`[MinLength]`/`[MaxLength]` count entries, exactly as they count elements for a
sequence.

```csharp
using ZodSharp;

[assembly: ZodSharpSchemaDefaults(AllowNulls = false)]

[ZodSchema]
public class RepositoryInventoryResponse
{
    // Rejected by default: a null element reports invalid_type at ["Items", index].
    public RepositoryInventoryItem[] Items { get; set; } = [];

    // Explicit opt-in for one property.
    [CollectionBehavior(AllowNulls = true)]
    public RepositoryInventoryItem[] OptionalItems { get; set; } = [];

    // A map: the setting governs null values, and failures are reported at ["Variants", key].
    public Dictionary<string, VariantConfiguration> Variants { get; set; } = [];
}
```

> [!NOTE]
> `[CollectionBehavior]` and `[ZodSchema(AllowNulls = ...)]` only take effect when the named argument is present; an attribute that leaves it out defers to the next level. `[CollectionBehavior]` on a property that is not a collection of nullable elements (or a dictionary of nullable values) is reported as `ZODSGEN046`.

A collection whose elements may be null exports its `items` schema with a `null` branch, mirroring how a nullable property is exported. A dictionary exports `{ "type": "object", "additionalProperties": … }` — see [JSON Schema Export](JsonSchema-Export.md#dictionaries).

## DateTimeOffset wire form

The JSON Schema generated for `[NonSentinel] DateTimeOffset` depends on the wire form assumed for the value.
By default offsets are preserved and the pattern is offset-aware: it rejects every offset-equivalent
representation of the instant-based bounds, so no unsupported marker is emitted. An optional narrower UTC
contract is available:

```csharp
// Directory.Build.props
<ZodSharpDateTimeOffsetSerialization>Utc</ZodSharpDateTimeOffsetSerialization>
```

```csharp
using ZodSharp;

[assembly: ZodSharpSchemaDefaults(
    DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc)]

[ZodSchema]
[DateTimeOffsetBehavior(DateTimeOffsetSerialization.Utc)]
public sealed class Observation
{
    public DateTimeOffset ObservedAt { get; set; }

    // Override the project/type default for one property.
    [DateTimeOffsetBehavior(DateTimeOffsetSerialization.PreserveOffset)]
    [JsonConverter(typeof(PreserveOffsetDateTimeOffsetConverter))]
    public DateTimeOffset LegacySeenAt { get; set; }
}
```

Precedence, highest first: `[DateTimeOffsetBehavior]` on the property (or positional record parameter), the
containing type's attribute, the assembly attribute, the `ZodSharpDateTimeOffsetSerialization` MSBuild
property, then `PreserveOffset`. The attribute shapes the schema only; pair `Utc` with the integration
package's `UtcDateTimeOffsetConverter` (and `PreserveOffset` under a global UTC converter with
`PreserveOffsetDateTimeOffsetConverter`). Both modes export an exact sentinel pattern; `Utc` additionally
narrows the accepted wire form to a zero offset. See
[JSON Schema Export](JsonSchema-Export.md#temporal-sentinels-and-the-datetimeoffset-wire-form).

## Automatic enum validation

Every non-flags enum property is validated automatically: the generated validator rejects a value that is not a defined member of the enum type. The check is emitted as an `EnumRule<TEnum>` (see [Validation Rules Reference](Validation-Rules-Reference.md#enum-rules)) and reports `invalid_enum_value`.

A member that is defined but never a valid value can be excluded globally by marking it `[ZodIgnore]`, and excluded for a single property with `[DeniedValues]`:

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

public enum ExampleEnum
{
    [ZodIgnore]
    Unspecified,

    AValidValue,

    AnotherValidValue,
}

[ZodSchema]
public class Model
{
    // Rejects anything that is not AValidValue or AnotherValidValue.
    public ExampleEnum Status { get; set; }

    // Also rejects AnotherValidValue for this property only.
    [DeniedValues(ExampleEnum.AnotherValidValue)]
    public ExampleEnum SecondaryStatus { get; set; }
}
```

The automatic validation is skipped when:

- the enum is declared `[Flags]` — a combination is a valid value without being a defined member;
- the property declares an explicit `[AllowedValues]` allow-list, which governs the property instead;
- the schema opts out with `[ZodSchema(ValidateEnumValues = false)]`.

A nullable enum property is validated only when it is not `null`.

## What is validated

- Properties must be public, non-static, non-indexer.
- Validation is emitted for a property when it carries any DataAnnotations attribute, its type is a source-defined complex type with a nested schema, or its type is an enum (see [Automatic enum validation](#automatic-enum-validation)).
- Classes, structs, and records are supported; structs do not receive `IValidateOptions` (ZODSGEN028 if requested).
- Nested complex types are discovered recursively and get their own generated `{TypeName}Schema`, even when the nested type does not itself carry `[ZodSchema]`.
- Nullable properties are null-guarded before value-set/type validation; a nullable target rejects `null` with `invalid_type`.
- A target implementing `IValidatableObject` is validated by it after the members and type rules pass, as DataAnnotations and Microsoft.Extensions.Validation do. On .NET 11, `IAsyncValidatableObject` and `AsyncValidationAttribute` members run in `ValidateAsync`; a synchronous `Validate` of such a type throws `InvalidOperationException` (`ZODSGEN048` warns at build time). See [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md#ivalidatableobject-and-asynchronous-dataannotations).

- A `Purview.ValueObjects` scalar marked with `[Scalar]` can carry `[ZodSchema]` on the same type; the generated validator validates the scalar as a unit and reports an empty path. A rule written against the scalar's underlying value is adapted automatically, or resolved against the unwrapped value and null-guarded when the underlying value is a `Nullable<T>` — see [Value Objects Integration](Value-Objects-Integration.md).
- A rule marked with the parameterless `[ZodRule]` generates a matching validation attribute. Every built-in rule ships its attribute inside `Purview.ZodSharp` (in the `ZodSharp.Rules` namespace — `[Email]`, `[E164]`, `[Regex]`, `[NonSentinel]`, `[MinValue]`, `[Even]`, …; names that collide with `System.ComponentModel.DataAnnotations` use a `Zod` suffix such as `[MinLengthZod]`), so they can annotate a member or a scalar value object directly — see [Built-in attributes](Custom-Rules.md#built-in-attributes-shipped-with-purviewzodsharp).

See [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) for the attribute coverage and structured issue shape, [Custom Rules](Custom-Rules.md) for extending validation with your own rules and attributes, and [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the `ZODSGEN*` diagnostics.
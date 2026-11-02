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

## Error code and message definitions

Every rule **should** expose its error identity as public constants so code (and tests) can assert against the rule rather than re-typing literals:

```csharp
public readonly record struct EmailRule : IValidationRule<string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Invalid email format";

    public string Code => ErrorCode;
    public string GetErrorMessage(in string value) => RuleMessage.Format(MessageFormat, value);
}
```

- **`ErrorCode`** is the rule's canonical code — the value a test compares against when no per-usage override is supplied. The interface member `IValidationRule<T>.Code` defaults to `"validation_failed"`; a rule that accepts a per-usage `code` override returns it (falling back to `ErrorCode`) from both `Code` and `IZodRule.Code`, so every route reports the same effective value.
- **`MessageFormat`** is a positional template. `{0}` (and `{1}`, …) are the offending value and any rule-specific arguments. Format it with `ZodSharp.Rules.RuleMessage.Format(format, …)`, which uses the invariant culture (messages are culture-independent; hosts localize at the boundary - see [Localization](Localization.md)), caches the parsed format and **never throws**: a custom message with a stray brace or an index you do not supply is written out literally instead of raising `FormatException` at validation time.
- **Keep the input out of the default message.** Errors leave the process (ProblemDetails, logs, UI), so a default `MessageFormat` should describe what was expected rather than quote the value (`"Invalid email format"`, not `"Invalid email format: {0}"`). The built-in rules follow this; see [Core Concepts: PII](Core-Concepts.md#pii).
- The constants may be inherited from a base rule class, and **abstract bases are exempt**, so a shared base can host them for its concrete derivations.

The convention is enforced by an analyzer: a source-declared rule that does not expose a public `const string ErrorCode` **and** a public `const string MessageFormat` is reported as **ZODSGEN042**. See [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the full list.

A `[Test]` can therefore assert without duplicating strings:

```csharp
await Assert.That(error.Code).IsEqualTo(EmailRule.ErrorCode);
await Assert.That(error.Message).IsEqualTo(EmailRule.MessageFormat);
```

### Runtime identity: `IZodRule`

The constants are the rule's *static* default. When one attribute must produce a different code per annotated member, the rule implements `ZodSharp.Core.IZodRule` and supplies its own code/origin at runtime:

```csharp
public interface IZodRule
{
    string? Code { get; }     // null falls back to the attribute-mapped code
    string? Origin { get; }   // null falls back to the attribute-mapped origin

    // Optional structured identity (default interface members returning null):
    string? Category => null;                                   // the Zod issue code (ZodIssueCategories)
    ErrorBound? Lower => null;                                  // the bounds the rule enforces
    ErrorBound? Upper => null;
    string? MessageFormat => null;                              // a named-placeholder template hosts may re-format
    IReadOnlyDictionary<string, object?>? Parameters => null;   // e.g. format, pattern, divisor
}
```

`ZodRuleErrors` (`ZodSharp.Core`) copies every member onto the reported `ValidationError`, so a runtime schema and a
generated validator report exactly the same error for the same rule. Every built-in rule implements the
structured members.

The generated code casts to `IZodRule` when reading the values, so the identity can depend on the rule's constructor arguments. A rule that accepts a `code`/`origin` constructor parameter **without** implementing `IZodRule` never reaches the reported error identity and is reported as **ZODSGEN039**.

The reported code and origin are resolved from the rule (when it implements `IZodRule`), then the applied attribute, then the `[ZodRule(typeof(...))]` mapping, and finally default to `validation_failed` — see [Error identity: code and origin precedence](#error-identity-code-and-origin-precedence) for the full order.

### Value-dependent failures: `IZodFailureDescriber<T>`

Some failures depend on the value: a range rule is `too_small` below the range and `too_big` above it, and a
length rule reports the length it saw. Implement `IZodFailureDescriber<T>` (or `IZodStringFailureDescriber` for
the span path) to describe that part; it is called only after the rule rejected the value, and its result is
merged over the rule's static identity:

```csharp
public readonly record struct BetweenRule(int Min, int Max)
    : IValidationRule<int>, IZodRule, IZodFailureDescriber<int>
{
    public const string ErrorCode = "out_of_range";
    public const string MessageFormat = "Value must be between {0} and {1}";

    public bool IsValid(in int value) => value >= Min && value <= Max;

    public string GetErrorMessage(in int value) => RuleMessage.Format(MessageFormat, Min, Max);

    public string Code => ErrorCode;

    string? IZodRule.Code => ErrorCode;
    string? IZodRule.Origin => ZodTypeNames.Number;
    ErrorBound? IZodRule.Lower => ErrorBound.Including(Min);
    ErrorBound? IZodRule.Upper => ErrorBound.Including(Max);

    public ZodRuleFailure DescribeFailure(in int value) =>
        new(Category: value < Min ? ZodIssueCategories.TooSmall : ZodIssueCategories.TooBig);
}
```

`ZodRuleFailure(string? Category = null, IReadOnlyDictionary<string, object?>? Parameters = null)`: a `null`
category keeps `IZodRule.Category`, and the parameters are merged over `IZodRule.Parameters`. When
`ZodErrorOptions.IncludeReceivedValues` is enabled (development only), `ZodRuleErrors` also adds the raw input as
the `received` parameter.

### Message overrides

The reported message is resolved in this order:

1. A `message` argument on the rule (typically a `Message` constructor parameter), when set.
2. The attribute's `ErrorMessage` / `ErrorMessageResourceName` / `ErrorMessageResourceType`, mapped to a constructor parameter named `message`.
3. `GetErrorMessage(value)`, which formats `MessageFormat` with the offending value.

`MessageFormat` is therefore the fallback, not the only message.

## Non-sentinel values (EF-friendly)

`ZodSharp.Rules.NonSentinelRule<T>` rejects the framework default/boundary values an ORM commonly stores to represent "no value" — `Guid.Empty`, `DateTime.MinValue`/`MaxValue`, `DateTimeOffset.MinValue`/`MaxValue`, `DateOnly.MinValue`/`MaxValue`, `TimeOnly.MinValue`/`MaxValue`, and `null`/empty/whitespace strings:

```csharp
var schema = Z.Date().AddRule(new NonSentinelRule<DateTime>());

var result = schema.Validate(DateTime.MinValue);
// result.Errors[0].Code == NonSentinelRule<DateTime>.ErrorCode   ("invalid_value")
```

Close it with the property type to use it through an attribute (`[ZodRule(typeof(NonSentinelRule<>))]` on a matching `NonSentinelAttribute`). Types without a known sentinel always pass, so the rule never rejects a type it does not understand.

## Defining a custom rule

```csharp
using ZodSharp.Core;
using ZodSharp.Rules;

namespace MyRules;

/// <summary>Rejects strings that contain whitespace.</summary>
public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Whitespace is not allowed.";

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

    public string Code => ErrorCode;

    public string GetErrorMessage(in string value) =>
        Message ?? RuleMessage.Format(MessageFormat, value);
}
```

Every rule **should** declare the public `ErrorCode`/`MessageFormat` constants (see [Error code and message definitions](#error-code-and-message-definitions)); omitting them is reported as `ZODSGEN042`, and a rule without them reports the fallback code `validation_failed` when it does not also implement `IZodRule`.

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
// result.Errors[0].Code    == NoWhitespaceRule.ErrorCode ("invalid_string")
// result.Errors[0].Message == "No spaces allowed."
// result.Errors[0].Path    is empty
```

Both methods mutate the receiver and return it for chaining; see [Guarantees and Limitations](Guarantees-and-Limitations.md#fluent-rule-methods-mutate-the-receiver).

## Extending the fluent interface

Every built-in rule has a dedicated method on its schema — `Z.String().Email()`, `Z.Number().Min(...)`, `Z.Date().NonSentinel()`, and so on. A custom rule gets the same ergonomics with an **extension method** that adds the rule to the receiver and returns it:

```csharp
using ZodSharp.Schemas;

namespace MyRules;

public static class ZodStringRuleExtensions
{
    /// <summary>Rejects strings that contain whitespace.</summary>
    public static ZodString NoWhitespace(this ZodString schema, string? message = null)
    {
        schema.AddRule(new NoWhitespaceRule(message));
        return schema;
    }
}
```

The call site then mirrors the built-in surface and stays chainable:

```csharp
var schema = Z.String().Min(3).NoWhitespace().ToUpper();
```

Two details keep the chain intact:

- **Return the concrete schema type** (`ZodString`, `ZodDate`, …), not the base `ZodType<TOutput, TInput>`. The generic `Rule<TRule>` helper and `AddRule` return the base type, so write `schema.Rule(new MyRule()); return schema;` (or call `AddRule` and return `schema`) rather than returning the result of `Rule(...)` directly.
- **Target the schema whose output type the rule validates.** A rule implementing `IValidationRule<string>` extends `ZodString`; a rule implementing `IValidationRule<Guid>` extends whichever schema produces a `Guid`. For a rule that applies to *any* schema, extend the single-type-argument base `ZodType<T>`:

```csharp
using ZodSharp.Core;

public static class ZodTypeRuleExtensions
{
    /// <summary>Rejects the default value of the schema's output type.</summary>
    public static ZodType<T> NotEmpty<T>(this ZodType<T> schema, string? message = null)
        where T : struct, IEquatable<T>
    {
        schema.AddRule(new NotEmptyRule<T>(Message: message));
        return schema;
    }
}
```

A fluent extension only covers the runtime API. To make the same rule usable from `[ZodSchema]` models, map it to an attribute with `[ZodRule]` (see below). The built-in catalogue — including each rule's `ErrorCode` and `MessageFormat` — is in [Validation Rules Reference](Validation-Rules-Reference.md).

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
> The attribute's arguments are mapped to the rule's public constructor parameters: named arguments by name (case-insensitive), and positional arguments by the applied attribute's own constructor parameter names first (with the raw position as a fallback for hand-authored attributes whose parameter names differ from the rule's). A parameter named `message` is supplied from the attribute's `ErrorMessage` when one is set.

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
    public const string ErrorCode = "invalid_value";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in T value) => !value.Equals(default(T));

    public string GetErrorMessage(in T value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFormat);

    // The rule owns its identity, so callers can pass a per-member error code.
    string? IZodRule.Code => Code;

    string? IZodRule.Origin => "value_object";
}
```

### Declaring the identity contract

A hand-authored attribute supplies those named arguments by exposing `Code` / `Origin` properties. Implement `ZodSharp.Core.IZodRuleAttribute` to make that contract explicit — the interface requires both members, so the compiler guarantees the properties the generator reads are present, and any member added to the interface is treated as identity rather than as an unconsumed argument:

```csharp
public sealed class NoWhitespaceAttribute : ValidationAttribute, IZodRuleAttribute
{
    public string? Code { get; set; }

    public string? Origin { get; set; }
}
```

Attributes that do not implement the interface are still read through the documented `Code` / `Origin` names, so existing declarations keep working.

Two warnings keep the identity honest rather than silently dropped:

| Situation | Diagnostic |
|---|---|
| A rule accepts a `code`/`origin` constructor parameter but does not implement `IZodRule`, so the generated validation supplies the value and never reads it back. | `ZODSGEN039` |
| An attribute supplies an argument the resolved rule never consumes: no matching constructor parameter, and not part of the error identity (`Code`/`Origin`/`IZodRuleAttribute` members) or the inherited `ErrorMessage`/`ErrorMessageResourceName`/`ErrorMessageResourceType` members. | `ZODSGEN040` |

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

### Rule families: one attribute for a primitive and a scalar value object

A constraint can also be *self-referential* (`where TSelf : IScalarValueObject<TSelf, string>`), which a primitive can never satisfy — `string` does not implement `IScalarValueObject<string, string>`. Declare both halves of the rule side by side and the generator resolves the member that fits the annotated type:

```csharp
namespace MyRules;

// Member-level half: validates the underlying primitive.
public readonly record struct NonWhiteSpaceStringRule(string? Message = null)
    : IValidationRule<string?>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in string? value) => value != null && !string.IsNullOrWhiteSpace(value);

    public string GetErrorMessage(in string? value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFormat);
}

// Value-object half: validates the scalar as a unit.
public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Code = null, string? Message = null)
    : IValidationRule<TSelf>, IZodRule
    where TSelf : IScalarValueObject<TSelf, string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Value must not be empty.";

    public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);

    public string GetErrorMessage(in TSelf value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFormat);

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

- `[NonWhiteSpaceString]` on a `string`/`string?` member resolves to `NonWhiteSpaceStringRule` (the non-generic sibling).
- `[NonWhiteSpaceString]` on a scalar type resolves to `NonWhiteSpaceStringRule<ThatScalar>`.
- Resolution is symmetric: mapping the attribute to the *non-generic* rule still resolves the generic member for a scalar target.
- When no member of the family can validate the target type, `ZODSGEN030` is reported and nothing is emitted.

## Generating the attribute from the rule

If you do not want to hand-write the attribute, mark the rule itself with the parameterless `[ZodRule]` and the generator emits a matching attribute:

```csharp
using ZodSharp.Core;

namespace MyRules;

[ZodRule(Code = "invalid_string", Origin = "string")]
public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
    : IValidationRule<string>
{
    public const string ErrorCode = "invalid_string";
    public const string MessageFormat = "Whitespace is not allowed.";

    public bool IsValid(in string value) => AllowEmpty || value.IndexOf(' ') < 0;

    public string GetErrorMessage(in string value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFormat);
}
```

This produces a `NoWhitespaceAttribute` in the rule's namespace, shaped like:

```csharp
/// <summary>Validation attribute that applies NoWhitespaceRule.</summary>
[global::System.AttributeUsage(
    global::System.AttributeTargets.Class
        | global::System.AttributeTargets.Struct
        | global::System.AttributeTargets.Property
        | global::System.AttributeTargets.Field
        | global::System.AttributeTargets.Parameter,
    Inherited = true,
    AllowMultiple = false)]
[global::ZodSharp.Core.ZodRule(typeof(global::MyRules.NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
public sealed class NoWhitespaceAttribute
    : global::System.ComponentModel.DataAnnotations.ValidationAttribute
{
    /// <summary>Initializes the attribute with the values required by NoWhitespaceRule.</summary>
    public NoWhitespaceAttribute(bool allowEmpty = true)
    {
        AllowEmpty = allowEmpty;
    }

    public bool AllowEmpty { get; set; } = true;

    public string? Message { get; set; } = null;
}
```

Mapping rules:

- The attribute name is the rule name with a trailing `Rule` replaced by `Attribute` (`NoWhitespaceRule` → `NoWhitespaceAttribute`). Override it with `[ZodRule(AttributeName = "…")]`.
- Each public constructor parameter becomes a settable property, Pascal-cased, with the parameter's default value preserved. The rule's **value** parameters are additionally emitted as a constructor: a parameter the rule declares without a default becomes a required constructor argument, so an attribute like `[MinValue]` cannot be applied without its bound, while a parameter with a default keeps that default (so `[NoWhitespace]` still works). A rule that overloads its constructor surfaces one attribute constructor per attribute-addressable overload, and the applied constructor selects the matching rule overload — `UUIDRule` yields both `[UUID]` (the versionless overload) and `[UUID(UuidVersion.V4)]` (the versioned one). A parameter named `message` becomes a `Message` property (the resolver maps it onto the rule's `message` argument) and defaults to the rule's `MessageFormat`; a parameter named `code`/`origin` becomes a `Code`/`Origin` property and `Code` defaults to the rule's `ErrorCode`; the inherited `ValidationAttribute.ErrorMessage` remains the fallback. A type-parameter parameter (for example the bound of `MinValueRule<T>`) is surfaced as a `double`.
- If the derived name collides with a `System.ComponentModel.DataAnnotations` attribute, the generated attribute is emitted under a `Zod` suffix (`MinLengthAttribute` → `MinLengthZodAttribute`, used as `[MinLengthZod]`).
- The attribute is always decorated with `AttributeTargets.Class | Struct | Property | Field | Parameter`, so it can annotate a member or a scalar value object. (`Class`/`Struct` are what make the [type-level form](#type-level-rules) possible.)
- The rule must be non-nested and non-abstract, and every parameter type must be a legal attribute-argument type (primitive, `string`, `enum`, `System.Type`).
- An **arity-1 generic rule** can be marked as well: the generated attribute maps to the open generic (`[ZodRule(typeof(NonWhiteSpaceStringRule<>))]`), which is the form that serves both a primitive member and a scalar value object. Rules with two or more type parameters are rejected (`ZODSGEN032`).
- When both halves of a [rule family](#rule-families-one-attribute-for-a-primitive-and-a-scalar-value-object) are marked, only the generic half emits the attribute; the two mappings would otherwise claim the same name.
- If a hand-authored type already declares the derived name, the generated attribute is suppressed and reported as `ZODSGEN037`. The hand-authored declaration's own `[ZodRule]` mapping then governs every usage of that attribute name, so confirm it still matches what the call sites expect.
- `[ZodRule(AllowMultiple = true)]` emits `AttributeUsage(..., AllowMultiple = true)`, so the attribute may be applied to a member more than once. Each application becomes its own rule, configured from that application's arguments and evaluated in source order:

```csharp
[ZodRule(AllowMultiple = true)]
public readonly record struct MultipleOfRule(int Factor = 1, string? Message = null) : IValidationRule<int>
{
    public const string ErrorCode = "not_multiple_of";
    public const string MessageFormat = "Number must be a multiple of {0}";

    public bool IsValid(in int value) => Factor != 0 && value % Factor == 0;

    public string GetErrorMessage(in int value) =>
        Message ?? string.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFormat, Factor, value);
}

[ZodSchema]
public partial class Sample
{
    [MultipleOf(Factor = 3)]
    [MultipleOf(Factor = 5)]
    public int Value { get; set; }   // must be a multiple of both 3 and 5
}
```

- A **hand-authored** attribute whose name encodes a rule name (`XAttribute` → `XRule`) must map to a rule that addresses every rule declared under that name. A mapping that declares only a non-generic rule while an arity-1 generic sibling exists (or that declares an unrelated rule) is reported as `ZODSGEN038`, because some usages of the attribute would resolve to no rule. An attribute name that does not encode a declared rule family is left alone, so free-form names remain valid.

> [!IMPORTANT]
> The generated attribute lives in the same assembly as the rule, but Roslyn generators cannot read another generator's output as a symbol. To *consume* a generated attribute with `[ZodSchema]`, reference the rule from a separate assembly (a rules library) — or hand-author the attribute and mark it with `[ZodRule(typeof(...))]`. The built-in rules below already satisfy this: their attributes ship inside `Purview.ZodSharp`.

## Built-in attributes (shipped with Purview.ZodSharp)

Every built-in rule that can be expressed as an attribute is generated once into the `Purview.ZodSharp` assembly, in the `ZodSharp.Rules` namespace, so a consumer can annotate a member or a scalar without hand-authoring anything:

```csharp
using ZodSharp;
using ZodSharp.Rules;

[ZodSchema]
public partial class Contact
{
    [Email]
    public string Email { get; set; } = string.Empty;

    [E164]
    public string Phone { get; set; } = string.Empty;

    [Regex("^[a-z]+$")]
    public string Code { get; set; } = string.Empty;

    [NonSentinel(Message = "Id must not be the default.")]
    public Guid Id { get; set; }
}
```

Each attribute mirrors its rule's constructor parameters and reports the rule's own `ErrorCode`/`Origin` (every built-in rule implements `IZodRule`). A value the rule declares without a default is a **required constructor argument** — `[Regex("^[a-z]+$")]`, `[UUID(UuidVersion.V4)]`, `[MinValue(3)]` — so it can never be silently omitted; a value with a default keeps it (for example `[StartsWith("https://", StringComparison.OrdinalIgnoreCase)]`). A rule with overloaded constructors mirrors each overload, so `[UUID]` uses the versionless overload and `[UUID(UuidVersion.V4)]` (or `[UUID(Version = UuidVersion.V4)]`) the versioned one. `[Regex]` uses the `(string pattern, string? message)` overload, so `Pattern` is a string. A rule's `message` parameter is surfaced as a `Message` property (defaulting to the rule's `MessageFormat`) and its `code` parameter (where present) as a `Code` property (defaulting to the rule's `ErrorCode`). A type-parameter value is surfaced as a `double`; an array of type parameters (for example the allowed values of `AllowedValuesRule<T>`) is surfaced as a `params object[]`, with each element converted back to the member type at the usage site, so `[AllowedValuesZod("a", "b", "c")]` works on a `string` member and `[DeniedValuesZod(1, 2, 3)]` on an `int` member.

Two adjustments keep every rule addressable:

- **Name collisions.** `MinLengthRule`, `MaxLengthRule`, `UrlRule`, `PhoneRule`, `CreditCardRule`, `Base64StringRule`, `RequiredRule`, `RangeRule`, `LengthRule`, `StringLengthRule`, `CompareRule`, `AllowedValuesRule`, and `DeniedValuesRule` derive an attribute name that `System.ComponentModel.DataAnnotations` already uses. Their attributes are emitted under a `Zod` suffix instead — `[MinLengthZod]`, `[MaxLengthZod]`, `[UrlZod]`, `[PhoneZod]`, `[CreditCardZod]`, `[Base64StringZod]`, `[RequiredZod]`, `[RangeZod]`, `[LengthZod]`, `[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`, `[DeniedValuesZod]` — so the rule's own `Code`/`Message` stay usable alongside the DataAnnotations attribute.
- **Generic bounds.** The generic bound rules (`MinValueRule<T>`, `MaxValueRule<T>`, `GreaterThanRule<T>`, `LessThanRule<T>`, `GreaterThanOrEqualRule<T>`, `LessThanOrEqualRule<T>`) surface their type-parameter bound as a `double`, so `[MinValue(3)]` works on an `int` or a `double` member (the value is converted to the member type).

The numeric parity and inclusive-comparison rules follow the same pattern: `[GreaterThanOrEqual(…)]`, `[LessThanOrEqual(…)]`, `[Even]`, and `[Odd]`.

`[Enum]` mirrors `EnumRule<TEnum>` and closes the open generic with the annotated enum member type. The `[ZodSchema]` generator applies the rule automatically to enum properties; `[ZodIgnore]` on an enum member excludes that member from the rule everywhere the enum is validated, and a property's `[DeniedValues]` excludes values for that property only. See [Enum rules](Validation-Rules-Reference.md#enum-rules).

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
            origin: ((global::ZodSharp.Core.IZodRule)assetIdCustomRule0).Origin));
}
```

- **Generic closure:** a type-level attribute closes an unbound generic rule with the **target type** (`NotEmptyRule<AssetId>`), so the rule sees the value object and can read its state through its own constraints. On a `[Scalar]` type, a rule written against the underlying value is instead closed with that value and wrapped — see [Value Objects Integration](Value-Objects-Integration.md).
- **Ordering** in the generated `Validate`: property rules → **type-level rules** → the synchronous `Validate()` refinement.
- Type-level attributes need `AttributeTargets.Class`/`Struct` on the attribute declaration; the property-level attributes above only need `Property`/`Field`.

### When a type-level rule does not run

- **The type gets no schema.** The generator is driven by `[ZodSchema]` and then walks nested complex property types. A rule attribute on a type that gets no schema is **ignored**, and the analyzer reports the warning **`ZODSGEN033`** so the mistake is visible. A type without `[ZodSchema]` that is referenced as a complex property of a schema *does* get a (secondary) schema, so its type-level rules run and no warning is raised.
- **`[ZodSchema(GenerateValidateMethod = false)]`.** Type-level rules live inside `Validate`, so they are omitted along with it.
- **`DisableZodSharpSourceGenerator`.** The generator — and therefore every rule — is skipped.

## Validating scalar value objects

A `Purview.ValueObjects` scalar **is** a single value, so validate it as a unit rather than through its `Value` property. Attach a rule at the **type level** (on the scalar type, not on its `Value` property) and the reported error has an **empty path**:

```csharp
[Scalar]
[ZodSchema]
[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
public readonly partial record struct AssetId
{
    public Guid Value { get; init; }
}
```

```text
Code    = "invalid_asset_id"
Message = "AssetId must not be empty."
Origin  = "value_object"
Path    = []
```

A rule can be written against the value object itself (`where TSelf : IScalarValueObject<TSelf, TValue>`), or against the underlying value (`IValidationRule<Guid>`) and adapted automatically by the value-objects generator's `ScalarRuleAdapter`.

> [!IMPORTANT]
> `Purview.ValueObjects.ScalarRuleAdapter<TSelf, TValue, TRule>` is emitted into your compilation by the **Purview.ValueObjects** source generator whenever the project references both `Purview.ValueObjects` and `Purview.ZodSharp`. Do not declare it yourself. If the value-objects generator is disabled (`DisableValueObjectsSourceGenerator`), the adapter is not emitted and the generated validator will not compile.

> [!NOTE]
> If the type has no value-object contract (a plain class with a `Guid` property), the property-level form still works: map the attribute to an `IValidationRule<Guid>` and put it on `Value`. See [Exposing a rule as a DataAnnotations attribute](#exposing-a-rule-as-a-dataannotations-attribute) and [Generic rules](#generic-rules).

> [!TIP]
> If the non-empty policy should be implicit rather than an attribute, the value-objects layer is the natural place to emit the attribute on the scalar type (it already knows about ZodSharp through `ZodSchemaMode`).

See [Value Objects Integration](Value-Objects-Integration.md) for the full walkthrough: wiring `[Scalar]` + `[ZodSchema]`, both rule shapes, the generated code, the error code / message definitions, and testing.

## Diagnostics

| ID | Severity | Meaning |
|---|---|---|
| ZODSGEN030 | Error | The mapped rule does not implement `IValidationRule<T>` for the property type (or the rule target type), or an unbound generic rule could not be closed with it. |
| ZODSGEN031 | Error | A rule constructor parameter could not be mapped from the attribute. |
| ZODSGEN032 | Error | A validation attribute could not be generated for the rule. |
| ZODSGEN033 | Warning | A rule-mapped attribute is applied to a type that gets no generated schema (no `[ZodSchema]` and not referenced as a complex property), so the rule never runs. |
| ZODSGEN037 | Warning | A rule marked `[ZodRule]` derives an attribute name that is already declared by hand, so the generated attribute is suppressed and the hand-authored declaration's own `[ZodRule]` mapping governs every usage. |
| ZODSGEN038 | Warning | A hand-authored rule attribute's `[ZodRule(typeof(...))]` mapping does not address every rule declared under the name the attribute encodes (`XAttribute` → `XRule`), leaving some usages of the attribute unresolved. |
| ZODSGEN039 | Warning | A rule accepts a `code`/`origin` constructor parameter without implementing `IZodRule`, so the value never reaches the reported error identity. |
| ZODSGEN040 | Warning | An attribute argument has no effect: the resolved rule has no matching constructor parameter and the value is not part of the reported error identity. |
| ZODSGEN042 | Warning | A validation rule does not expose public `const string ErrorCode` / `MessageFormat` constants, so its error identity cannot be asserted in tests without duplicating literals. |
| ZODSGEN043 | Info | A built-in rule marked `[ZodRule]` does not generate a validation attribute (its name is taken by a `System.ComponentModel.DataAnnotations` attribute, or a constructor parameter cannot be represented as an attribute property). |

See [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the full list.

## Related

- [Validation Rules Reference](Validation-Rules-Reference.md) — the catalogue of built-in rules.
- [Fluent Schema API](Fluent-Schema-API.md) — `AddRule`/`Rule` live on `ZodType`.
- [Value Objects Integration](Value-Objects-Integration.md) — `[Scalar]` value objects and the `ScalarRuleAdapter`.
- [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) — built-in attribute coverage.
- [Guarantees and Limitations](Guarantees-and-Limitations.md) — allocation and mutation semantics.


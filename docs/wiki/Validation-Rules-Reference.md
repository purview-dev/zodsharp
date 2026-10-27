# Validation Rules Reference

Every built-in validation rule lives in the `ZodSharp.Rules` namespace and is a `readonly record struct` implementing `ZodSharp.Core.IValidationRule<T>`. Rules are the smallest unit of validation: a schema is a type/structural check followed by an ordered list of rules, and each failing rule adds a `ValidationError` to the result.

This page is the catalogue of the rules that ship with the core package. For authoring your own, see [Custom Rules](Custom-Rules.md).

## The rule contract

```csharp
namespace ZodSharp.Core;

public interface IValidationRule<T>
{
    bool IsValid(in T value);
    string GetErrorMessage(in T value);
    string Code => "validation_failed";
}
```

- String rules additionally implement `ZodSharp.Core.IStringValidationRule` (`bool IsValid(ReadOnlySpan<char>)` / `string GetErrorMessage(ReadOnlySpan<char>)`) so `ZodString.ValidateSpan`/`IsValidSpan` can validate without materialising the input.
- Every rule exposes its error identity as public constants: `public const string ErrorCode` and `public const string MessageFormat` (a `{0}`-style positional template). `Code` returns the rule's effective code — a per-usage `code` override when supplied, otherwise `ErrorCode` — and `GetErrorMessage` formats `MessageFormat` with `RuleMessage.Format` (which caches the parsed `CompositeFormat`) using the invariant culture.
- A constructor `message` parameter, when supplied, overrides the formatted `MessageFormat`. `MessageFormat` is the fallback, not the only message; a rule built without a message and `default(TRule)` behave the same. A custom message may use the rule's positional placeholders, and formatting **never throws**: a stray brace or an index the rule does not supply is written out literally.
- Default messages do not quote string input: format, prefix/suffix/substring, compare, allowed/denied and null-or-non-whitespace rules describe what was expected (`"String must start with 'PUR-'"`). Numeric rules still state the failing number and length rules the length. `RegexRule`'s `{0}` is the pattern.
- Every built-in rule implements `IZodRule`, so its failure carries a full structured identity: `Origin` (`string`, `number`, `date`, `enum`, …), `Category` (the Zod issue code: `invalid_format` for string formats, `too_small`/`too_big` by direction for bound, length and range rules, `not_multiple_of`, `invalid_type` for `Int`, `invalid_value` for value lists), `Lower`/`Upper` bounds with the correct inclusivity (`GreaterThan`/`LessThan` are exclusive) and parameters such as `format`, `pattern`, `prefix`, `suffix`, `includes`, `divisor`, `options`, `version`, `expected` and `count`. See [Core Concepts: Error fields](Core-Concepts.md#error-fields).
- The analyzer reports `ZODSGEN042` when a source-declared rule omits `ErrorCode`/`MessageFormat`. See [Source Generator Diagnostics](Source-Generator-Diagnostics.md).

Rules can be used three ways:

```csharp
using ZodSharp;
using ZodSharp.Rules;

// 1. Standalone
var rule = new EmailRule();
if (!rule.IsValid("not-an-email"))
    Console.WriteLine(rule.GetErrorMessage("not-an-email"));

// 2. Attached to a schema (AddRule or the generic Rule helper)
var schema = Z.String().AddRule(new EmailRule());
var same = Z.String().Rule(new EmailRule());

// 3. Surfaced as a DataAnnotations attribute the [ZodSchema] generator honours
//    (built-in mappings, or a custom rule via [ZodRule]). See Source Generator DataAnnotations.
```

Tests can assert against the rule's constants instead of duplicating literals:

```csharp
var result = Z.String().Email().Validate("not-an-email");
await Assert.That(result.Errors[0].Code).IsEqualTo(EmailRule.ErrorCode);          // "invalid_string"
await Assert.That(result.Errors[0].Message).IsEqualTo(EmailRule.MessageFormat);   // "Invalid email format"
await Assert.That(result.Errors[0].Category).IsEqualTo(ZodIssueCategories.InvalidFormat);
```

## Error codes

The built-in rules report one of the following codes as `Code`. `Category` always carries the matching Zod v4 issue code (`ZodIssueCategories`), so a client can switch on `Category` without knowing ZodSharp's specific codes:

| Code | Meaning | Emitted by |
|---|---|---|
| `invalid_string` | A string did not match the required format | all string format rules |
| `invalid_type` | A value was not the expected type/shape (e.g. not a whole number) | `IntRule<T>` |
| `too_small` | A length or value fell below a lower bound | `MinLengthRule`, `MinValueRule<T>`, `GreaterThanRule<T>` |
| `too_big` | A length or value exceeded an upper bound | `MaxLengthRule`, `MaxValueRule<T>`, `LessThanRule<T>`, `SafeIntegerRule` (whose `Category` is `too_small` below the range and `invalid_type` for a fraction) |
| `not_multiple_of` | A number was not a multiple of the divisor | `MultipleOfRule<T>` |
| `not_finite` | A number was `NaN` or infinite | `FiniteRule<T>` |
| `invalid_value` | A value was a rejected sentinel or was not in the allowed set | `NonSentinelRule<T>`, `AllowedValuesRule<T>`, `DeniedValuesRule<T>` |
| `invalid_enum_value` | A value was not a defined member of the enum type, or was an excluded member | `EnumRule<TEnum>` |
| `missing_field` | A required value was absent (or empty/whitespace-only) | `RequiredRule<T>` |
| `invalid_length` | A string length fell outside the required range (`Category` `too_small`/`too_big` by direction) | `LengthRule`, `StringLengthRule`, and generated `[StringLength]`/`[Length]` on strings |
| `invalid_range` | A comparable value fell outside the required range (`Category` `too_small`/`too_big` by direction) | `RangeRule<T>` |
| `mismatch` | A value did not equal the expected value | `CompareRule` |
| `validation_failed` | Fallback for rules that do not declare an identity | any rule without `ErrorCode`/`IZodRule` |

## String rules

Fluent methods live on `ZodString` (`ZodSharp.Schemas`). The `Span` column marks rules that implement `IStringValidationRule` and therefore stay on the zero-allocation span path.

### Length

| Rule | Constructor | Fluent method | Code | Message format | Span |
|---|---|---|---|---|---|
| `MinLengthRule` | `(int minLength)` | `Min(n)`, `Length(n)` | `too_small` | `String must be at least {0} characters long, but got {1}` | yes |
| `MaxLengthRule` | `(int maxLength)` | `Max(n)`, `Length(n)` | `too_big` | `String must be at most {0} characters long, but got {1}` | yes |

`Z.String().Length(n)` adds **both** rules, so the string must be exactly `n` characters.

### Format

| Rule | Constructor | Fluent method | Code | Message format | Span |
|---|---|---|---|---|---|
| `EmailRule` | `()` | `Email()` | `invalid_string` | `Invalid email format` | yes |
| `RegexRule` | `(Regex pattern, string? message)` or `(string pattern, string? message)` | `Regex(...)` | `invalid_string` | `String does not match the required pattern: {0}` | yes |
| `UrlRule` | `(string? message)` | `Url()` | `invalid_string` | `Invalid URL format` | no |
| `UriRule` | `(UriKind uriKind, string? message)` | `Uri()`, `Uri(uriKind)` | `invalid_string` | `Invalid Uri, kind: {0}` | no |
| `PhoneRule` | `(string? message)` | `Phone()` | `invalid_string` | `Invalid phone number format` | yes |
| `E164Rule` | `(string? message)` | `E164()` | `invalid_string` | `Invalid E.164 phone number` | yes |
| `CreditCardRule` | `(string? message)` | `CreditCard()` | `invalid_string` | `Invalid credit card number format` | yes |
| `Base64StringRule` | `(string? message)` | `Base64String()` | `invalid_string` | `Invalid Base64 string format` | no |
| `Base64UrlRule` | `(string? message)` | `Base64Url()` | `invalid_string` | `Invalid base64url string` | yes |
| `UUIDRule` | `(string? message)` or `(UuidVersion version, string? message)` | `UUID()` / `UUID(version)` | `invalid_string` | `Invalid UUID format` (or `VersionedMessageFormat`, `Invalid UUID v{1} format`) | yes |
| `ULIDRule` | `(string? message)` | `ULID()` | `invalid_string` | `Invalid ULID` | yes |
| `JWTRule` | `(string? message)` | `JWT()` | `invalid_string` | `Invalid JWT` | yes |
| `IPAddressRule` | `(string? message, string? code)` or `(IPAddressRuleType ruleType, string? message, string? code)` | `IP()` / `IP(ruleType)` | `invalid_string` | `Invalid {1} address` | yes |
| `HexRule` | `(string? message)` | `Hex()` | `invalid_string` | `Invalid hexadecimal string` | yes |
| `DateStringRule` | `(string? message)` | `Date()` | `invalid_string` | `Invalid date` | yes |
| `TimeStringRule` | `(string? message)` | `Time()` | `invalid_string` | `Invalid time` | yes |
| `DatetimeStringRule` | `(string? message)` | `Datetime()` | `invalid_string` | `Invalid date-time` | yes |
| `NanoidRule` | `(string? message)` | `Nanoid()` | `invalid_string` | `Invalid nanoid` | yes |
| `Cuid2Rule` | `(string? message)` | `Cuid2()` | `invalid_string` | `Invalid CUID2` | yes |
| `StartsWithRule` | `(string prefix, StringComparison comparison = StringComparison.Ordinal, string? message, string? code)` | `StartsWith(prefix, comparison)` | `invalid_string` | `String must start with '{0}'` | yes |
| `EndsWithRule` | `(string suffix, StringComparison comparison = StringComparison.Ordinal, string? message, string? code)` | `EndsWith(suffix, comparison)` | `invalid_string` | `String must end with '{0}'` | yes |
| `IncludesRule` | `(string substring, string? message, string? code)` | `Includes(substring)` | `invalid_string` | `String must contain '{0}'` | yes |
| `EmojiRule` | `(string? message)` | `Emoji()` | `invalid_string` | `Invalid emoji` | yes |
| `XidRule` | `(string? message)` | `Xid()` | `invalid_string` | `Invalid XID` | yes |
| `KsuidRule` | `(string? message)` | `Ksuid()` | `invalid_string` | `Invalid KSUID` | yes |
| `DurationRule` | `(string? message)` | `Duration()` | `invalid_string` | `Invalid duration` | yes |
| `GuidRule` | `(string? message)` | `Guid()` | `invalid_string` | `Invalid GUID` | yes |
| `CidrRule` | `(CidrRuleType ruleType, string? message, string? code)` | `Cidr()` / `Cidr(ruleType)` | `invalid_string` | `Invalid {1} CIDR` | yes |

Every rule constructor also accepts an optional `string? code = null` after `message`; when set it overrides the reported error code (otherwise the rule's own `ErrorCode` is reported).

Behaviour notes:

- **`EmailRule`** matches a compiled, case-insensitive regex (`^[^@\s]+@[^@\s]+\.[^@\s]+$`).
- **`RegexRule`** uses the supplied `Regex`; the `Z.String().Regex(string)` overload compiles the pattern with a 100 ms timeout. Its message's `{0}` is the pattern, not the value.
- **`UrlRule`** accepts the compiled HTTP(S) regex **or** an absolute `http`/`https` URI (`Uri.TryCreate`), so it may allocate when the fast regex misses.
- **`UriRule`** validates with `Uri.TryCreate` against the supplied `UriKind` and therefore allocates. `Z.String().Uri()` defaults to `UriKind.RelativeOrAbsolute`; pass an explicit `UriKind` to require an absolute or relative URI.
- **`PhoneRule`** mirrors `[Phone]`: digits plus `() .+-`, with at least one digit.
- **`E164Rule`** requires `+`, a first digit `1`-`9`, and 7-15 digits in total.
- **`CreditCardRule`** mirrors `[CreditCard]`: Luhn check, ignoring spaces and hyphens.
- **`Base64StringRule`** mirrors `[Base64String]`: `Convert.FromBase64String` must succeed.
- **`Base64UrlRule`** accepts URL-safe base64 (`A-Z a-z 0-9 - _`) with no padding; a length of `4n+1` is rejected.
- **`UUIDRule`** accepts RFC 9562 versions 1-8 with a variant nibble of `8`-`9`/`a`-`b`, plus the nil and max UUIDs. The versioned constructor requires a specific version and rejects nil/max. It validates both a `string` and a `Guid` — a `Guid` is a UUID, so its bytes are inspected directly — which lets `[UUID(UuidVersion.Vx)]` constrain a `Guid` member or a `[Scalar<Guid>]` value object.
- **`ULIDRule`** requires 26 Crockford base32 characters, with the first character `0`-`7`.
- **`JWTRule`** requires three non-empty base64url-encoded segments separated by periods.
- **`IPAddressRule`** accepts IPv4 or IPv6 (`IPAddress.TryParse`); pass an `IPAddressRuleType` to require `IPv4`, `IPv6`, or `Any` (the default).
- **`HexRule`** accepts any run of ASCII hex digits; the empty string is valid, matching Zod.
- **`DateStringRule`** parses `yyyy-MM-dd` (invariant culture); **`TimeStringRule`** accepts `HH:mm`, optionally `:ss` and fractional seconds; **`DatetimeStringRule`** requires `yyyy-MM-dd` + `T` + `HH:mm:ss[.fff]` + `Z`.
- **`NanoidRule`** requires exactly 21 URL-safe characters; **`Cuid2Rule`** requires non-empty lowercase alphanumerics.
- **`StartsWithRule`** and **`EndsWithRule`** use the supplied `StringComparison` (default `Ordinal`); **`IncludesRule`** uses ordinal comparison.
- **`EmojiRule`** mirrors Zod's `emoji()`: every rune must be an `Extended_Pictographic` or `Emoji_Component` code point, and at least one must be an anchor (a pictograph, a regional indicator, or a keycap). The Unicode tables are generated from Unicode 16.0 `emoji-data.txt`, because .NET's regex engine does not expose the `Extended_Pictographic` property.
- **`XidRule`** requires exactly 20 base32hex characters (`0-9`, `a-v`/`A-V`); **`KsuidRule`** requires exactly 27 base62 characters.
- **`DurationRule`** accepts ISO 8601-1 durations; the 8601-2 extensions (negative durations and fractional/negative components) are rejected, matching Zod.
- **`GuidRule`** accepts any 8-4-4-4-12 hexadecimal identifier, including nil and max; `UUIDRule` additionally checks the RFC 9562 version and variant nibbles. A C# `Guid` is exported to JSON Schema as a canonical hexadecimal `pattern` (not `format: uuid`), because `Guid` accepts any 128-bit value; a `Guid` that must be an RFC version opts in with `[UUID(UuidVersion.Vx)]`.
- **`CidrRule`** accepts an IPv4 or IPv6 block; pass a `CidrRuleType` to require `IPv4` or `IPv6` (`Any` is the default).

## Number rules

Fluent methods live on `ZodNumber` (`ZodSharp.Schemas`), which validates `double`.

| Rule | Constructor | Fluent method | Code | Message format |
|---|---|---|---|---|
| `MinValueRule<T>` | `(T minValue)` | `Min(v)`, `NonNegative()` | `too_small` | `Value must be at least {0}, but got {1}` |
| `MaxValueRule<T>` | `(T maxValue)` | `Max(v)`, `NonPositive()` | `too_big` | `Value must be at most {0}, but got {1}` |
| `GreaterThanRule<T>` | `(T exclusiveMinimum)` | `Gt(v)`, `Positive()` | `too_small` | `Value must be greater than {0}, but got {1}` |
| `LessThanRule<T>` | `(T exclusiveMaximum)` | `Lt(v)`, `Negative()` | `too_big` | `Value must be less than {0}, but got {1}` |
| `GreaterThanOrEqualRule<T>` | `(T minValue, string? message)` | `Gte(v)` | `too_small` | `Value must be greater than or equal to {0}, but got {1}` |
| `LessThanOrEqualRule<T>` | `(T maxValue, string? message)` | `Lte(v)` | `too_big` | `Value must be less than or equal to {0}, but got {1}` |
| `EvenRule<T>` | `(string? message)` | `Even()` | `invalid_value` | `Number must be even, but got {0}` |
| `OddRule<T>` | `(string? message)` | `Odd()` | `invalid_value` | `Number must be odd, but got {0}` |
| `MultipleOfRule<T>` | `(T divisor, string? message)` | `MultipleOf(divisor)` | `not_multiple_of` | `Number must be a multiple of {0}, but got {1}` |
| `FiniteRule<T>` | `(string? message)` | `Finite()` | `not_finite` | `Number must be finite, but got {0}` |
| `SafeIntegerRule` | `(string? message)` | `Safe()` | `too_big` | `Number must be a safe integer, but got {0}` |
| `IntRule<T>` | `()` | `Int()` | `invalid_type` | `Expected integer, but got {0}` |

Behaviour notes:

- All six bound rules (`MinValueRule<T>`, `MaxValueRule<T>`, `GreaterThanRule<T>`, `LessThanRule<T>`, `GreaterThanOrEqualRule<T>`, `LessThanOrEqualRule<T>`) are generic over `T : IComparable<T>`, so they can be reused with any comparable type (the fluent methods close them with `double`). `ZodBigInt` and `ZodDate` close the generic rules with `long` and `DateTime`.
- The arithmetic rules (`IntRule<T>`, `FiniteRule<T>`, `MultipleOfRule<T>`, `EvenRule<T>`, `OddRule<T>`) are generic over `T : INumber<T>`, so they close with `int`, `long`, `double`, `decimal`, and every other numeric type; the fluent methods close them with `double`.
- `Positive()` is `GreaterThanRule<double>(0.0)`, `Negative()` is `LessThanRule<double>(0.0)`, `NonNegative()` is `MinValueRule<double>(0.0)`, and `NonPositive()` is `MaxValueRule<double>(0.0)`.
- **`EvenRule<T>`** and **`OddRule<T>`** treat a value as even when `value % 2 == 0`.
- **`MultipleOfRule<T>`** throws `ArgumentException` when the divisor is `0`, and compares the distance to the nearest multiple against a relative tolerance of `1e-12` (so `0.3` is accepted for a divisor of `0.1`). Integer types cannot represent the tolerance, so their comparison is exact.
- **`FiniteRule<T>`** rejects `NaN` and infinities via `T.IsFinite`.
- **`SafeIntegerRule`** is intentionally `double`-only ("safe integer" is a JavaScript `Number` concept); it requires a whole number within `int.MinValue`..`int.MaxValue`.
- **`IntRule<T>`** requires `value % T.One == T.Zero`, which also rejects infinities (a truncate comparison would accept them).

## Value rules

| Rule | Constructor | Fluent method | Code | Message format |
|---|---|---|---|---|
| `NonSentinelRule<T>` | `(string? message)` | `NonSentinel()` | `invalid_value` | `Value has a sentinel value, but got {0}` |
| `NullOrNonWhiteSpaceRule` | `(string? message, string? code)` | — | `invalid_string` | `Value must be null or non-whitespace` |

`NonSentinelRule<T>` rejects the framework default/boundary values an ORM commonly stores to mean "no value": `Guid.Empty`; `DateTime`, `DateTimeOffset`, `DateOnly`, and `TimeOnly` `MinValue`/`MaxValue`; and `null`/empty/whitespace strings. Types without a known sentinel always pass, so the rule never rejects a type it does not understand. It detects sentinels with a `typeof(T)` dispatch and reinterprets the value in place, so no boxing occurs. The `{0}` argument is a **description** of the sentinel (`an empty GUID`, `DateTime.MinValue`, `an empty string`, …) rather than the raw value, and a type-level `[NonSentinel]` on a `[Scalar]` type replaces the message's `Value` subject with the scalar's name — see [Value Objects Integration](Value-Objects-Integration.md#message-overrides). Its JSON Schema mapping is exact for `DateOnly`, `TimeOnly`, the deterministic `DateTime` wire forms, and `DateTimeOffset` (the default pattern is offset-aware); the optional UTC contract narrows the accepted offset — see [JSON Schema Export](JsonSchema-Export.md#temporal-sentinels-and-the-datetimeoffset-wire-form).

`NullOrNonWhiteSpaceRule` is the null-tolerant counterpart: it accepts `null` but rejects an empty or whitespace-only string. It exists for nullable scalar value objects, which round-trip JSON `null`, so their schema must accept `null` while still rejecting whitespace — `[RequiredZod]` always rejects `null` and so cannot express it. Apply it to a nullable scalar type with `[NullOrNonWhiteSpace]`; a rule written against the underlying `string?` value is adapted to the value object automatically.

The fluent `NonSentinel()` method is declared on `ZodType<TOutput, TInput>`, so it is available on every schema and closes the rule with the schema's output type. `ZodString`, `ZodDate`, and `ZodDateTimeOffset` override it with a covariant return type so the fluent chain keeps the concrete schema:

```csharp
var schema = Z.Date().NonSentinel();
var result = schema.Validate(DateTime.MinValue);
// result.Errors[0].Code == NonSentinelRule<DateTime>.ErrorCode   ("invalid_value")

var offsetSchema = Z.DateTimeOffset().NonSentinel();
var offsetResult = offsetSchema.Validate(DateTimeOffset.MinValue);

// Covariant overrides keep the concrete schema, so later fluent calls still compile.
var chained = Z.String().NonSentinel().Min(3);
```

The same rule can be attached directly with `AddRule`/`Rule`, or through the shipped `[NonSentinel]` attribute, which closes the open generic with the annotated member/scalar type — see [Custom Rules](Custom-Rules.md#non-sentinel-values-ef-friendly).

## DataAnnotations-mirroring rules

These rules mirror `System.ComponentModel.DataAnnotations` attributes as first-class rules, so the same behaviour is available standalone, through the fluent API, or through a generated attribute:

| Rule | Constructor | Fluent method | Code | Message format |
|---|---|---|---|---|
| `RequiredRule<T>` | `(bool allowEmptyStrings = false, bool trimWhitespace = false, string? message, string? code)` | `ZodString.Required(...)` | `missing_field` | `{0} is required` (`{0}` is the display name, `Value` when unknown) |
| `RangeRule<T>` | `(T minimum, T maximum, bool minimumIsExclusive = false, bool maximumIsExclusive = false, string? message, string? code)` | `ZodNumber.Range(...)`, `ZodBigInt.Range(...)`, `ZodDate.Range(...)` | `invalid_range` | `Value must be between {0} and {1}, but got {2}` |
| `LengthRule` | `(int minimum, int maximum, string? message, string? code)` | `ZodString.Length(min, max)` | `invalid_length` | `String length must be between {0} and {1} characters, but got {2}` |
| `StringLengthRule` | `(int maximumLength, int minimumLength = 0, string? message, string? code)` | `ZodString.StringLength(max, min)` | `invalid_length` | `String must be at most {0} characters long and at least {1}, but got {2}` |
| `CompareRule` | `(string other, StringComparison comparison = Ordinal, string? message, string? code)` | `ZodString.Compare(...)` | `mismatch` | `Value does not match the expected value` |
| `AllowedValuesRule<T>` | `(T[] values, string? message, string? code)` | `AllowedValues(values)` | `invalid_value` | `Value is not one of the allowed values` |
| `DeniedValuesRule<T>` | `(T[] values, string? message, string? code)` | `DeniedValues(values)` | `invalid_value` | `Value is one of the denied values` |

`RequiredRule<T>` always rejects `null`. Its default message is `"Value is required"` and it reports the shared `"{displayName} is required"` template (`ZodMessages.Required`) as `MessageFormat`, so a host that knows the member can re-render it with the display name. When `allowEmptyStrings` is `false` an empty string is rejected as well; `trimWhitespace` additionally treats a whitespace-only string as empty (leading/trailing whitespace on a non-empty value is preserved). The `ZodString.Required` fluent method is a covariant convenience over the generic rule.

`AllowedValuesRule<T>` and `DeniedValuesRule<T>` accept both a single value (`new AllowedValuesRule<int>(5)`) and an array. The generated `[AllowedValuesZod]` / `[DeniedValuesZod]` attributes surface the array as a `params object[]` (plus a single-value overload), so `[AllowedValuesZod("a", "b", "c")]` works on a `string` member and `[DeniedValuesZod(1, 2, 3)]` on an `int` member; the resolver converts each element to the member type.

## Enum rules

| Rule | Constructor | Fluent method | Code | Message format |
|---|---|---|---|---|
| `EnumRule<TEnum>` | `()` or `(TEnum[]? disallowed, string? message, string? code)` | `Enum()` | `invalid_enum_value` | `'{0}' is not a defined member of {1}` |

`EnumRule<TEnum>` is generic over `TEnum : struct, Enum` and rejects a value that is not a defined member of the enum type, or that resolves to a member the rule excludes. It is equivalent to Zod's `z.nativeEnum(Enum)` semantics. The defined members are resolved once per closed generic type, so the validation path performs no reflection.

The `[ZodSchema]` generator emits this rule automatically for every non-flags enum property — see [Source Generator](Source-Generator.md#automatic-enum-validation). The rule is closed with the property's enum type and receives the excluded members as its disallowed set:

- every enum member marked `[ZodIgnore]` (a member that is defined but never a valid value, such as `Unspecified`), and
- the values the property's `[DeniedValues]` attribute lists.

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
    public ExampleEnum Status { get; set; }

    [DeniedValues(ExampleEnum.AnotherValidValue)]
    public ExampleEnum SecondaryStatus { get; set; }
}
```

The rule is skipped for a `[Flags]` enum (a combination is valid without being a defined member), for a property that declares an explicit `[AllowedValues]` allow-list (the allow-list governs), and when the schema opts out with `[ZodSchema(ValidateEnumValues = false)]`. A hand-written `ZodNativeEnum<TEnum>` schema already rejects undefined members; its `Enum()` method adds this rule to customise the message/code, and the rule can be attached to any enum-output schema with `AddRule`/`Rule`.

## Generated attributes

Each built-in rule that can be expressed as an attribute ships a generated `ValidationAttribute` in the `ZodSharp.Rules` namespace (inside the `Purview.ZodSharp` assembly), so a member or a scalar can be annotated directly:

| Attribute | Rule | Notes |
|---|---|---|
| `[Email]` | `EmailRule` | |
| `[E164]` | `E164Rule` | |
| `[Regex("…")]` | `RegexRule` | mirrors the `(string pattern, string? message)` overload |
| `[UUID]` / `[UUID(UuidVersion.…)]` | `UUIDRule` | the version is optional; omit it for the versionless rule |
| `[ULID]` | `ULIDRule` | |
| `[JWT]` | `JWTRule` | |
| `[IPAddress]` / `[IPAddress(IPAddressRuleType.…)]` | `IPAddressRule` | defaults to `Any`; pass a type to require IPv4/IPv6 |
| `[Hex]` | `HexRule` | |
| `[Base64Url]` | `Base64UrlRule` | |
| `[Nanoid]` | `NanoidRule` | |
| `[Cuid2]` | `Cuid2Rule` | |
| `[DateString]` | `DateStringRule` | |
| `[TimeString]` | `TimeStringRule` | |
| `[DatetimeString]` | `DatetimeStringRule` | |
| `[StartsWith("…")]` | `StartsWithRule` | |
| `[EndsWith("…")]` | `EndsWithRule` | |
| `[Includes("…")]` | `IncludesRule` | |
| `[MultipleOf(…)]` | `MultipleOfRule<T>` | closes the open generic with the member type |
| `[Finite]` | `FiniteRule<T>` | closes the open generic with the member type |
| `[SafeInteger]` | `SafeIntegerRule` | |
| `[Int]` | `IntRule<T>` | closes the open generic with the member type |
| `[Uri(UriKind.…)]` | `UriRule` | the `UriKind` is required |
| `[NonSentinel]` | `NonSentinelRule<T>` | closes the open generic with the member/scalar type |
| `[NullOrNonWhiteSpace]` | `NullOrNonWhiteSpaceRule` | accepts `null` but rejects empty/whitespace-only strings (for nullable scalar value objects) |
| `[Enum]` | `EnumRule<TEnum>` | closes the open generic with the enum member type; excludes only the members the rule receives |
| `[MinValue(…)]` | `MinValueRule<T>` | the type-parameter bound is a `double`; the rule closes with the member type |
| `[MaxValue(…)]` | `MaxValueRule<T>` | as above |
| `[GreaterThan(…)]` | `GreaterThanRule<T>` | as above |
| `[LessThan(…)]` | `LessThanRule<T>` | as above |
| `[GreaterThanOrEqual(…)]` | `GreaterThanOrEqualRule<T>` | as above |
| `[LessThanOrEqual(…)]` | `LessThanOrEqualRule<T>` | as above |
| `[Even]` | `EvenRule<T>` | closes the open generic with the member type |
| `[Odd]` | `OddRule<T>` | closes the open generic with the member type |
| `[MinCount(…)]` | `MinCountRule` | collection/dictionary minimum element count; the trim-safe `[MinLength]` on a collection, with the same error identity |
| `[MaxCount(…)]` | `MaxCountRule` | collection/dictionary maximum element count; the trim-safe `[MaxLength]` on a collection |
| `[CountRange(…, …)]` | `CountRangeRule` | inclusive element-count range; the trim-safe `[Length]` on a collection |
| `[MinLengthZod(…)]` | `MinLengthRule` | suffixed to avoid the DataAnnotations name; exposes `Code`/`Message` |
| `[MaxLengthZod(…)]` | `MaxLengthRule` | as above |
| `[UrlZod]` | `UrlRule` | as above |
| `[PhoneZod]` | `PhoneRule` | as above |
| `[CreditCardZod]` | `CreditCardRule` | as above |
| `[Base64StringZod]` | `Base64StringRule` | as above |
| `[Emoji]` | `EmojiRule` | |
| `[Xid]` | `XidRule` | |
| `[Ksuid]` | `KsuidRule` | |
| `[Duration]` | `DurationRule` | |
| `[Guid]` | `GuidRule` | accepts any 8-4-4-4-12 hex identifier |
| `[Cidr]` / `[Cidr(CidrRuleType.…)]` | `CidrRule` | defaults to `Any`; pass a type to require IPv4/IPv6 |
| `[RequiredZod(…)]` | `RequiredRule<T>` | suffixed; exposes `AllowEmptyStrings`/`TrimWhitespace` and rejects an absent value exactly like `[Required]` |
| `[RangeZod(…)]` | `RangeRule<T>` | the type-parameter bounds are doubles; the rule closes with the member type |
| `[LengthZod(…)]` | `LengthRule` | suffixed to avoid the DataAnnotations name |
| `[StringLengthZod(…)]` | `StringLengthRule` | as above |
| `[CompareZod(…)]` | `CompareRule` | as above |
| `[AllowedValuesZod(…)]` | `AllowedValuesRule<T>` | a `params object[]` array (and a single-value overload); the rule closes with the member type |
| `[DeniedValuesZod(…)]` | `DeniedValuesRule<T>` | a `params object[]` array (and a single-value overload); the rule closes with the member type |

A generated attribute's constructor mirrors the rule's value parameters: a parameter the rule declares without a default becomes a required constructor argument, and a parameter with a default keeps that default (so an optional value such as `[StartsWith("…", StringComparison.OrdinalIgnoreCase)]` still has a usable default). The value parameters are also exposed as settable properties. The rule's `message` and identity (`code`/`origin`) parameters stay properties only — `Message` and, where present, `Code` — and default to the rule's own `MessageFormat`/`ErrorCode`, so the attribute is self-describing.

Each generated attribute reports the rule's own `ErrorCode`/`Origin` because every built-in rule implements `IZodRule`.

Rules whose derived attribute name is already taken by `System.ComponentModel.DataAnnotations` — `MinLengthRule`, `MaxLengthRule`, `UrlRule`, `PhoneRule`, `CreditCardRule`, `Base64StringRule`, `RequiredRule`, `RangeRule`, `LengthRule`, `StringLengthRule`, `CompareRule`, `AllowedValuesRule`, `DeniedValuesRule` — are emitted under a `Zod` suffix instead (for example `[MinLengthZod]`, `[RequiredZod]`, `[RangeZod]`), so the rule's own identity and message stay usable. The generic bound rules surface their type-parameter bound as a `double`, so `[MinValue(3)]` works on an `int` or a `double` member (the value is converted to the member type).

## Exposed as DataAnnotations attributes

The `[ZodSchema]` generator maps several built-in rules to their `System.ComponentModel.DataAnnotations` attributes, and any custom rule can be mapped with `[ZodRule(typeof(...))]`:

| Attribute | Rule | Failure code |
|---|---|---|
| `[EmailAddress]` | `EmailRule` | `invalid_string` |
| `[Url]` | `UrlRule` | `invalid_string` |
| `[Phone]` | `PhoneRule` | `invalid_string` |
| `[CreditCard]` | `CreditCardRule` | `invalid_string` |
| `[Base64String]` | `Base64StringRule` | `invalid_string` |
| `[RegularExpression]` | compiled `Regex`, not `RegexRule` | `invalid_string` |

Size and range attributes (`[Length]`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[Range]`) are emitted as direct, typed codegen; the equivalent rule structs (`LengthRule`, `StringLengthRule`, `MinLengthRule`, `MaxLengthRule`, `RangeRule<T>`) are also available directly and through their generated attributes. The rule-identity variants of the colliding names are the suffixed [generated attributes](#generated-attributes) (`[MinLengthZod]`, `[MaxLengthZod]`, `[UrlZod]`, `[PhoneZod]`, `[CreditCardZod]`, `[Base64StringZod]`, `[RequiredZod]`, `[RangeZod]`, `[LengthZod]`, `[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`, `[DeniedValuesZod]`), which report the rule's own `Code` and accept a `Message`. The remaining built-in rules are covered by their own generated attributes — for example `[Regex("…")]` validates through `RegexRule`, whereas `[RegularExpression]` compiles the pattern inline. See [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) for the full attribute table and the generated metadata, and [Custom Rules](Custom-Rules.md) for mapping your own rules to attributes.

## Related

- [Custom Rules](Custom-Rules.md) — the `IValidationRule<T>` contract, `IZodRule`, and attribute mapping.
- [String Validation](String-Validation.md) — the `ZodString` fluent surface and span validation.
- [Number Validation](Number-Validation.md) — the `ZodNumber` fluent surface.
- [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) — attributes the generator understands.
- [Source Generator Diagnostics](Source-Generator-Diagnostics.md) — `ZODSGEN042` and the rule conventions analyzer.

# String Validation

`ZodString` (namespace `ZodSharp.Schemas`) validates `string` values. `ParseInternal` rejects `null` with an `invalid_type` error (`"Expected string, received null"`); on success the accumulated rules run.

```csharp
using ZodSharp;

var schema = Z.String().Min(3).Max(50).Email();
var result = schema.Validate("user@example.com");
```

## Methods

| Method | Signature | Rule added |
|---|---|---|
| `Min` | `Min(int minLength)` | `MinLengthRule` — `too_small` when too short |
| `Max` | `Max(int maxLength)` | `MaxLengthRule` |
| `Length` | `Length(int length)` | exact length (both bounds) |
| `Email` | `Email()` | `EmailRule` — static compiled regex |
| `Regex` | `Regex(Regex pattern, string? message)` / `Regex(string pattern, string? message)` | `RegexRule`; the string overload compiles with a 100 ms timeout |
| `Url` | `Url(string? message)` | `UrlRule` — regex or absolute `http`/`https` URI |
| `Uri` | `Uri(string? message)` / `Uri(UriKind uriKind, string? message)` | `UriRule` — `Uri.TryCreate` against the supplied `UriKind` (defaults to `RelativeOrAbsolute`) |
| `Phone` | `Phone(string? message)` | `PhoneRule` — digits plus `() .+-`, at least one digit |
| `CreditCard` | `CreditCard(string? message)` | `CreditCardRule` — Luhn algorithm |
| `Base64String` | `Base64String(string? message)` | `Base64StringRule` — `Convert.FromBase64String` |
| `UUID` | `UUID(string? message)` | `UUIDRule` — char-scan, RFC 9562 versions 1-8, variant nibble `8-9/a-b`, plus nil and max |
| `UUID` | `UUID(UuidVersion version, string? message)` | `UUIDRule` — requires a specific version (e.g. `V7`), variant nibble `8-9/a-b`, nil/max rejected |
| `StartsWith` | `StartsWith(string prefix, StringComparison comparison = StringComparison.Ordinal, string? message, string? code)` | `StartsWithRule` — comparison defaults to `Ordinal` |
| `EndsWith` | `EndsWith(string suffix, StringComparison comparison = StringComparison.Ordinal, string? message, string? code)` | `EndsWithRule` — comparison defaults to `Ordinal` |
| `Includes` | `Includes(string substring, string? message, string? code)` | `IncludesRule` — ordinal substring containment |
| `IP` | `IP(string? message, string? code)` / `IP(IPAddressRuleType ruleType, string? message, string? code)` | `IPAddressRule` — IPv4/IPv6, or only the requested family (`IPv4`/`IPv6`/`Any`; defaults to `Any`) |
| `JWT` | `JWT(string? message)` | `JWTRule` — three base64url-encoded segments |
| `Hex` | `Hex(string? message)` | `HexRule` — hexadecimal characters (empty string is valid, matching Zod) |
| `Base64Url` | `Base64Url(string? message)` | `Base64UrlRule` — URL-safe base64, no padding (groups of 4 plus a 2-3 character tail) |
| `ULID` | `ULID(string? message)` | `ULIDRule` — 26 Crockford base32 characters, first character `0`-`7` |
| `Datetime` | `Datetime(string? message)` | `DatetimeStringRule` — ISO 8601 date-time (`yyyy-MM-ddTHH:mm:ss[.fff]Z`) |
| `Date` | `Date(string? message)` | `DateStringRule` — ISO 8601 date (`yyyy-MM-dd`) |
| `Time` | `Time(string? message)` | `TimeStringRule` — ISO 8601 time (`HH:mm`, optionally `:ss` and fractional seconds) |
| `Nanoid` | `Nanoid(string? message)` | `NanoidRule` — 21 URL-safe characters |
| `Cuid2` | `Cuid2(string? message)` | `Cuid2Rule` — lowercase alphanumeric characters |
| `E164` | `E164(string? message)` | `E164Rule` — `+` followed by 7-15 digits |
| `NonSentinel` | `NonSentinel(string? message)` | `NonSentinelRule<string>` — rejects `null`/empty/whitespace; inherited from `ZodType<T>` and overridden to keep `ZodString` in the chain |
| `ToLower` | `ToLower()` | wraps a transform (`ToLowerInvariant`), returns a `ZodString` |
| `ToUpper` | `ToUpper()` | wraps a transform (`ToUpperInvariant`) |
| `Trim` | `Trim()` | wraps a transform (`Trim`) |
| `ValidateSpan` | `ValidateSpan(ReadOnlySpan<char> value)` | validates the span directly; a successful result materialises the value string |
| `IsValidSpan` | `IsValidSpan(ReadOnlySpan<char> value, out ImmutableArray<ValidationError> errors)` | allocation-free on success; materialises the input only when a rule or transform has no span path |

> [!NOTE]
> `ToLower`, `ToUpper`, and `Trim` produce a new string on every validation. `IsValidSpan` does not allocate when the value is valid; `ValidateSpan` allocates once because its result carries a `string`.

Every method that adds a rule also accepts optional `string? message` and `string? code` parameters: `message` overrides the rule's default message, and `code` overrides the reported error code (otherwise the rule's own `ErrorCode` is reported). The rule's default message format is used when `message` is omitted.

## Examples

```csharp
var email = Z.String().Email().Validate("user@example.com");

var url = Z.String().Url().Validate("https://example.com");

var uuid = Z.String().UUID().Validate("550e8400-e29b-41d4-a716-446655440000");

var uuidV7 = Z.String().UUID(UuidVersion.V7).Validate("0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");

var prefix = Z.String().StartsWith("https://");
var suffix = Z.String().EndsWith(".com");

var exact = Z.String().Length(10);

var normalized = Z.String().Trim().ToUpper().Validate("  hello  "); // "HELLO"

ReadOnlySpan<char> span = "user@example.com".AsSpan();
var spanResult = Z.String().Min(3).Max(50).Email().ValidateSpan(span);
```

## Error messages

Rules produce `ValidationError` entries with an empty path. Size validations emit `too_small` or `too_big` (origin `string`, typed bounds, the length in `count`); string-format validations emit `invalid_string` with category `invalid_format` and a `format` parameter (`email`, `uuid`, `regex`, `starts_with`, …). Default messages never quote the input. Many methods accept a custom `message` and `code` parameter. Rule structs live in `ZodSharp.Rules` and can be reused standalone with `IValidationRule<T>`. For the full catalogue — including rules without a fluent method — see [Validation Rules Reference](Validation-Rules-Reference.md).

Every rule exposes its error identity as public constants — `public const string ErrorCode` and a `public const string MessageFormat` (a `{0}`-style template) — so tests and consumers can assert against the rule instead of duplicating literals:

```csharp
var result = Z.String().Email().Validate("not-an-email");
// result.Errors[0].Code    == EmailRule.ErrorCode        ("invalid_string")
// result.Errors[0].Message == EmailRule.MessageFormat     ("Invalid email format")
// result.Errors[0].Category == ZodIssueCategories.InvalidFormat
```

## Span validation

`ValidateSpan(ReadOnlySpan<char> value)` validates the span directly using the rules' `IStringValidationRule` implementations; it materialises a `string` only for the returned value (and only falls back to the string pipeline for schemas with transforms or rules without a span implementation). `IsValidSpan(ReadOnlySpan<char> value, out ImmutableArray<ValidationError> errors)` is the allocation-free entry point when the value is not needed. An empty span is validated by the rules like an empty string.

## Custom rules

Custom rules implement `IValidationRule<string>` and can be attached with `Z.String().Rule(new MyRule())`. Implement `IStringValidationRule` as well to keep them on the span path. They can also be exposed as DataAnnotations-style attributes; see [Custom Rules](Custom-Rules.md).
# String Validation

`ZodString` (namespace `ZodSharp.Schemas`) validates `string` values. `ParseInternal` rejects `null` with an `invalid_type` error (`"Expected string, but got null"`); on success the accumulated rules run.

```csharp
using ZodSharp;

var schema = Z.String().Min(3).Max(50).Email();
var result = schema.Validate("user@example.com");
```

## Methods

| Method | Signature | Rule added |
|---|---|---|
| `Min` | `Min(int minLength)` | `MinLengthRule` — `too_small` via `validation_failed` when too short |
| `Max` | `Max(int maxLength)` | `MaxLengthRule` |
| `Length` | `Length(int length)` | exact length (both bounds) |
| `Email` | `Email()` | `EmailRule` — static compiled regex |
| `Regex` | `Regex(Regex pattern, string? message)` / `Regex(string pattern, string? message)` | `RegexRule`; the string overload compiles with a 100 ms timeout |
| `Url` | `Url(string? message)` | `UrlRule` — regex or absolute `http`/`https` URI |
| `Phone` | `Phone(string? message)` | `PhoneRule` — digits plus `() .+-`, at least one digit |
| `CreditCard` | `CreditCard(string? message)` | `CreditCardRule` — Luhn algorithm |
| `Base64String` | `Base64String(string? message)` | `Base64StringRule` — `Convert.FromBase64String` |
| `UUID` | `UUID(string? message)` | `UUIDRule` — char-scan, RFC 9562 versions 1-8, variant nibble `8-9/a-b`, plus nil and max |
| `UUID` | `UUID(UuidVersion version, string? message)` | `UUIDRule` — requires a specific version (e.g. `V7`), variant nibble `8-9/a-b`, nil/max rejected |
| `StartsWith` | `StartsWith(string prefix, string? message)` | `StartsWithRule` — ordinal comparison |
| `EndsWith` | `EndsWith(string suffix, string? message)` | `EndsWithRule` — ordinal comparison |
| `ToLower` | `ToLower()` | wraps a transform (`ToLowerInvariant`), returns a `ZodString` |
| `ToUpper` | `ToUpper()` | wraps a transform (`ToUpperInvariant`) |
| `Trim` | `Trim()` | wraps a transform (`Trim`) |
| `ValidateSpan` | `ValidateSpan(ReadOnlySpan<char> value)` | validates the span directly; a successful result materialises the value string |
| `IsValidSpan` | `IsValidSpan(ReadOnlySpan<char> value, out ImmutableArray<ValidationError> errors)` | allocation-free on success; materialises the input only when a rule or transform has no span path |

> [!NOTE]
> `ToLower`, `ToUpper`, and `Trim` produce a new string on every validation. `IsValidSpan` does not allocate when the value is valid; `ValidateSpan` allocates once because its result carries a `string`.

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

Rules produce `ValidationError` entries with code `validation_failed` and an empty path. Many methods accept a custom `message` parameter. Rule structs live in `ZodSharp.Rules` and can be reused standalone with `IValidationRule<T>`.

## Span validation

`ValidateSpan(ReadOnlySpan<char> value)` validates the span directly using the rules' `IStringValidationRule` implementations; it materialises a `string` only for the returned value (and only falls back to the string pipeline for schemas with transforms or rules without a span implementation). `IsValidSpan(ReadOnlySpan<char> value, out ImmutableArray<ValidationError> errors)` is the allocation-free entry point when the value is not needed. An empty span is validated by the rules like an empty string.

## Custom rules

Custom rules implement `IValidationRule<string>` and can be attached with `Z.String().Rule(new MyRule())`. Implement `IStringValidationRule` as well to keep them on the span path. They can also be exposed as DataAnnotations-style attributes; see [Custom Rules](Custom-Rules.md).
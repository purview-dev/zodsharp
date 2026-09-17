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
| `ValidateSpan` | `ValidateSpan(ReadOnlySpan<char> value)` | zero-allocation span validation |

> [!NOTE]
> `ToLower`, `ToUpper`, and `Trim` produce a new string on every validation — these are the only string validations that allocate on a successful path.

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

`ValidateSpan(ReadOnlySpan<char> value)` avoids string allocations on the validation path. An empty span validates successfully as `""`.
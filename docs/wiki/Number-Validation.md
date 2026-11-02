# Number Validation

`ZodNumber` (namespace `ZodSharp.Schemas`) validates `double` values. `ParseInternal` rejects `double.NaN` with an `invalid_type` error (`"Expected number, received nan"`); on success the accumulated rules run.

```csharp
using ZodSharp;

var schema = Z.Number().Min(0).Max(120).Int();
var result = schema.Validate(30.0);
```

## Methods

| Method | Signature | Rule added |
|---|---|---|
| `Min` | `Min(double minValue)` | `MinValueRule<double>` — `Value must be at least ...` |
| `Max` | `Max(double maxValue)` | `MaxValueRule<double>` |
| `Gt` | `Gt(double value)` | `GreaterThanRule<double>` — strictly greater than `value` |
| `Gte` | `Gte(double value)` | `GreaterThanOrEqualRule<double>` — greater than or equal to `value` |
| `Lt` | `Lt(double value)` | `LessThanRule<double>` — strictly less than `value` |
| `Lte` | `Lte(double value)` | `LessThanOrEqualRule<double>` — less than or equal to `value` |
| `Int` | `Int()` | `IntRule<double>` — `value % 1 == 0`; failure code `invalid_type` |
| `Positive` | `Positive()` | `GreaterThanRule<double>(0.0)` — strictly greater than zero |
| `Negative` | `Negative()` | `LessThanRule<double>(0.0)` — strictly less than zero |
| `NonNegative` | `NonNegative()` | `MinValueRule<double>(0.0)` — greater than or equal to zero |
| `NonPositive` | `NonPositive()` | `MaxValueRule<double>(0.0)` — less than or equal to zero |
| `MultipleOf` | `MultipleOf(double divisor, string? message)` | `MultipleOfRule<double>` — throws `ArgumentException` for a zero divisor; relative-tolerance comparison (`1e-12`) |
| `Finite` | `Finite(string? message)` | `FiniteRule<double>` — `T.IsFinite` |
| `Safe` | `Safe(string? message)` | `SafeIntegerRule` — integer within `int.MinValue`..`int.MaxValue`; failure code `too_big` |
| `Even` | `Even(string? message)` | `EvenRule<double>` — `value % 2 == 0`; failure code `invalid_value` |
| `Odd` | `Odd(string? message)` | `OddRule<double>` — `value % 2 != 0`; failure code `invalid_value` |

All six bound rules (`MinValueRule<T>`, `MaxValueRule<T>`, `GreaterThanRule<T>`, `LessThanRule<T>`, `GreaterThanOrEqualRule<T>`, `LessThanOrEqualRule<T>`) are generic over `T : IComparable<T>`, and the arithmetic rules (`IntRule<T>`, `FiniteRule<T>`, `MultipleOfRule<T>`, `EvenRule<T>`, `OddRule<T>`) are generic over `T : INumber<T>`, so they close with any numeric type (`int`, `long`, `decimal`, …) — not just `double`. `ZodNumber` closes them with `double`; `ZodBigInt` and `ZodDate` close the bound rules with `long` and `DateTime`. `SafeIntegerRule` is intentionally `double`-only, because "safe integer" is a JavaScript `Number` concept.

Every numeric method also accepts optional `string? message` and `string? code` parameters: `message` overrides the rule's default message, and `code` overrides the reported error code (otherwise the rule's own `ErrorCode` is reported).

## Examples

```csharp
var positive = Z.Number().Positive();       // > 0
var negative = Z.Number().Negative();       // < 0
var nonNegative = Z.Number().NonNegative(); // >= 0
var nonPositive = Z.Number().NonPositive(); // <= 0

var multipleOf = Z.Number().MultipleOf(10); // multiples of 10
var fractional = Z.Number().MultipleOf(0.1); // 0.3 is accepted (floating-point tolerance)
var finite = Z.Number().Finite();           // rejects Infinity / NaN
var safe = Z.Number().Safe();               // safe integer range
var whole = Z.Number().Int();               // no fractional part
var even = Z.Number().Even();               // 0, 2, 4, ...
var odd = Z.Number().Odd();                 // 1, 3, 5, ...

var age = Z.Number().Min(0).Max(120).Int().Validate(25.0);
var atLeastTen = Z.Number().Gte(10);        // >= 10
var atMostTen = Z.Number().Lte(10);         // <= 10
```

## Numeric coercion

When a `Z.Number()` is used as an object field or union option, boxed values are coerced via `IConvertible` (invariant culture) — for example a `long` from a `Dictionary<string, object?>` validates against a `Z.Number()` field. Non-numeric values fail with `invalid_type`.

## See also

- [Validation Rules Reference](Validation-Rules-Reference.md) — every built-in rule with its error code and message format.
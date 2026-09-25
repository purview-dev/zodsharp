# Number Validation

`ZodNumber` (namespace `ZodSharp.Schemas`) validates `double` values. `ParseInternal` rejects `double.NaN` with an `invalid_type` error (`"Expected number, but got NaN"`); on success the accumulated rules run.

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
| `Int` | `Int()` | `IntRule` — `value == Math.Truncate(value)` |
| `Positive` | `Positive()` | `GreaterThanRule<double>(0.0)` — strictly greater than zero |
| `Negative` | `Negative()` | `LessThanRule<double>(0.0)` — strictly less than zero |
| `NonNegative` | `NonNegative()` | `MinValueRule<double>(0.0)` — greater than or equal to zero |
| `NonPositive` | `NonPositive()` | `MaxValueRule<double>(0.0)` — less than or equal to zero |
| `MultipleOf` | `MultipleOf(double divisor, string? message)` | `MultipleOfRule` — throws `ArgumentException` for a zero divisor; relative-tolerance comparison (`1e-12`) |
| `Finite` | `Finite(string? message)` | `FiniteRule` — `double.IsFinite` |
| `Safe` | `Safe(string? message)` | `SafeIntegerRule` — integer within `int.MinValue`..`int.MaxValue` |

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

var age = Z.Number().Min(0).Max(120).Int().Validate(25.0);
```

## Numeric coercion

When a `Z.Number()` is used as an object field or union option, boxed values are coerced via `IConvertible` (invariant culture) — for example a `long` from a `Dictionary<string, object?>` validates against a `Z.Number()` field. Non-numeric values fail with `invalid_type`.
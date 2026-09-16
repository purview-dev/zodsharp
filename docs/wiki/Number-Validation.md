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
| `Positive` | `Positive()` | `MinValueRule<double>(0.0)` |
| `Negative` | `Negative()` | `MaxValueRule<double>(0.0)` |
| `MultipleOf` | `MultipleOf(double divisor, string? message)` | `MultipleOfRule` — throws `ArgumentException` for a zero divisor; tolerance-based |
| `Finite` | `Finite(string? message)` | `FiniteRule` — `double.IsFinite` |
| `Safe` | `Safe(string? message)` | `SafeIntegerRule` — integer within `int.MinValue`..`int.MaxValue` |

## Examples

```csharp
var positive = Z.Number().Positive();
var negative = Z.Number().Negative();

var multipleOf = Z.Number().MultipleOf(10); // multiples of 10
var finite = Z.Number().Finite();           // rejects Infinity / NaN
var safe = Z.Number().Safe();               // safe integer range
var whole = Z.Number().Int();               // no fractional part

var age = Z.Number().Min(0).Max(120).Int().Validate(25.0);
```

## Numeric coercion

When a `Z.Number()` is used as an object field or union option, boxed values are coerced via `IConvertible` (invariant culture) — for example a `long` from a `Dictionary<string, object?>` validates against a `Z.Number()` field. Non-numeric values fail with `invalid_type`.
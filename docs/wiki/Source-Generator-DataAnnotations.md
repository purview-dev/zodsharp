# Source Generator DataAnnotations

The `[ZodSchema]` generator reads `System.ComponentModel.DataAnnotations` attributes and emits direct, typed codegen — no reflection at runtime.

## Supported attributes

| Attribute | Generated behaviour | Failure code |
|---|---|---|
| `[Required]` | nullable property must not be null (strings with `AllowEmptyStrings=false` must be non-empty) | `missing_field` |
| `[Length(min, max)]` | min/max size with `too_small`/`too_big`; applies to strings, arrays (incl. jagged/rectangular), and countable collections | `too_small` / `too_big` |
| `[StringLength(max)]` / `[StringLength(max, MinimumLength=min)]` | string size limits via direct `.Length` | `too_small` / `too_big` |
| `[MinLength(n)]` | only checked when `n > 0` | `too_small` |
| `[MaxLength(n)]` | only checked when `n >= 0` | `too_big` |
| `[Range(...)]` | inclusive (or exclusive) numeric/parsed bounds | `invalid_range` |
| `[RegularExpression(pattern)]` | compiled `Regex` field, checked on non-empty strings | `invalid_string` |
| `[AllowedValues(...)]` | typed equality checks against the allowed set | `invalid_value` |
| `[DeniedValues(...)]` | typed equality checks against the denied set | `invalid_value` |
| `[EmailAddress]` | reuses `ZodSharp.Rules.EmailRule` on non-empty strings | `invalid_string` |
| `[Url]` | reuses `UrlRule` | `invalid_string` |
| `[Phone]` | reuses `PhoneRule` | `invalid_string` |
| `[CreditCard]` | reuses `CreditCardRule` | `invalid_string` |
| `[Base64String]` | reuses `Base64StringRule` | `invalid_string` |
| `[Compare(otherProperty)]` | typed equality between two properties | `mismatch` |
| `[Display(Name=...)]` | not validated; `Name` used as the display name in messages and `{0}` placeholders | — |

`[Length]` follows DataAnnotations null semantics: `null` is valid unless `[Required]` is also present.

## Size validators and structured issues

Size attributes generate direct `Length` or `Count` access when possible:

- `string` → `.Length`.
- arrays (including rectangular arrays) → `.Length`.
- jagged arrays → outer-array `.Length`.
- countable collections → `.Count`.
- `IEnumerable` / `IEnumerable<T>` → a single counted pass via `CollectionCountHelper.GetCount` (fast paths for `ICollection<T>`, `IReadOnlyCollection<T>`, and non-generic `ICollection`).

Structured size failures expose the same metadata as the runtime API:

- `Code`: `too_small` or `too_big`.
- `Origin`: `string` for strings, `array` for arrays and collections.
- `Minimum` / `Maximum`: the inclusive bound.
- `Inclusive`: `true`.
- `Path`: the property path.

```csharp
[ZodSchema]
public sealed class Basket
{
    [Required]
    [Length(2, 5)]
    public List<string>? Items { get; set; }
}

var result = BasketSchema.Validate(new Basket { Items = ["apple"] });
// result.Errors[0].Code == "too_small"
// result.Errors[0].Minimum == 2
// result.Errors[0].Origin == "array"
// result.Errors[0].Inclusive == true
```

> [!NOTE]
> Today the generator reports `Origin = "array"` for both arrays and collections; there is no `"collection"` origin in generated code.

## Range

`[Range]` supports three constructor shapes plus `MinimumIsExclusive`, `MaximumIsExclusive`, `ConvertValueInInvariantCulture`, and `ParseLimitsInInvariantCulture`:

- `[Range(int, int)]` and `[Range(double, double)]` — literal numeric bounds.
- `[Range(typeof(T), "min", "max")]` — parsed bounds for numeric types and comparable types.

Comparable range targets include `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and `Version` (which uses `CompareTo`), plus any type implementing `IComparable` with user-defined comparison operators. Bounds are emitted as static typed fields and compared without runtime attribute execution.

## Error message customization

Honours `ErrorMessage`, or `ErrorMessageResourceName` + `ErrorMessageResourceType`, with `{0}` (display name), `{1}`, and `{2}` (bound) placeholders formatted via `string.Format(CultureInfo.CurrentCulture, ...)`. Providing only one of the resource name/type pair is reported as ZODSGEN005.

## Type applicability diagnostics

Misuse is reported at compile time rather than silently ignored:

- `[Length]` with `min > max` → ZODSGEN003.
- `[Length]` on an unsupported target (e.g. `decimal`) → ZODSGEN004.
- String-only attributes (`[RegularExpression]`, `[EmailAddress]`, `[Url]`, `[Phone]`, `[CreditCard]`, `[Base64String]`) on non-string targets, `[AllowedValues]`/`[DeniedValues]` on unsupported types, or `[Range]` on unsupported types → ZODSGEN006.
- `[Compare]` referencing an unknown property → ZODSGEN020.

See [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the full list.
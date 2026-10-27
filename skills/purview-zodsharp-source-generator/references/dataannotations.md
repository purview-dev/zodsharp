# DataAnnotations coverage

The `[ZodSchema]` generator reads `System.ComponentModel.DataAnnotations` attributes and emits
direct, typed codegen. Nothing is reflected at runtime.

## Supported attributes

| Attribute | Generated behaviour | `Code` / `Category` |
| --- | --- | --- |
| `[Required]` | `RequiredAttribute` semantics: a string is missing when `null`, empty or whitespace-only; `AllowEmptyStrings = true` rejects only `null` | `missing_field` / `invalid_type` |
| `[Length(min, max)]` | min/max size; strings, arrays (including jagged and rectangular), countable collections | `invalid_length` / `too_small`, `too_big` (both bounds) |
| `[StringLength(max)]`, `[StringLength(max, MinimumLength=min)]` | string size via direct `.Length` | `invalid_length` / `too_small`, `too_big` (both bounds) |
| `[MinLength(n)]` | checked only when `n > 0` | `too_small` / `too_small` |
| `[MaxLength(n)]` | checked only when `n >= 0` | `too_big` / `too_big` |
| `[Range(…)]` | inclusive or exclusive (`MinimumIsExclusive`/`MaximumIsExclusive`) bounds in `Lower`/`Upper`; origin from the value's Zod type | `invalid_range` / `too_small`, `too_big` |
| `[RegularExpression(pattern)]` | compiled `Regex` field, checked on non-empty strings; `pattern` parameter | `invalid_string` / `invalid_format` |
| `[AllowedValues(…)]` | typed equality against the allowed set; enums listed by name in `options` | `invalid_value` / `invalid_value` |
| `[DeniedValues(…)]` | typed equality against the denied set; on an enum property the values are absorbed into the automatic enum rule instead | `invalid_value` / `invalid_value` |
| `[EmailAddress]` | reuses `ZodSharp.Rules.EmailRule` on non-empty strings | `invalid_string` / `invalid_format` |
| `[Url]`, `[Phone]`, `[CreditCard]`, `[Base64String]` | reuse `UrlRule`, `PhoneRule`, `CreditCardRule`, `Base64StringRule` | `invalid_string` / `invalid_format` |
| `[Compare(otherProperty)]` | typed equality between two properties | `mismatch` / `invalid_value` |
| `[Display(Name=…)]`, `[Display(Name=…, ResourceType=…)]` | not validated; the display name in messages (`{displayName}`, `{0}`); `ResourceType` is read at runtime | — |
`[Length]` follows DataAnnotations null semantics: `null` is valid unless `[Required]` is also
present.

`[RequiredZod]` — the generated `RequiredRule<T>` attribute — mirrors `[Required]`: it is emitted
*outside* the generator's non-null guard, so an absent value is reported as `missing_field` even
though every other rule bound to the member is skipped for `null`. Its `AllowEmptyStrings` has the
same meaning as `RequiredAttribute.AllowEmptyStrings`, and it adds `TrimWhitespace`, so it is a
drop-in replacement.

## Positional records

A positional record declares its properties from the primary-constructor parameter list, and the C#
compiler applies an attribute valid on both a parameter and a property **to the parameter only**. The
generator therefore reads attributes on the synthesized property *and* on the primary-constructor
parameter:

```csharp
[ZodSchema]
public sealed record RepositoryInventorySummary(
    [NonSentinel] string Name,
    [NonSentinel] string CanonicalReference,
    [NullOrNonWhiteSpace] string? DefaultBranch,
    [NonSentinel] DateTimeOffset LastObservedAt
);
```

`[property: NonSentinel]` is equally honoured. When both targets carry the same attribute type the
property target wins and the rule runs **once**. Declared properties, classes and record structs are
unaffected.

## Size validators and structured issues

Size attributes generate direct `Length`/`Count` access where possible:

- `string` → `.Length`
- arrays, including rectangular → `.Length`
- jagged arrays → outer-array `.Length`
- countable collections → `.Count`
- `IEnumerable` / `IEnumerable<T>` → one counted pass via `CollectionCountHelper.GetCount`, with fast
  paths for `ICollection<T>`, `IReadOnlyCollection<T>` and non-generic `ICollection`

A structured size failure carries the same metadata as the runtime API: `Code` (`invalid_length` for
`[Length]`/`[StringLength]`, `too_small`/`too_big` for `[MinLength]`/`[MaxLength]`), `Category`
(`too_small`/`too_big` by direction), `Origin` (`string` for strings, `array` for arrays, `collection`
for countable and `IEnumerable` collections), `Lower`/`Upper` (both bounds for `[Length]` and
`[StringLength]`) and `FullPath`.

```csharp
[ZodSchema]
public sealed class Basket
{
    [Required]
    [Length(2, 5)]
    public List<string>? Items { get; set; }
}

var result = BasketSchema.Validate(new Basket { Items = ["apple"] });
// Code == "invalid_length", Category == "too_small", Lower == ErrorBound(2, Inclusive: true)
```

## `[Range]`

Three constructor shapes, plus `MinimumIsExclusive`, `MaximumIsExclusive`,
`ConvertValueInInvariantCulture` and `ParseLimitsInInvariantCulture`:

- `[Range(int, int)]` and `[Range(double, double)]` — literal numeric bounds.
- `[Range(typeof(T), "min", "max")]` — parsed bounds for numeric and comparable types.

Comparable targets include `TimeSpan`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` and
`Version` (which uses `CompareTo`), plus any `IComparable` with user-defined comparison operators.
Bounds are emitted as static typed fields and compared without running the attribute at runtime.

## Error messages

Messages are formatted **at validation time** by `ZodMessageFormat` from a named template kept in
`ValidationError.MessageFormat`, with `displayName`, `member`, `declaringType` and the rule's values
(`minimum`, `maximum`, `pattern`, `options`, …) in `Parameters`. Formatting never throws: a stray brace
in a custom message renders literally.

`ErrorMessage`, or `ErrorMessageResourceName` + `ErrorMessageResourceType`, replace the text only (the
code, category and bounds are kept). Positional placeholders keep their DataAnnotations meaning: `{0}` is
the display name and `{1}`/`{2}` the attribute's values - for `[Length]` `{1}` is the minimum and `{2}`
the maximum, as `LengthAttribute` formats them; for `[StringLength]` `{1}` is the maximum and `{2}` the
minimum. Supplying only one half of the resource name/type pair is `ZODSGEN005`; a placeholder the
attribute does not supply is `ZODSGEN052`.
## Applicability is a compile error, not a silent skip

- `[Length]` with `min > max` → `ZODSGEN003`.
- `[Length]` on an unsupported target, such as `decimal` → `ZODSGEN004`.
- String-only attributes (`[RegularExpression]`, `[EmailAddress]`, `[Url]`, `[Phone]`,
  `[CreditCard]`, `[Base64String]`) on a non-string, `[AllowedValues]`/`[DeniedValues]` on an
  unsupported type, or `[Range]` on an unsupported type → `ZODSGEN006`.
- `[Compare]` naming an unknown property → `ZODSGEN020`.

## Built-in rule attributes

Every built-in rule ships a generated attribute in the `ZodSharp.Rules` namespace, usable alongside
the DataAnnotations above:

`[Email]`, `[E164]`, `[Ulid]`, `[Uuid]`, `[Jwt]`, `[IpAddress]`, `[Hex]`, `[Regex]`, `[StartsWith]`,
`[EndsWith]`, `[Includes]`, `[MultipleOf]`, `[Finite]`, `[SafeInteger]`, `[Int]`, `[Uri]`,
`[Base64Url]`, `[Nanoid]`, `[Cuid2]`, `[DateString]`, `[DatetimeString]`, `[TimeString]`, `[Emoji]`,
`[Xid]`, `[Ksuid]`, `[Duration]`, `[Guid]`, `[Cidr]`, `[NonSentinel]`, `[NullOrNonWhiteSpace]`,
`[MinValue]`, `[MaxValue]`, `[GreaterThan]`, `[LessThan]`, `[GreaterThanOrEqual]`,
`[LessThanOrEqual]`, `[Even]`, `[Odd]`, `[Enum]`, `[RequiredZod]`, `[RangeZod]`, `[LengthZod]`,
`[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`, `[DeniedValuesZod]`.

A value the rule declares **without a default** becomes a required constructor argument —
`[Regex("…")]`, `[UUID(UuidVersion.V4)]`, `[Uri(UriKind.Absolute)]`, `[MinValue(3)]` — while
defaulted values and `Message`/`Code` stay named properties. A rule with overloaded constructors
mirrors each overload, so `[Uuid]` is the versionless rule and `[UUID(UuidVersion.V4)]` the versioned
one. `[UUID]` also applies to a `Guid` member or a `[Scalar<Guid>]` type, because a `Guid` *is* a
UUID — which is how a time-ordered identifier is constrained to an RFC version.

Names colliding with a DataAnnotations attribute take a `Zod` suffix: `[MinLengthZod]`,
`[MaxLengthZod]`, `[UrlZod]`, `[PhoneZod]`, `[CreditCardZod]`, `[Base64StringZod]`, `[RequiredZod]`,
`[RangeZod]`, `[LengthZod]`, `[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`,
`[DeniedValuesZod]`.

A custom rule reaches the same pipeline through `[ZodRule(typeof(MyRule))]` on an attribute, or
`[ZodRule]` on the rule to have the attribute generated.

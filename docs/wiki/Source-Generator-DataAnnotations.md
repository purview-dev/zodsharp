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
| `[DeniedValues(...)]` | typed equality checks against the denied set; for an enum property the values are absorbed into the automatic enum rule instead | `invalid_value` |
| `[EmailAddress]` | reuses `ZodSharp.Rules.EmailRule` on non-empty strings | `invalid_string` |
| `[Url]` | reuses `UrlRule` | `invalid_string` |
| `[Phone]` | reuses `PhoneRule` | `invalid_string` |
| `[CreditCard]` | reuses `CreditCardRule` | `invalid_string` |
| `[Base64String]` | reuses `Base64StringRule` | `invalid_string` |
| `[Compare(otherProperty)]` | typed equality between two properties | `mismatch` |
| `[Display(Name=...)]` | not validated; `Name` used as the display name in messages and `{0}` placeholders | — |

`[Length]` follows DataAnnotations null semantics: `null` is valid unless `[Required]` is also present.

`[RequiredZod]` (the generated `RequiredRule<T>` attribute) mirrors `[Required]`: it is emitted outside the generator's non-null guard, so an absent value is reported as `missing_field` even though every other rule bound to the member is skipped for `null`. Its `AllowEmptyStrings` property has the same name and meaning as `RequiredAttribute.AllowEmptyStrings`, and it additionally exposes `TrimWhitespace`, so it is a drop-in replacement for `[Required]`.

## Positional records

A positional record declares its properties from the primary-constructor parameter list, and the C# compiler applies an attribute that is valid on both a parameter and a property to the parameter only. The generator therefore reads the attributes on the synthesized property *and* on the primary-constructor parameter, so a validation attribute written directly on a positional parameter is honoured:

```csharp
[ZodSchema]
public sealed record RepositoryInventorySummary(
    [NonSentinel] string Name,
    [NonSentinel] string CanonicalReference,
    [NullOrNonWhiteSpace] string? DefaultBranch,
    [NonSentinel] DateTimeOffset LastObservedAt
);
```

`[property: NonSentinel]` (the explicit property target) is equally honoured; when both targets carry the same attribute type the property target wins and the rule runs once. Declared properties, classes, and record structs are unaffected.

## Enum properties

Enum properties are validated automatically — the generator rejects a value that is not a defined member of the enum type with `invalid_enum_value`. The check is emitted as `ZodSharp.Rules.EnumRule<TEnum>`. Members can be excluded with `[ZodIgnore]` (on the enum member, for every property of that type) or `[DeniedValues]` (on the property only). `[Flags]` enums and properties with an explicit `[AllowedValues]` allow-list are not auto-validated, and the whole feature is disabled with `[ZodSchema(ValidateEnumValues = false)]`. See [Source Generator](Source-Generator.md#automatic-enum-validation) for the full rules and examples.

## Size validators and structured issues

Size attributes generate direct `Length` or `Count` access when possible:

- `string` → `.Length`.
- arrays (including rectangular arrays) → `.Length`.
- jagged arrays → outer-array `.Length`.
- countable collections → `.Count`.
- `IEnumerable` / `IEnumerable<T>` → a single counted pass via `CollectionCountHelper.GetCount` (fast paths for `ICollection<T>`, `IReadOnlyCollection<T>`, and non-generic `ICollection`).

Structured size failures expose the same metadata as the runtime API:

- `Code`: `too_small` or `too_big`.
- `Origin`: `string` for strings, `array` for arrays, `collection` for countable/`IEnumerable` collections.
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
> The generator reports `Origin = "array"` for arrays and `Origin = "collection"` for countable/`IEnumerable` collections.

## Collection null items

A collection property rejects `null` elements by default: the generated validator reports `invalid_type` at the element's own path (`["Items", 0]`). Control it per property with `[CollectionBehavior(AllowNulls = true/false)]`, per element type with `T?`, per type with `[ZodSchema(AllowNulls = ...)]`, or per assembly with `[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]` and the `ZodSharpAllowNulls` MSBuild property. The most specific statement wins. See [Collection null items](Source-Generator.md#collection-null-items) for the full precedence, and `ZODSGEN046` for the attribute-applicability warning.

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

## Custom attributes

The same pipeline honours custom rules exposed as validation attributes. Mark the attribute with `[ZodRule(typeof(MyRule))]` (or mark the rule itself with `[ZodRule]` to have the attribute generated), and properties annotated with it are validated through the rule. See [Custom Rules](Custom-Rules.md).

Every built-in rule also ships a generated attribute in the `ZodSharp.Rules` namespace, so it can be used directly alongside the attributes above: `[Email]`, `[E164]`, `[Ulid]`, `[Uuid]`, `[Jwt]`, `[IpAddress]`, `[Hex]`, `[Regex]`, `[StartsWith]`, `[EndsWith]`, `[Includes]`, `[MultipleOf]`, `[Finite]`, `[SafeInteger]`, `[Int]`, `[Uri]`, `[Base64Url]`, `[Nanoid]`, `[Cuid2]`, `[DateString]`, `[DatetimeString]`, `[TimeString]`, `[Emoji]`, `[Xid]`, `[Ksuid]`, `[Duration]`, `[Guid]`, `[Cidr]`, `[NonSentinel]`, `[NullOrNonWhiteSpace]`, `[MinValue]`, `[MaxValue]`, `[GreaterThan]`, `[LessThan]`, `[GreaterThanOrEqual]`, `[LessThanOrEqual]`, `[Even]`, `[Odd]`, `[Enum]`, `[RequiredZod]`, `[RangeZod]`, `[LengthZod]`, `[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`, and `[DeniedValuesZod]`. A value the rule declares without a default is a required constructor argument — `[Regex("…")]`, `[UUID(UuidVersion.V4)]`, `[Uri(UriKind.Absolute)]`, `[MinValue(3)]` — while defaulted values and `Message`/`Code` stay named properties. A rule with overloaded constructors mirrors each overload, so `[Uuid]` uses the versionless UUID rule and `[UUID(UuidVersion.V4)]` the versioned one. `[UUID]` also applies to a `Guid` member or a `[Scalar<Guid>]` type (a `Guid` is a UUID, so the rule validates its bytes), which is how a time-bound identifier that must be an RFC version is constrained. The rules whose name collides with a DataAnnotations attribute use a `Zod` suffix (`[MinLengthZod]`, `[MaxLengthZod]`, `[UrlZod]`, `[PhoneZod]`, `[CreditCardZod]`, `[Base64StringZod]`, `[RequiredZod]`, `[RangeZod]`, `[LengthZod]`, `[StringLengthZod]`, `[CompareZod]`, `[AllowedValuesZod]`, `[DeniedValuesZod]`). See [Built-in attributes](Custom-Rules.md#built-in-attributes-shipped-with-purviewzodsharp).
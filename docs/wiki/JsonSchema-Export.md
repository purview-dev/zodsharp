# JSON Schema Export

Export a Purview.ZodSharp schema to a JSON Schema (Draft 2020-12) definition with `Z.ToJsonSchema`, which lives in the core `Purview.ZodSharp` package.

```csharp
using ZodSharp;

var userSchema = Z.Object()
    .Field("name", Z.String().Min(3))
    .Field("email", Z.String().Email())
    .Field("age", Z.Number().Min(0).Int())
    .Build();

var jsonSchema = Z.ToJsonSchema<Dictionary<string, object?>>(userSchema, new ToJsonSchemaOptions
{
    Title = "User",
    Id = "https://example.com/schemas/user.json"
});
```

## ToJsonSchemaOptions

| Property | Default | Purpose |
|---|---|---|
| `IncludeSchema` | `true` | emit `$schema: "https://json-schema.org/draft/2020-12/schema"` |
| `Id` | `null` | sets `$id` |
| `Title` | `null` | sets `title` |

## JsonSchemaDefinition

`Z.ToJsonSchema` returns `JsonSchemaDefinition` (namespace `ZodSharp.JsonSchema`), a mutable POCO mirroring the JSON Schema keywords:

- **Identity**: `Schema` (`$schema`), `Id` (`$id`), `Ref` (`$ref`).
- **Type/title**: `Type`, `Title`, `Description`, `Default`, `Format`.
- **String**: `MinLength`, `MaxLength`, `Pattern`.
- **Number**: `Minimum`, `Maximum`, `ExclusiveMinimum`, `ExclusiveMaximum`, `MultipleOf`.
- **Array**: `Items`, `MinItems`, `MaxItems`, `UniqueItems`.
- **Object**: `Properties`, `Required`, `AdditionalProperties`.
- **Composition**: `AnyOf`, `OneOf`, `AllOf`.
- **Enum/const**: `Enum`, `Const`.
- **Definitions**: `Defs` (2020-12) and `Definitions` (draft-07).
- **Metadata**: `Deprecated`, `ReadOnly`, `WriteOnly`, `Examples`, `Nullable` (OpenAPI 3.0).

## Supported schema types

`ToJsonSchemaConverter` handles `ZodString`, `ZodNumber`, `ZodBoolean`, `ZodNull`, `ZodObject`, `ZodOptional`, `ZodUnion`, `ZodArray`, `ZodRecord`, `ZodLiteral`, `ZodNullable`, `ZodNativeEnum`/`ZodEnum`, and `ZodLazy`.

- Objects emit `additionalProperties: false`.
- Records (`Z.Record(valueSchema)`) emit `{ "type": "object", "additionalProperties": <value> }`.
- Literals emit `const` (and `type`).
- Native enums emit `enum` with the defined members in their System.Text.Json wire form: the numeric values by default, or the member names when the enum is serialized as a string. A `[Flags]` enum emits `type: integer` instead, because a combination is valid without being a defined member.
- Lazy/recursive schemas emit `$ref` entries under `$defs` (e.g. `#/$defs/__lazyN`).

Rules that describe their own JSON Schema constraint implement `IJsonSchemaRule` and are applied during
export (see [Validation rules and JSON Schema](#validation-rules-and-json-schema) below).

## Export a generated `[ZodSchema]`

A source-generated `[ZodSchema]` schema can be exported directly — there is no need to rebuild its rules by
hand with `Z.Object()`/`Z.String()`. The generated `{Type}Schema` class exposes a static `BuildJsonSchema()`
method, and the generated `{Type}SchemaValidator` (the DI adapter) implements `IJsonSchemaProvider`, so it can
be handed to `Z.ToJsonSchema`:

```csharp
using ZodSharp;

// Either call the generated schema directly…
var jsonSchema = RepositoryInventoryItemSchema.BuildJsonSchema();

// …or export the generated validator (the DI adapter) through Z.ToJsonSchema.
var exported = Z.ToJsonSchema(new RepositoryInventoryItemSchemaValidator());
```

The definition is produced from the same metadata the generated validator uses, so the two cannot drift
apart: property names, nested objects, native enums, nullable fields, required fields, `[NonSentinel]`,
`[NullOrNonWhiteSpace]`, DataAnnotations, and `[ZodRule]`-mapped rules are all reflected.

- **Property names** use the System.Text.Json wire form: an explicit `[JsonPropertyName]` wins, otherwise the
  name is camelCased to match the ASP.NET Core / `JsonSerializerDefaults.Web` default. A non-camelCase global
  naming policy configured at runtime is not visible to the generator, so a consumer using one must adjust the
  exported names (or declare `[JsonPropertyName]`).
- **Required fields** follow the nullable reference-type annotation: a non-nullable reference type (or
  `Nullable<T>`) is required, and a nullable one (`string?`) is wrapped in `anyOf: [ …, { "type": "null" } ]`
  and is not required. `[Required]` also makes a field required. The generated validator agrees: a
  non-nullable reference type rejects null and its rules run unconditionally, while a nullable one skips them
  for null. A rule that rejects an absent value (for example `[RequiredZod]`) makes the field non-null even
  when its annotation is nullable.
- **Collection items** follow the null-items setting (see
  [Collection null items](Source-Generator.md#collection-null-items)): a collection whose elements may be null
  exports `items` wrapped in `anyOf: [ …, { "type": "null" } ]`; otherwise `items` is the element's own schema.
- **Scalar value objects** (`[Scalar<TValue>]`) export as their underlying value's schema — a `Guid` scalar is
  a canonical 8-4-4-4-12 hexadecimal string (a `pattern`, not Zod's RFC `uuid` format, because C# `Guid`
  accepts any 128-bit value) — not as an object wrapping a `Value` property.
- **Enums** follow System.Text.Json. They export as numbers by default; a per-type or per-property
  `[JsonConverter(typeof(JsonStringEnumConverter))]` (or the assembly-level `ZodSharpEnumSerialization=String`
  switch, matching a global converter) makes them strings, and `ZodSharpEnumNamingPolicy=CamelCase` matches a
  `JsonNamingPolicy.CamelCase`. A member's `[JsonStringEnumMemberName]` name is used verbatim. A `[Flags]`
  enum exports as `type: integer`.
- **Unsupported rules** are not dropped silently: a rule the exporter cannot represent is listed under the
  `x-zodsharp-unsupported` extension so the exported schema is visibly incomplete.

> `additionalProperties: false` is emitted for objects (matching the runtime exporter). System.Text.Json
> ignores unknown members by default, so a request with extra keys is accepted by the C# validator but
> rejected by the exported schema.

### Opting a type out of schema generation

Not every `[ZodSchema]` type belongs to the cross-platform contract surface — a validation-only domain or
options type does not need to be exported to TypeScript/JavaScript. Two switches disable the export members
(the static `BuildJsonSchema` and the `IJsonSchemaProvider` implementation) while keeping validation intact:

```csharp
[ZodSchema(GenerateJsonSchema = false)] // this type only
public sealed record InternalThing;
```

```xml
<!-- the whole assembly -->
<PropertyGroup>
  <ZodSharpGenerateJsonSchemas>false</ZodSharpGenerateJsonSchemas>
</PropertyGroup>
```

A type that exports JSON Schema must not reference a type whose export is disabled, because the nested
`BuildJsonSchema` does not exist. The generator falls back to an empty (any) schema for that property and the
analyzer reports **ZODSGEN045** naming the property and the disabled type, so the gap is surfaced at build
time rather than shipped silently.

## Dictionaries

System.Text.Json writes a dictionary as a JSON object, so a generated dictionary property
(`IDictionary<TKey, TValue>` / `IReadOnlyDictionary<TKey, TValue>`) exports an object rather than an array:

```json
"variants": {
  "type": "object",
  "additionalProperties": { "type": "object", "properties": { … } }
}
```

- `additionalProperties` is the **value** type's schema. A nested `[ZodSchema]` value type is referenced
  inline, exactly as an element schema is for an array; a type whose export is disabled falls back to `{}`
  and reports `ZODSGEN045`.
- A nullable value (`Dictionary<string, Child?>`, `Dictionary<string, int?>`, or
  `[CollectionBehavior(AllowNulls = true)]`) wraps the value schema in
  `anyOf: [ …, { "type": "null" } ]`, mirroring the sequence `items` branch.
- `[Length]`/`[MinLength]`/`[MaxLength]` export `minProperties`/`maxProperties` (not `minItems`/`maxItems`).
- The TypeScript counterpart is `z.record(z.string(), V)`. Importing an object with `additionalProperties`
  produces a record schema — see [JSON Schema Import](JsonSchema-Import.md#records).

## Validation rules and JSON Schema

Every rule that can describe its own JSON Schema constraint implements `ZodSharp.JsonSchema.IJsonSchemaRule`.
The exporter applies these rules through `JsonSchemaRules.Apply`, and the generated `BuildJsonSchema()` does
the same, so a rule maps to JSON Schema identically on both paths:

| Rule | JSON Schema |
|---|---|
| `MinLengthRule` / `MaxLengthRule` | `minLength` / `maxLength` |
| `MinValueRule<T>` / `MaxValueRule<T>` | `minimum` / `maximum` |
| `GreaterThanRule<T>` / `LessThanRule<T>` | `exclusiveMinimum` / `exclusiveMaximum` |
| `MultipleOfRule<T>` / `EvenRule<T>` / `OddRule<T>` | `multipleOf` (odd uses `not: { multipleOf: 2 }`) |
| `IntRule<T>` / `SafeIntegerRule` | `type: integer` (safe integer also bounds to the `int` range) |
| `EmailRule` / `UrlRule` / `UriRule` | `format: email` / `uri` |
| `UUIDRule` (versionless) | `format: uuid` |
| `UUIDRule(UuidVersion.Vx)` | `format: uuid` plus a `pattern` that pins the version and variant |
| `GuidRule` | a `pattern` for a canonical 8-4-4-4-12 hexadecimal identifier (any version/variant) |
| `RegexRule` | `pattern` |
| `StartsWithRule` / `EndsWithRule` / `IncludesRule` | anchored / escaped `pattern` |
| `NonSentinelRule<string>` / `NullOrNonWhiteSpaceRule` | `minLength: 1` + `pattern: "\\S"` |
| `NonSentinelRule<Guid>` | a `pattern` that rejects the all-zero UUID |
| `NonSentinelRule<DateOnly>` | a `pattern` that rejects `0001-01-01` and `9999-12-31` |
| `NonSentinelRule<TimeOnly>` | a `pattern` that rejects `00:00:00` and `23:59:59.9999999` |
| `NonSentinelRule<DateTime>` | a `pattern` that rejects the deterministic min/max timestamp forms (no offset, `Z`, or zero offset) |
| `NonSentinelRule<DateTimeOffset>` | an offset-aware `pattern` that rejects every RFC 3339 representation of both instant bounds; the `Utc` contract narrows it to zero-offset form |
| `AllowedValuesRule<T>` / `DeniedValuesRule<T>` / `EnumRule<TEnum>` | `enum` / `not: { enum: … }` |
| `FiniteRule<T>` / `RequiredRule<T>` | no keyword (implied by `number` / the object's `required` list) |

A C# `Guid` is exported as a `pattern`, not `format: uuid`, because `Guid` accepts any 128-bit value while an
RFC UUID validator (Zod's `z.uuid()`, which `z.fromJSONSchema` applies to **both** `uuid` and `guid`) checks the
version and variant nibbles. A time-bound identifier generated by `FastGuid.NewSqlServerGuid()` /
`NewPostgreSqlGuid()` is deliberately not an RFC UUID and would be rejected by that validator. A Guid that must
be a specific RFC version opts in with `[UUID(UuidVersion.Vx)]` (which also validates a `Guid` or a
`[Scalar<Guid>]`), and exports `format: uuid` plus a version-pinning `pattern`.

JSON Schema has a single `pattern` keyword, so when more than one rule constrains the pattern (for example a
Guid plus `[NonSentinel]`, or a regex plus `[NonSentinel]`) the constraints are composed under `allOf` rather
than one overwriting the other. Each `allOf` branch repeats the schema's `type`, because Zod's `fromJSONSchema`
discards a branch without one.

A rule that does not implement `IJsonSchemaRule` is reported through
`JsonSchemaDefinition.UnsupportedRules` (`x-zodsharp-unsupported`). Consumer rules opt in by implementing the
interface; for example a custom null-or-non-whitespace rule would add `IJsonSchemaRule` and set
`minLength`/`pattern` on the schema passed to `ApplyTo`. The built-in `NullOrNonWhiteSpaceRule` already does
this, so a domain that needs that behaviour should use `[NullOrNonWhiteSpace]` rather than a bespoke rule.

## Temporal sentinels and the DateTimeOffset wire form

`DateOnly`, `TimeOnly` and `DateTime` export a `pattern` that rejects the System.Text.Json wire forms of their
`MinValue`/`MaxValue`. The temporal mappings are exact for the deterministic forms: a `DateOnly` has one
`yyyy-MM-dd` form, `TimeOnly` is a local `HH:mm:ss[.fffffff]` value, and a `DateTime` is deterministic when it
is `Unspecified` or `Utc` (or written with a zero offset). A `DateTimeKind.Local` boundary value can carry an
arbitrary machine offset, and JSON Schema has no way to know the receiver's time zone; use
`Unspecified`/`Utc`, or `DateTimeOffset`/`Z.DateTimeOffset()` whose pattern is exact, for a portable contract.

`TimeOnly` is exported as a `pattern` rather than `format: time`. JSON Schema's `format: time` is an RFC 3339
full-time and requires an offset, while System.Text.Json never writes one for `TimeOnly`; a strict
`format: time` consumer would reject every value.

`DateTimeOffset` compares instants, and RFC 3339 allows the same instant at different offsets
(`0001-01-01T00:00:00+00:00` and `0001-01-01T05:30:00+05:30` are both `DateTimeOffset.MinValue`). The default
`DateTimeOffsetSerialization.PreserveOffset` pattern is offset-aware: it correlates the local time with the
offset and rejects every representation of both bounds. It is exact, so no `x-zodsharp-unsupported` marker is
emitted and no serializer change is required. The pattern is intentionally detailed because it encodes that
relation for both bounds; it stays a standard ECMA-262 `pattern`.

The `Utc` setting is a narrower contract. It requires a `Z` or zero offset and pairs with a converter that
rejects non-UTC input, which is useful when the application wants to standardise on UTC:

```csharp
using ZodSharp;

// Project-wide default (or set ZodSharpDateTimeOffsetSerialization=Utc in Directory.Build.props).
[assembly: ZodSharpSchemaDefaults(
    DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc)]
```

```csharp
// System.Text.Json
using ZodSharp.Json.Serialization.SystemTextJson;
options.Converters.Add(new UtcDateTimeOffsetConverter());

// Newtonsoft.Json — the helper also sets DateParseHandling.None so the converter sees the raw offset.
using ZodSharp.Json.Serialization.NewtonsoftJson;
var settings = new JsonSerializerSettings().UseZodSharpUtcDateTimeOffset();
```

Under the UTC contract the exported `pattern` requires a `Z` or zero offset and rejects both bounds. The
converter must reject a non-UTC offset on read; normalizing it silently would reintroduce the gap. A value such
as `2024-01-02T03:04:05+05:30` is therefore rejected by both sides when the contract is UTC, even though it is
a valid timestamp. Under the default `PreserveOffset`, that value is accepted and the exact offset-aware
pattern still rejects the bounds.

The setting can be overridden at several levels; the most specific statement wins:

| Level | Declaration |
|---|---|
| Property (or positional record parameter) | `[DateTimeOffsetBehavior(DateTimeOffsetSerialization.Utc)]` / `...[PreserveOffset]` |
| Containing type | `[DateTimeOffsetBehavior(DateTimeOffsetSerialization.Utc)]` |
| Assembly | `[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = ...)]` |
| MSBuild | `ZodSharpDateTimeOffsetSerialization` (`Utc` or `PreserveOffset`, for example in `Directory.Build.props`) |
| Built-in default | `PreserveOffset` |

The attribute controls schema generation only. Pair a `Utc` override with `UtcDateTimeOffsetConverter`, and a
`PreserveOffset` override under a project-wide UTC converter with `PreserveOffsetDateTimeOffsetConverter`
(available in both integration packages). A `[Scalar<DateTimeOffset>]` owns its schema, so apply the attribute
to the scalar type rather than to a property whose type is that scalar.

A hand-built fluent schema has no compile-time metadata to read, so set the runtime equivalent on
`ToJsonSchemaOptions` when converting a manual schema:

```csharp
var jsonSchema = Z.ToJsonSchema(
    Z.DateTimeOffset().NonSentinel(),
    new ToJsonSchemaOptions { DateTimeOffsetSerialization = DateTimeOffsetSerialization.Utc });
```

The default remains `PreserveOffset`, which is also exact. The `rule.ApplyTo(schema, ...)` and
`JsonSchemaRules.Apply(schema, rule, ...)` overloads are available for code that builds a definition directly.

## Serialize the definition

Pick the JSON serializer that matches the integration package you referenced:

```csharp
// System.Text.Json (Purview.ZodSharp.SystemTextJson)
using ZodSharp.JsonSchema.SystemTextJson;
var json = System.Text.Json.JsonSerializer.Serialize(jsonSchema, JsonSchemaSerializerOptions.Default);

// Newtonsoft.Json (Purview.ZodSharp.NewtonsoftJson)
using ZodSharp.JsonSchema.NewtonsoftJson;
var json = JsonConvert.SerializeObject(jsonSchema, JsonSchemaSerializerOptions.Default);
```

`JsonSchemaSerializerOptions.Default` (camelCase, ignore nulls, indented) and `.Reading` (camelCase, ignore nulls) are provided by each integration package and map the JSON Schema keyword names (`$schema`, `$id`, `$ref`, `$defs`), with camelCase for every other keyword. `JsonSchemaDefinition` itself carries no serializer annotations, so use these options when serializing it — that also keeps exported definitions round-tripping through the matching import API.

## Round-trip

Import the exported definition back into Purview.ZodSharp with `Z.FromJsonSchema` from an integration package — see [JSON Schema Import](JsonSchema-Import.md) and the round-trip example in the example app (`JsonSchemaExamples`).
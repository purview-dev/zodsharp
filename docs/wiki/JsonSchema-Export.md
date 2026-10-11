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

`ToJsonSchemaConverter` handles `ZodString`, `ZodNumber`, `ZodBoolean`, `ZodNull`, `ZodObject`, `ZodOptional`, `ZodUnion`, `ZodArray`, `ZodLiteral`, `ZodNullable`, `ZodNativeEnum`/`ZodEnum`, and `ZodLazy`.

- Objects emit `additionalProperties: false`.
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
- **Required fields** are the non-nullable ones (and any `[Required]`); a nullable field is wrapped in
  `anyOf: [ …, { "type": "null" } ]`.
- **Scalar value objects** (`[Scalar<TValue>]`) export as their underlying value's schema — a `Guid` scalar is
  a `uuid` string — not as an object wrapping a `Value` property.
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
| `EmailRule` / `UrlRule` / `UriRule` / `UUIDRule` | `format: email` / `uri` / `uuid` |
| `RegexRule` / `GuidRule` | `pattern` |
| `StartsWithRule` / `EndsWithRule` / `IncludesRule` | anchored / escaped `pattern` |
| `NonSentinelRule<string>` / `NullOrNonWhiteSpaceRule` | `minLength: 1` + `pattern: "\\S"` |
| `NonSentinelRule<Guid>` | a `pattern` that rejects the all-zero UUID |
| `AllowedValuesRule<T>` / `DeniedValuesRule<T>` / `EnumRule<TEnum>` | `enum` / `not: { enum: … }` |
| `FiniteRule<T>` / `RequiredRule<T>` | no keyword (implied by `number` / the object's `required` list) |

A rule that does not implement `IJsonSchemaRule` is reported through
`JsonSchemaDefinition.UnsupportedRules` (`x-zodsharp-unsupported`). Consumer rules opt in by implementing the
interface; for example a custom `NullOrValidStringRule` would add `IJsonSchemaRule` and set `minLength`/`pattern`
on the schema passed to `ApplyTo`.

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
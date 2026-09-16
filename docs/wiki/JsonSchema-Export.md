# JSON Schema Export

Export a ZodSharp schema to a JSON Schema (Draft 2020-12) definition with `Z.ToJsonSchema`, which lives in the core `Purview.ZodSharp` package.

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

`ToJsonSchemaConverter` handles `ZodString`, `ZodNumber`, `ZodBoolean`, `ZodNull`, `ZodObject`, `ZodOptional`, `ZodUnion`, `ZodArray`, `ZodLiteral`, `ZodNullable`, and `ZodLazy`.

- Objects emit `additionalProperties: false`.
- Literals emit `const` (and `type`).
- Lazy/recursive schemas emit `$ref` entries under `$defs` (e.g. `#/$defs/__lazyN`).

## Serialize the definition

Pick the JSON serializer that matches the integration package you referenced:

```csharp
// System.Text.Json (Purview.ZodSharp.SystemTextJson)
using ZodSharp.JsonSchema;
var json = System.Text.Json.JsonSerializer.Serialize(jsonSchema, JsonSchemaSerializerOptions.Default);

// Newtonsoft.Json (Purview.ZodSharp.NewtonsoftJson)
using ZodSharp.JsonSchema;
var json = JsonConvert.SerializeObject(jsonSchema, JsonSchemaSerializerOptions.Default);
```

`JsonSchemaSerializerOptions.Default` (camelCase, ignore nulls, indented) and `.Reading` (camelCase, ignore nulls) are provided by each integration package.

## Round-trip

Import the exported definition back into ZodSharp with `Z.FromJsonSchema` from an integration package — see [JSON Schema Import](JsonSchema-Import.md) and the round-trip example in the example app (`JsonSchemaExamples`).
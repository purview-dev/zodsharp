# JSON Schema Import

Import a JSON Schema into a Purview.ZodSharp schema with `Z.FromJsonSchema`. This API is provided by the JSON integration packages — reference either `Purview.ZodSharp.SystemTextJson` or `Purview.ZodSharp.NewtonsoftJson`. The two JSON integrations are mutually exclusive; pick one and import its JSON Schema namespace (`ZodSharp.JsonSchema.SystemTextJson` or `ZodSharp.JsonSchema.NewtonsoftJson`).

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.SystemTextJson; // or ZodSharp.JsonSchema.NewtonsoftJson

var jsonSchemaString = """
    {
      "type": "object",
      "properties": {
        "name": { "type": "string", "minLength": 3 },
        "email": { "type": "string", "format": "email" }
      },
      "required": ["name", "email"]
    }
    """;

var userSchema = Z.FromJsonSchema(jsonSchemaString);
var result = userSchema.Validate(userData);
```

## Overloads

| Signature | Notes |
|---|---|
| `IZodSchema<object, object> FromJsonSchema(string jsonSchema)` | parses the JSON string into a `JsonSchemaDefinition`, then into a schema |
| `IZodSchema<object, object> FromJsonSchema(JsonSchemaDefinition schema)` | import from an already-deserialized definition |

> [!NOTE]
> `Z.FromJsonSchema` is implemented as a C# 14 extension member on `Z`, so it only exists when a JSON integration package is referenced. `Z.ToJsonSchema` is a real static member on `Z` in the core package.

## Supported keywords

`FromJsonSchemaParser` (namespace `ZodSharp.JsonSchema.SystemTextJson` or `ZodSharp.JsonSchema.NewtonsoftJson`) maps:

- `type` — `string` / `number` / `integer` / `boolean` / `null` / `object` / `array`.
- `enum` → `ZodUnion` of literals (a single member becomes a literal); `const` → literal.
- `anyOf` / `oneOf` → `ZodUnion`; `allOf` → the intersection of every branch (and the base schema when the
  schema also declares a `type`). A type-less branch is `any`, matching Zod's `fromJSONSchema`.
- String constraints — `minLength`, `maxLength`, `pattern`, and `format` (`email`, `uri`, `uuid`). The `uuid`/`guid` format maps to the versionless `.UUID()`; a versioned `.UUID(UuidVersion.V7)` schema exports `format: "uuid"` plus a version-pinning `pattern`, and the `allOf` composition preserves that pattern on import. Multiple `pattern` constraints are composed under `allOf`, and every branch is applied.
- Numeric constraints — `minimum`, `maximum`, `multipleOf`; `integer` additionally applies `.Int()`.
- Objects — `required` and optional fields via `Z.Object().Field(...)`.
- Arrays — `items`, `minItems`, `maxItems`.

## Limitations

- `$ref` is supported only for **local** references (`#/...`, for example `#/$defs/Name`). External `$ref` targets throw `NotSupportedException` naming the unsupported reference:

  ```text
  External $ref 'external.json#/$defs/name' is not supported. Only local references ('#/...', for example '#/$defs/Name') can be resolved; inline or pre-resolve external schemas before importing.
  ```

  Inline the referenced schema, or move it under the root `$defs`, before importing. Local references may be cyclic — a reference that is still being resolved becomes a lazy schema.
- The reader binds the JSON Schema keyword names `$schema`, `$id`, `$ref`, and `$defs` (plus the draft-07 `definitions`) through the integration package's `JsonSchemaSerializerOptions`, and the same options write them back, so exported definitions round-trip through either package. `JsonSchemaDefinition` itself carries no serializer annotations, so serialize it with `JsonSchemaSerializerOptions` to get keyword-compliant output.

## Cross-platform reuse

Export a TypeScript/Zod schema to JSON Schema (Zod v4+ `z.toJSONSchema`) and import it on the backend:

```typescript
import { z } from "zod";
const UserSchema = z.object({ username: z.string().min(3), email: z.string().email() });
const jsonSchema = z.toJSONSchema(UserSchema);
```

```csharp
var userSchema = Z.FromJsonSchema(jsonSchemaString);
var result = userSchema.Validate(incomingData);
```

See [Cross-Platform Interop](Cross-Platform-Interop.md) for the repository's fixture-based verification of this loop.
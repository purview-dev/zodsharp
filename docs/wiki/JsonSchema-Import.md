# JSON Schema Import

Import a JSON Schema into a ZodSharp schema with `Z.FromJsonSchema`. This API is provided by the JSON integration packages — reference either `Purview.ZodSharp.SystemTextJson` or `Purview.ZodSharp.NewtonsoftJson` (both expose the same surface).

```csharp
using ZodSharp;

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
| `IZodSchema<object, object> FromJsonSchema(string jsonSchema, FromJsonSchemaOptions? options = null)` | parses the JSON string into a `JsonSchemaDefinition`, then into a schema |
| `IZodSchema<object, object> FromJsonSchema(JsonSchemaDefinition schema, FromJsonSchemaOptions? options = null)` | import from an already-deserialized definition |

`FromJsonSchemaOptions` is currently an empty placeholder reserved for future options.

> [!NOTE]
> `Z.FromJsonSchema` is implemented as a C# 14 extension member on `Z`, so it only exists when a JSON integration package is referenced. `Z.ToJsonSchema` is a real static member on `Z` in the core package.

## Supported keywords

`FromJsonSchemaParser` (namespace `ZodSharp.JsonSchema`) maps:

- `type` — `string` / `number` / `integer` / `boolean` / `null` / `object` / `array`.
- `enum` → `ZodUnion` of literals (a single member becomes a literal); `const` → literal.
- `anyOf` / `oneOf` → `ZodUnion`; `allOf` → first schema.
- String constraints — `minLength`, `maxLength`, `pattern`, and `format` (`email`, `uri`, `uuid`).
- Numeric constraints — `minimum`, `maximum`, `multipleOf`; `integer` additionally applies `.Int()`.
- Objects — `required` and optional fields via `Z.Object().Field(...)`.
- Arrays — `items`, `minItems`, `maxItems`.

## Limitations

- `$ref` is supported only for **local** references (`#/...`); external `$ref` targets throw `NotSupportedException`.
- The options type is currently empty; behaviour is fixed by the supported keyword set above.

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
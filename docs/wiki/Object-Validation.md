# Object Validation

`ZodObject` (namespace `ZodSharp.Schemas`) validates `Dictionary<string, object?>` values. Build schemas with the `ZodObjectBuilder` returned by `Z.Object()`.

```csharp
using ZodSharp;

var userSchema = Z.Object()
    .Field("name", Z.String().Min(1))
    .Field("age", Z.Number().Min(0).Max(120).Int())
    .Field("email", Z.String().Email())
    .Build();

var result = userSchema.Validate(new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "age", 30.0 },
    { "email", "john@example.com" }
});
```

## Behaviour

- `null` input → `invalid_type` (`"Expected object, but got null"`).
- Missing fields are allowed only when the key is optional (`Partial`/`Required`/`.Optional` semantics) or the field schema is itself optional (`IOptionalSchema.IsOptional`, e.g. `Z.Optional(...)`); otherwise `missing_field` with path `[key]`.
- When a missing field's schema `ProvidesValueOnMissing` (e.g. `Z.Default(...)`), the produced value is injected into the output.
- Field values are validated against their schema; failures get the field name prepended to the error path. Changed/coerced values trigger a rebuild of the output dictionary.
- Unknown keys are handled by the object's `UnknownKeyPolicy` or `CatchallSchema` (below).

## Unknown key policies

`UnknownKeyPolicy` is an enum with three values. `Strip` is the default.

| Policy | Behaviour |
|---|---|
| `Strip` (default) | Unknown keys are dropped from the output. |
| `Passthrough` | Unknown keys are kept as-is. |
| `Strict` | Unknown keys fail with `unrecognized_key` and path `[key]`. |

```csharp
var strict = Z.Object().Field("name", Z.String()).Build().Strict();
var permissive = Z.Object().Field("name", Z.String()).Build().Passthrough();
```

## Catchall

`Catchall(IZodSchema<object, object> schema)` validates every unknown key against the schema and includes the validated value in the output. The schema argument must not be `null`.

```csharp
var schema = Z.Object()
    .Field("name", Z.String())
    .Catchall(Z.Number())
    .Build();
```

## Fluent methods

These return a **new** `ZodObject` instance:

| Method | Behaviour |
|---|---|
| `Extend<T>(string key, IZodSchema<T, T> schema)` | add or replace a field |
| `Merge(ZodObject other)` | other's shape overrides; adopts other's `UnknownKeyPolicy` + `CatchallSchema`; optionality per contributing object |
| `Pick(params string[] keys)` | keep only the given keys |
| `Omit(params string[] keys)` | remove the given keys |
| `Partial()` | every shape key optional |
| `Required()` | no optional keys; all shape keys required |
| `Passthrough()` | `UnknownKeyPolicy.Passthrough` |
| `Strict()` | `UnknownKeyPolicy.Strict` |
| `Strip()` | `UnknownKeyPolicy.Strip` |
| `Catchall(IZodSchema<object, object> schema)` | validate unknown keys against a schema |

## Exposed shape

`ZodObject` exposes `Shape`, `UnknownKeyPolicy`, `OptionalKeys`, `RequiredKeys`, and `CatchallSchema` as read-only properties, so metadata is inspectable (used by the JSON Schema exporter).

## Builder

`ZodObjectBuilder` validates its arguments: a null/whitespace field name or a null schema throws `ArgumentNullException`. Typed fields are wrapped so boxed values coerce correctly (see [Core Concepts](Core-Concepts.md)).
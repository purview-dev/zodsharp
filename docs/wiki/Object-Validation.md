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

- `null` input → `invalid_type` (`"Expected object, received null"`, template `ZodMessages.NullValue`).
- Missing fields are allowed only when the key is optional (`Partial`/`Required`/`.Optional` semantics) or the field schema is itself optional (`IOptionalSchema.IsOptional`, e.g. `Z.Optional(...)`); otherwise `missing_field` (category `invalid_type`) at the key's path, with the message `"{key} is required"` (template `"{displayName} is required"`, `displayName` = the key). Generated validators and `RequiredRule` report the same code and template.
- When a missing field's schema `ProvidesValueOnMissing` (e.g. `Z.Default(...)`), the produced value is injected into the output.
- Field values are validated against their schema; failures are re-pathed under the field's member segment (`address.street`) and keep every field of the inner error. A value of the wrong type reports `"Expected string, received number"` with Zod type names in `expected`/`received`. Changed/coerced values trigger a rebuild of the output dictionary.
- Unknown keys are handled by the object's `UnknownKeyPolicy` or `CatchallSchema` (below).

## Error paths

Object errors use member segments for field keys, so `FullPath.ToDottedString()` renders `address.street`, and a
nested array element `items[0].name`. An object key that contains `.`, brackets, quotes or whitespace is quoted
(`["a.b"]`). See [Core Concepts: Paths](Core-Concepts.md#paths) and [Error fields](Core-Concepts.md#error-fields).

## Unknown key policies

`UnknownKeyPolicy` is an enum with three values. `Strip` is the default.

| Policy | Behaviour |
|---|---|
| `Strip` (default) | Unknown keys are dropped from the output. |
| `Passthrough` | Unknown keys are kept as-is. |
| `Strict` | Unknown keys fail with **one** `unrecognized_keys` issue at the object's own path, listing every unknown key in the `keys` parameter (`"Unrecognized keys: x, y"`). |

```csharp
var strict = Z.Object().Field("name", Z.String()).Build().Strict();
var errors = strict.Validate(new Dictionary<string, object?> { ["name"] = "a", ["x"] = 1, ["y"] = 2 }).Errors;
// errors[0].Code == ZodIssueCodes.UnrecognizedKeys ("unrecognized_keys"), errors[0].FullPath.IsRoot == true
// errors[0].Parameters["keys"] holds ["x", "y"]
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
# Fluent Schema API

## The `Z` factory

`public static class Z` (namespace `ZodSharp`) is the entry point for creating schemas.

| Method | Signature | Returns |
|---|---|---|
| `String()` | `Z.String()` | `ZodString` |
| `Number()` | `Z.Number()` | `ZodNumber` |
| `Boolean()` | `Z.Boolean()` | `ZodBoolean` |
| `Null()` | `Z.Null()` | `ZodNull` |
| `Array<T>` | `Z.Array<T>(IZodSchema<T, T> elementSchema)` | `ZodArray<T>` |
| `Optional<T>` | `Z.Optional<T>(IZodSchema<T, T> schema)` — `T : class` | `ZodOptional<T>` |
| `Nullable<T>` | `Z.Nullable<T>(IZodSchema<T, T> schema)` — `T : struct` | `ZodNullable<T>` |
| `Object()` | `Z.Object()` | `ZodObjectBuilder` |
| `Union` (untyped) | `Z.Union(params IZodSchema<object, object>[] options)` | `ZodUnion` |
| `Union<T1,T2>` (typed) | `Z.Union<T1, T2>(IZodSchema<T1, T1>, IZodSchema<T2, T2>)` | `ZodTypedUnion<T1, T2>` |
| `Intersection<T>` | `Z.Intersection<T>(IZodSchema<T, T> left, IZodSchema<T, T> right)` | `ZodIntersection<T>` |
| `Literal<T>` | `Z.Literal<T>(T value)` — `T : IEquatable<T>` | `ZodLiteral<T>` |
| `Lazy<T>` | `Z.Lazy<T>(Func<IZodSchema<T, T>> schemaGetter)` | `ZodLazy<T>` |
| `DiscriminatedUnion` | `Z.DiscriminatedUnion(string discriminator)` | `ZodDiscriminatedUnionBuilder` |
| `Enum` (string values) | `Z.Enum(params string[] values)` | `ZodEnum` |
| `Enum<TEnum>` (native) | `Z.Enum<TEnum>()` — `TEnum : struct, Enum` | `ZodNativeEnum<TEnum>` |
| `Record<TValue>` | `Z.Record<TValue>(IZodSchema<TValue, TValue> valueSchema)` | `ZodRecord<TValue>` |
| `Tuple<T1,T2>` | `Z.Tuple<T1, T2>(IZodSchema<T1, T1>, IZodSchema<T2, T2>)` | `ZodTuple<T1, T2>` |
| `Tuple<T1,T2,T3>` | `Z.Tuple<T1, T2, T3>(IZodSchema<T1, T1>, IZodSchema<T2, T2>, IZodSchema<T3, T3>)` | `ZodTuple<T1, T2, T3>` |
| `ToJsonSchema<T>` | `Z.ToJsonSchema<T>(IZodSchema<T, T> schema, ToJsonSchemaOptions? options = null)` | `JsonSchemaDefinition` |

> [!NOTE]
> `Enum` and `Union` are overloaded: `Enum(params string[])` vs `Enum<TEnum>()`, and `Union(params ...)` vs `Union<T1, T2>(...)`. `ToJsonSchema` lives in the core package; `FromJsonSchema` is an extension on `Z` provided by the JSON integration packages.

## Schema type reference

Each schema type has its own page:

- [String Validation](String-Validation.md) — `ZodString`.
- [Number Validation](Number-Validation.md) — `ZodNumber`.
- [Object Validation](Object-Validation.md) — `ZodObject`, `ZodObjectBuilder`.
- [Arrays and Other Schemas](Arrays-and-Other-Schemas.md) — `ZodArray`, `ZodBoolean`, `ZodNull`, `ZodEnum`, `ZodNativeEnum`, `ZodLiteral`, `ZodRecord`, `ZodTuple`, `ZodLazy`.
- [Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md) — `ZodUnion`, `ZodTypedUnion`, `ZodDiscriminatedUnion`.
- [Composition and Transforms](Composition-and-Transforms.md) — `ZodTransform`, `ZodRefinement`, `ZodSuperRefinement`, `ZodPipe`, `ZodCatch`, `ZodDefault`, `ZodPrefault`, `ZodIntersection`.
- [Compiled Validators and Caching](Compiled-Validators-and-Caching.md) — `CompiledValidator`, `SchemaCache`.

## The interfaces

- `IZodSchema<TOutput, TInput>` — `Validate` / `ValidateAsync`; `IZodSchema<T>` is the convenience form where input equals output.
- `IZodSchemaValidator` (marker) and `IZodSchemaValidator<T>` — the DI-facing adapter surface (see [Dependency Injection](Dependency-Injection.md)).
- `IValidationRule<T>` — the rule contract implemented by every struct rule.

## Convenience composition on any schema

Because composition is implemented on the base `ZodType`, every schema can chain `.Describe(...)`, `.Transform(...)`, `.Refine(...)`, `.SuperRefine(...)`, `.Pipe(...)`, `.Catch(...)`, `.Prefault(...)`, `.Default(...)`, `.And(...)`, and `.Or(...)`. See [Composition and Transforms](Composition-and-Transforms.md).
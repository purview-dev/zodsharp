# Fluent Schema API

## The `Z` factory

`public static class Z` (namespace `ZodSharp`) is the entry point for creating schemas.

| Method | Signature | Returns |
|---|---|---|
| `String()` | `Z.String()` | `ZodString` |
| `Number()` | `Z.Number()` | `ZodNumber` |
| `Boolean()` | `Z.Boolean()` | `ZodBoolean` |
| `Null()` | `Z.Null()` | `ZodNull` |
| `Date()` | `Z.Date()` | `ZodDate` |
| `DateTimeOffset()` | `Z.DateTimeOffset()` | `ZodDateTimeOffset` |
| `BigInt()` | `Z.BigInt()` | `ZodBigInt` |
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
- `IValidationRule<T>` — the rule contract implemented by every struct rule. `ZodType<TOutput, TInput>.AddRule(rule)` and `Rule<TRule>(rule)` are public, so custom rules can be attached to any schema, and a fluent extension method can give them a first-class method like the built-in rules. Every rule exposes its reported code and message template as public `const string ErrorCode` / `MessageFormat` constants (enforced by `ZODSGEN042`). `ZodString` also exposes `IsValidSpan`/`ValidateSpan` for span-based string validation, and string rules that implement `IStringValidationRule` participate in the span path.
- `IZodRule` — implemented by rules that own their error identity (`Code`/`Origin`). When a mapped rule implements it, the generator prefers the rule's values over the attribute's, so one attribute can produce a per-member error code.
- `IStringValidationRule` — the span-based counterpart of `IValidationRule<string>`.

The shipped `ZodSharp.Rules` namespace provides the built-in rules (for example `EmailRule`, `MinLengthRule`, `NonSentinelRule<T>`); each is a `readonly record struct` usable standalone or through the fluent API. Every rule has a fluent method on its schema — see [Validation Rules Reference](Validation-Rules-Reference.md) for the catalogue.

See [Custom Rules](Custom-Rules.md) for defining, attaching, and mapping rules (including generic rules, the `ErrorCode`/`MessageFormat` constants convention, and [fluent extension methods](Custom-Rules.md#extending-the-fluent-interface)).

## Convenience composition on any schema

Because composition is implemented on the base `ZodType`, every schema can chain `.Describe(...)`, `.Transform(...)`, `.Refine(...)`, `.SuperRefine(...)`, `.Pipe(...)`, `.Catch(...)`, `.Prefault(...)`, `.Default(...)`, `.And(...)`, and `.Or(...)`. See [Composition and Transforms](Composition-and-Transforms.md).
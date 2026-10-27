# Arrays and Other Schemas

## Arrays

`ZodArray<T>` (namespace `ZodSharp.Schemas`) validates `T[]` values. Each element is validated against the element schema; failures are re-pathed under a typed index segment, keeping every field of the element's error, so `FullPath.ToDottedString()` renders `[1]` (or `items[1]` inside an object) and the legacy `Path` is `["1"]` (it used to be `["[1]"]`). A `null` array reports `"Expected array, received null"`. A rebuilt array is produced only when a transform changed an element.

```csharp
using ZodSharp;

var numbers = Z.Array(Z.Number()).Min(1).Max(10);
var result = numbers.Validate(new[] { 1.0, 2.0, 3.0 });
```

| Method | Behaviour |
|---|---|
| `Min(int minLength, string? message)` | `too_small` when count < min: `"Array must have at least 2 elements, but got 1"` |
| `Max(int maxLength, string? message)` | `too_big` when count > max: `"Array must have at most 2 elements, but got 3"` |
| `Length(int length, string? message)` | exact length (both bounds): `"Array must have exactly 2 elements, but got 3"` |
| `NonEmpty(string? message)` | `minLength = 1` |

Size failures carry origin `array`, both configured bounds as `Lower`/`Upper` (inclusive) and the element count in
the `count` parameter. `Min`, `Max` and `Length` keep separate custom messages, so `Min(2, "few").Max(5, "many")`
reports the message of the bound that failed.

## Boolean

`ZodBoolean` validates `bool`; there are no fluent methods.

```csharp
var schema = Z.Boolean();
```

## Null

`ZodNull` succeeds only for `null` input, and implements `IAcceptsNull` so it can serve as an object field accepting `null`.

```csharp
var schema = Z.Null();
```

## Date

`ZodDate` validates `DateTime` values, with optional inclusive `.Min`/`.Max` bounds. Equivalent to Zod's `z.date()`.

```csharp
var schema = Z.Date().Min(new DateTime(2020, 1, 1)).Max(new DateTime(2030, 12, 31));
```

## BigInt

`ZodBigInt` validates 64-bit integers (`long`), with `.Min`, `.Max`, `.Gt`, `.Gte`, `.Lt`, `.Lte`, `.Positive`,
`.Negative`, `.NonNegative`, and `.NonPositive`. Equivalent to Zod's `z.bigint()`.

```csharp
var schema = Z.BigInt().Positive().Max(9_000_000_000);
```

## Enum (string values)

`ZodEnum` validates against a set of allowed strings. Failure produces `invalid_enum_value` (category `invalid_value`, origin `enum`) with the message `"Expected one of: admin, guest, user"` (options in ordinal order) and the `options` parameter; the received value is not echoed.

```csharp
var schema = Z.Enum("admin", "user", "guest");
```

## Native enum

`ZodNativeEnum<TEnum>` validates a native `System.Enum` using `Enum.IsDefined`. Failure produces `invalid_enum_value`, reported exactly as `EnumRule` reports it, with the options listed in ordinal order.

```csharp
var schema = Z.Enum<Color>(); // Color : struct, Enum
```

## Literal

`ZodLiteral<T>` (where `T : IEquatable<T>`) accepts exactly one value. Failure produces `invalid_literal` (category `invalid_value`) with the message `"Expected literal active"` and the literal in the `expected` parameter; the received value is not echoed.

```csharp
var schema = Z.Literal("active");
var schema2 = Z.Literal(42);
```

## Record

`ZodRecord<TValue>` validates `Dictionary<string, TValue>`, validating every value against the value schema. Errors are re-pathed under a typed key segment (rendered with the invariant culture): `[en]`, or `["0"]`/`["a.b"]` when the key would otherwise be ambiguous. It always produces a fresh validated dictionary.

```csharp
var schema = Z.Record(Z.Number());
```

A null value is the value schema's decision: `Z.Record(Z.String())` rejects it, while
`Z.Record(Z.Optional(Z.String()))` accepts it. The schema exports and imports as
`{ "type": "object", "additionalProperties": … }` (see
[JSON Schema Export](JsonSchema-Export.md#dictionaries)), and a generated `Dictionary<TKey, TValue>`
property is validated the same way.

## Tuple

`ZodTuple` validates fixed-length tuples. Two- and three-element overloads exist; input is `object?[]`.

```csharp
var schema = Z.Tuple(Z.String(), Z.Number());

var result = schema.Validate(new object?[] { "John", 30.0 });
// (string, double) success value
```

Failures:

- `null` input → `invalid_type` (`"Expected tuple, received null"`).
- Wrong length → `invalid_tuple_length` with category `too_small`/`too_big`, origin `array` and both bounds equal to the arity (`"Expected tuple of length 2, but got 3"`).
- Element failures are prefixed with the element's index (`[0]`; legacy `Path` `["0"]`). A type mismatch reads `"Expected string, received number"`. They used to be reported at the tuple root.
- A `null` element is handed to the element schema when its type can hold `null`, so `Z.Optional(...)` elements accept it.

## Lazy

`ZodLazy<T>` defers schema construction to first use, enabling recursive and circular schemas. The inner `Schema` is resolved lazily and thread-safely.

```csharp
var categorySchema = Z.Lazy<Dictionary<string, object?>>(() =>
    Z.Object()
        .Field("name", Z.String())
        .Field("subcategories", Z.Array(categorySchema))
        .Build());
```

## Optional / Nullable

`Z.Optional<T>(schema)` (`T : class`) accepts `null` or a value matching the inner schema. `Z.Nullable<T>(schema)` (`T : struct`) is the value-type counterpart.

```csharp
var optional = Z.Optional(Z.String());
optional.Validate(null);      // Success
optional.Validate("value");   // Success
```

For a generated collection, element nullability follows the element type: `T[]`/`List<T>` rejects `null` elements, while `T?[]`/`List<T?>` accepts them — the same distinction as `z.array(T)` versus `z.array(T.nullable())`. A dictionary property (`IDictionary<,>`/`IReadOnlyDictionary<,>`) is a map instead: values are validated like elements, and it maps to `z.record(z.string(), V)`. See [Collection null items](Source-Generator.md#collection-null-items).
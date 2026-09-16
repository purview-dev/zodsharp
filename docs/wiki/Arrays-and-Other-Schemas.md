# Arrays and Other Schemas

## Arrays

`ZodArray<T>` (namespace `ZodSharp.Schemas`) validates `T[]` values. Each element is validated against the element schema; failures carry index paths such as `["0"]`. A rebuilt array is produced only when a transform changed an element.

```csharp
using ZodSharp;

var numbers = Z.Array(Z.Number()).Min(1).Max(10);
var result = numbers.Validate(new[] { 1.0, 2.0, 3.0 });
```

| Method | Behaviour |
|---|---|
| `Min(int minLength, string? message)` | `too_small` when count < min |
| `Max(int maxLength, string? message)` | `too_big` when count > max |
| `Length(int length, string? message)` | exact length (both bounds) |
| `NonEmpty(string? message)` | `minLength = 1` |

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

## Enum (string values)

`ZodEnum` validates against a set of allowed strings. Failure produces `invalid_enum_value`.

```csharp
var schema = Z.Enum("admin", "user", "guest");
```

## Native enum

`ZodNativeEnum<TEnum>` validates a native `System.Enum` using `Enum.IsDefined`. Failure produces `invalid_enum_value`.

```csharp
var schema = Z.Enum<Color>(); // Color : struct, Enum
```

## Literal

`ZodLiteral<T>` (where `T : IEquatable<T>`) accepts exactly one value. Failure produces `invalid_literal`.

```csharp
var schema = Z.Literal("active");
var schema2 = Z.Literal(42);
```

## Record

`ZodRecord<TValue>` validates `Dictionary<string, TValue>`, validating every value against the value schema with key-prefixed error paths. It always produces a fresh validated dictionary.

```csharp
var schema = Z.Record(Z.Number());
```

## Tuple

`ZodTuple` validates fixed-length tuples. Two- and three-element overloads exist; input is `object?[]`.

```csharp
var schema = Z.Tuple(Z.String(), Z.Number());

var result = schema.Validate(new object?[] { "John", 30.0 });
// (string, double) success value
```

Failures: `invalid_type` on `null`, `invalid_tuple_length` on wrong length, and per-index `invalid_type` with paths like `["[0]"]`.

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
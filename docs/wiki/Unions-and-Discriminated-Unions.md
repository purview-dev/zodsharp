# Unions and Discriminated Unions

## Untyped union

`ZodUnion` (namespace `ZodSharp.Schemas`) tries each option in order and returns the first success.

```csharp
using ZodSharp;

var schema = Z.Union(Z.String(), Z.Number(), Z.Boolean());
var result = schema.Validate(42.0); // matches Z.Number()
```

When no option matches, a single `invalid_union` error is produced (`"Value does not match any of the union options"`) whose `Parameters["errors"]` carries every option's errors.

> [!NOTE]
> On the failure path the accumulated option errors are collected, which allocates. Matching a later option (e.g. number or boolean after a string option) also allocates while the earlier options are attempted — see [Performance](Performance.md).

## Typed union

`ZodTypedUnion<T1, T2>` dispatches on runtime type (`is T1` / `is T2`) and produces a `Union<T1, T2>` result value.

```csharp
var schema = Z.Union(Z.String(), Z.Number());
var result = schema.Validate(42.0);

if (result.IsSuccess)
    result.Value.Match(
        str => Console.WriteLine($"string: {str}"),
        num => Console.WriteLine($"number: {num}"));
```

## Union<T1, T2> and Union<T1, T2, T3>

The `Union<...>` value type (namespace `ZodSharp.Unions`) is the result of a typed union:

- `Create(T1)` / `Create(T2)` (and a three-case variant) — tagged construction.
- Implicit conversions from the case types.
- `int Tag` and `object Value` (`Value` throws if uninitialized).
- `TryGetValue(out T1)` / `TryGetValue(out T2)`.
- `Match(Func<T1, TResult>, Func<T2, TResult>)` and `Switch(Action<T1>, Action<T2>)`.
- `==` / `!=`, `Equals`, `GetHashCode`, `ToString`.

## Discriminated union

`ZodDiscriminatedUnion` dispatches on a discriminator value read from the input — a dictionary key or a public instance property — resolved case-insensitively.

```csharp
var union = Z.DiscriminatedUnion("type")
    .Option("user", userSchema)
    .Option("admin", adminSchema)
    .Build();

var result = union.Validate(new Dictionary<string, object?>
{
    { "type", "user" },
    { "name", "John" }
});
```

Failures:

- No discriminator present → `missing_discriminator`.
- Value not among the options → `invalid_discriminator` listing the expected values.
- `null` input → `invalid_type`.

The builder (`ZodDiscriminatedUnionBuilder`) accepts untyped `IZodSchema<object, object>` options via `Option(string value, IZodSchema<object, object> schema)` and typed options via `Option<T>(string value, IZodSchema<T, T> schema)`, which wrap the schema for coercion and `null` handling.

## Intersection

`ZodIntersection<T>` (created with `Z.Intersection<T>(left, right)` or `.And(other)`) succeeds only when both schemas validate; failures merge both error sets.

```csharp
var schema = Z.String().Min(3).And(Z.String().Max(10));
```
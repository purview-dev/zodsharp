# Unions and Discriminated Unions

## Untyped union

`ZodUnion` (namespace `ZodSharp.Schemas`) tries each option in order and returns the first success.

```csharp
using ZodSharp;

var schema = Z.Union(Z.String(), Z.Number(), Z.Boolean());
var result = schema.Validate(42.0); // matches Z.Number()
```

When no option matches, a single `invalid_union` error (category `invalid_union`) is reported at the union's path. Its message names the options with Zod type names (`"Value does not match any of the union options (string, number)"`), the `options` parameter lists them, and the per-option issues - Zod's `unionErrors` - are available through `GetUnionErrors()`:

```csharp
var error = Z.Union(Z.String().Min(5), Z.Number()).Validate("ab").Errors[0];

// error.Code == ZodIssueCodes.InvalidUnion
foreach (ImmutableArray<ValidationError> option in error.GetUnionErrors())
{
    // option 0: too_small (string must be at least 5 characters)
    // option 1: invalid_type ("Expected number, received string")
}
```

`GetUnionErrors()` (on `ZodIssues`) returns an `ImmutableArray<ImmutableArray<ValidationError>>`, one entry per
option in declaration order, with paths relative to the union. It is stored under the `unionErrors` parameter
(`ZodIssueParameters.UnionErrors`); the old mutable list under `errors`, which collided with the RFC 7807
`errors` member, is gone. Typed and native unions report the same shape (they used to discard the inner
errors). ProblemDetails projects the union error at its own path, with `unionErrors` as nested issues.

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

## Native unions (.NET 11+)

When targeting `net11.0` or later, `Z.NativeUnion<T1, T2>` returns a native C# 15 union
(`ZodSharp.Unions.NativeUnion<T1, T2>`) instead of `Union<T1, T2>`:

```csharp
var schema = Z.NativeUnion(Z.String().Min(1), Z.Object().Build());
var result = schema.Validate("hello");

if (result.IsSuccess)
{
    var length = result.Value switch
    {
        string s => s.Length,
        Dictionary<string, object?> o => o.Count,
        _ => -1,
    };
}
```

Prefer this when **both option types are reference types**: the native union is allocation-free and
supports exhaustive pattern matching. Value-type cases box, so keep using `Z.Union<T1, T2>` (the
hand-rolled `Union<T1, T2>`) when a case is a value type, or when you need
`Tag`/`Match`/`Switch`/`TryGetValue`/equality. The analyzer reports `ZODSGEN041` (Info) and offers a
code fix when a reference-type-only `Z.Union` can be switched to `Z.NativeUnion`.

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

Failures are reported at the discriminator's path, with category `invalid_union` and the `discriminator` and
`options` parameters (options in declaration order). The received discriminator value is not echoed.

| Failure | Code | Message |
|---|---|---|
| No discriminator present | `missing_discriminator` | `Discriminator field 'type' not found` |
| Value not among the options | `invalid_discriminator` | `Invalid discriminator value. Expected one of: user, admin` |
| `null` input | `invalid_type` | `Expected object, received null` (at the root) |

Once an option is selected, its own errors are reported unchanged (for example `invalid_type` at `level`).
Discriminated unions export to JSON Schema as `oneOf` (see [JSON Schema Export](JsonSchema-Export.md)).

The builder (`ZodDiscriminatedUnionBuilder`) accepts untyped `IZodSchema<object, object>` options via `Option(string value, IZodSchema<object, object> schema)` and typed options via `Option<T>(string value, IZodSchema<T, T> schema)`, which wrap the schema for coercion and `null` handling.

## Unions in generated validators and hosts

The source generator validates a member whose type is a C# 15 union (`public union Pet(Cat, Dog);`) by
dispatching to the active case's schema, and a member typed as a C# 15 `closed` class by dispatching to the
derived type's schema. Paths do not include the union's `Value`: an error in the `Cat` case of a member
`public Pet Pet { get; set; }` is reported at `Pet.Name`, not `Pet.Value.Name`. Every derived type of a closed hierarchy needs its own
`[ZodSchema]`; one without is reported as `ZODSGEN049` (see
[Source Generator Diagnostics](Source-Generator-Diagnostics.md)).

On .NET 11 the hosts understand unions too:

- **Minimal APIs.** A JSON-body parameter typed as a union is validated through its active case, both by the
  Microsoft.Extensions.Validation pipeline (`AddZodSharpValidation()` claims union parameters without an
  application `AddValidation()` call) and by `WithZodSharpValidation()`. See
  [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md) and
  [ASP.NET Core Integration](AspNetCore-Integration.md).
- **Blazor.** A form model property of union type resolves its field through the case object, so messages
  land next to the right input. See [Blazor Integration](Blazor-Integration.md).
- **OpenAPI.** A C# union keeps the platform's `anyOf` shape and gets each case constrained; polymorphic
  (`[JsonPolymorphic]`) types are described with `oneOf` and a `discriminator`. See
  [OpenAPI Integration](OpenAPI-Integration.md).

## Intersection

`ZodIntersection<T>` (created with `Z.Intersection<T>(left, right)` or `.And(other)`) succeeds only when both schemas validate; failures merge both error sets, and an issue raised identically by both sides is reported once.

```csharp
var schema = Z.String().Min(3).And(Z.String().Max(10));
```
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
> On the failure path the accumulated option errors are collected, which allocates. Matching a later option (e.g. number or boolean after a string option) also allocates while the earlier options are attempted — see [Performance](../quality/Performance.md).

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
- Implicit conversions from the case types (a `null` reference produces an empty union).
- `int Tag` (`0` empty, `1` `T1`, `2` `T2`, and `3` for the three-case `T3`) and `object? Value` (`null` when empty).
- `TryGetValue(out T1)` / `TryGetValue(out T2)`.
- `Match(Func<T1, TResult>, Func<T2, TResult>)` and `Switch(Action<T1>, Action<T2>)` (they throw on an empty union).
- `==` / `!=`, `Equals`, `GetHashCode`, `ToString`.

## Native unions (.NET 11+)

When targeting `net11.0` or later, `Z.NativeUnion<T1, T2>` returns a native C# 15 union
(`ZodSharp.Unions.NativeUnion<T1, T2>`) instead of `Union<T1, T2>`:

```csharp
var schema = Z.NativeUnion(Z.Number(), Z.Boolean());
var result = schema.Validate(3d);

if (result.IsSuccess)
{
    var described = result.Value switch
    {
        double n => n,
        bool b => b ? 1d : 0d,
        // No discard arm: both case types are handled, so the switch is exhaustive.
    };
}
```

The type is declared by hand with `[Union]` rather than with the `union` keyword, so it implements the
language's **non-boxing access pattern**. Each case is stored in its own typed field, and `TryGetValue(out T1)`
/ `TryGetValue(out T2)` / `HasValue` let pattern matching read a value-type case without boxing; the compiler
still treats it as a union (implicit conversions from each case type, and exhaustive `switch` expressions).
`Value` is only the boxing view the union contract requires, and `Tag` identifies the active case.

Use it whenever you need a two-option union on .NET 11+: reference-type and value-type cases are both
allocation-free. Use the hand-rolled `Union<T1, T2>` on .NET 10, or when you need `Match`/`Switch`/equality.
The analyzer reports `ZODSGEN041` (Info) and offers a code fix for any `Z.Union<T1, T2>` built on .NET 11+.

## Unions as schema targets

A union type can itself carry `[ZodSchema]`, for either the `union` keyword or a hand-written `[Union]` struct:

```csharp
[ZodSchema]
public readonly union Pet(Cat, Dog);
```

`PetSchema.Validate(pet)` dispatches on the union's active case and validates it with that case type's schema, so the
error paths are the case's own (`Name`, `Weight`) without a synthetic `Value` segment. Both forms of the language's
access pattern are understood:

- A case with `TryGetValue(out TCase)` (the non-boxing access pattern: `NativeUnion`, hand-written unions) is matched
  without boxing.
- A case without it (a `union` declaration) is matched through `Value`.

An empty union, or a case type with no validator, fails as `invalid_union` with one `invalid_type` per option instead
of passing silently. A case type with no validator is also reported at build time as **ZODSGEN055** (warning): add
`[ZodSchema]` to that case, or reference the assembly that generated its schema.

A union member of a class target works the same way and needs no `[ZodSchema]` on the union: the member dispatches
directly to the case schemas. `[Required]`, `[RequiredZod]` and `[NonSentinel]` treat an empty union as the member's
absent/default value — `missing_field`, the rule's own error, or `invalid_type` respectively — using `HasValue` when the
union provides it and `Value` otherwise. An empty union is never silently accepted.

Async rules work end to end: a union whose active case has an `AsyncValidationAttribute` (or an
`IAsyncValidatableObject`) reports `RequiresAsyncValidation = true`, `ValidateAsync` awaits the case schema,
`Validate` throws, and the ASP.NET Core pipeline, Blazor forms, Microsoft.Extensions.Validation and options
validation all await it through the same generated entry points.

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
Discriminated unions export to JSON Schema as `oneOf` (see [JSON Schema Export](../serialization/JsonSchema-Export.md)).

The builder (`ZodDiscriminatedUnionBuilder`) accepts untyped `IZodSchema<object, object>` options via `Option(string value, IZodSchema<object, object> schema)` and typed options via `Option<T>(string value, IZodSchema<T, T> schema)`, which wrap the schema for coercion and `null` handling.

## Unions in generated validators and hosts

The source generator validates a member whose type is a C# 15 union (`public union Pet(Cat, Dog);`) by
dispatching to the active case's schema, and a member typed as a C# 15 `closed` class by dispatching to the
derived type's schema. Paths do not include the union's `Value`: an error in the `Cat` case of a member
`public Pet Pet { get; set; }` is reported at `Pet.Name`, not `Pet.Value.Name`. Every derived type of a closed hierarchy needs its own
`[ZodSchema]`; one without is reported as `ZODSGEN049` (see
[Source Generator Diagnostics](../generator/Source-Generator-Diagnostics.md)).

On .NET 11 the hosts understand unions too:

- **Minimal APIs.** A JSON-body parameter typed as a union is validated through its active case, both by the
  Microsoft.Extensions.Validation pipeline (`AddZodSharpValidation()` claims union parameters without an
  application `AddValidation()` call) and by `WithZodSharpValidation()`. See
  [Microsoft.Extensions.Validation](../hosting/Microsoft-Extensions-Validation.md) and
  [ASP.NET Core Integration](../AspNetCore-Integration.md).
- **Blazor.** A form model property of union type resolves its field through the case object, so messages
  land next to the right input. See [Blazor Integration](../hosting/Blazor-Integration.md).
- **OpenAPI.** A C# union keeps the platform's `anyOf` shape and gets each case constrained; polymorphic
  (`[JsonPolymorphic]`) types are described with `oneOf` and a `discriminator`. See
  [OpenAPI Integration](../hosting/OpenAPI-Integration.md).

## Intersection

`ZodIntersection<T>` (created with `Z.Intersection<T>(left, right)` or `.And(other)`) succeeds only when both schemas validate; failures merge both error sets, and an issue raised identically by both sides is reported once.

```csharp
var schema = Z.String().Min(3).And(Z.String().Max(10));
```
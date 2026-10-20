# Guarantees and Limitations

## Guarantees

- **Zero-allocation on valid inputs for the core schema types.** Primitives, strings, arrays, and objects (and their generated `[ZodSchema]` validators) validate valid inputs without allocating. See [Performance](Performance.md) for the measurements. The exceptions are listed under [Limitations](#limitations).
- **No reflection on hot paths.** The runtime library uses expression trees only in the opt-in `CompiledValidator` and to compile a one-off discriminator accessor per (type, discriminator) pair for `ZodDiscriminatedUnion`. After that first use, validation runs direct property access; the source generator emits direct typed codegen.
- **Deterministic, reviewable generated code.** The `[ZodSchema]` generator output is stable and de-duplicated; there are no scope leaks in emitted code.
- **Cross-platform parity.** The C# implementation is exercised against TypeScript/Zod fixtures (see [Cross-Platform Interop](Cross-Platform-Interop.md)). Array item nullability lines up with Zod: a generated `T[]` rejects null elements like `z.array(T)`, and a nullable element exports the same `items.anyOf` null branch that `z.array(T.nullable())` does.
- **Multi-targeting.** Packages target `net8.0`, `net9.0`, `net10.0`, and `net11.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.
- **A fully built schema is safe to cache and share across threads.** Validation only reads the rule set and `Description`, so once construction is finished a schema can be reused concurrently. Building is *not* immutable — see the next section.

## Limitations

### Fluent rule methods mutate the receiver

`Min`, `Max`, `Email`, `Regex`, `UUID`, `StartsWith`, `EndsWith`, `Describe`, and the other fluent rule/description methods append to the receiver's rule list (or set its description) **in place** and return `this`. Only the composition methods (`Transform`, `Refine`, `SuperRefine`, `Pipe`, `Catch`, `Default`, `Prefault`, `And`, `Or`) return a new schema. Finish configuring a schema before sharing it; two pieces of code holding the same instance share its rules.

### Span validation returns a string

`ZodString.ValidateSpan(ReadOnlySpan<char>)` validates the span directly whenever every accumulated rule implements `IStringValidationRule`, but its result carries a `string`, so a **successful** validation still allocates once when the value is materialised. Use `ZodString.IsValidSpan(ReadOnlySpan<char>, out ImmutableArray<ValidationError>)` when the value is not needed: it is allocation-free on success and only materialises the input when a rule (or a transform) cannot be evaluated over a span. `ValidateSpan` falls back to the string pipeline for schemas that involve string transforms (`Trim`, `ToLower`, `ToUpper`) or rules without a span implementation.

### Rules without a span implementation

`UrlRule` (its `Uri.TryCreate` fallback needs a string) and `Base64StringRule` (`Convert.FromBase64String`) do not implement `IStringValidationRule`; adding either to a `ZodString` disables the span fast path for that schema, and `ValidateSpan`/`IsValidSpan` fall back to materialising the input once.

### Size-failure Origin values

The generator reports `Origin = "string"` for string size failures, `Origin = "array"` for arrays, and `Origin = "collection"` for countable/`IEnumerable` collections. `ZodArray` reports `"array"`.

### Typed union allocation

`ZodTypedUnion`/`ZodUnion` allocate while attempting non-matching options and on failure; discriminated unions backed by dictionaries dispatch directly and stay zero-allocation.

### Rule errors

Rules evaluated by the base `Validate` pipeline emit Zod-compatible codes: `too_small`/`too_big` for bounds, `not_multiple_of`/`not_finite` for numbers, `invalid_string` for string-format validations, `invalid_type` for `IntRule<T>`, and `invalid_value` for `NonSentinelRule<T>`. Custom rules that do not declare a code default to `validation_failed`. Every rule exposes its correlated code and message template as public `const string ErrorCode` / `MessageFormat` constants so tests can assert against the rule rather than duplicating literals; a source-declared rule that omits them is reported as `ZODSGEN042`. Structured `too_small`/`too_big` issues (with `Origin`, `Minimum`/`Maximum`, and `Inclusive`) are produced by `ZodArray` and by the source generator's size validators.

### String transforms allocate

`ToLower`, `ToUpper`, and `Trim` produce new strings on every validation (transform outputs are new strings by nature).

### Number semantics

`ZodNumber` operates on `double`. `Int()`, `Safe()`, and `Finite()` are validation rules, not conversions; `.Int()` rejects fractional values rather than rounding them. `Positive()`/`Negative()` are strict (they reject `0`; use `NonNegative()`/`NonPositive()` for inclusive bounds). `MultipleOf` compares the distance to the nearest multiple against a relative tolerance (`1e-12`), so `0.3` is accepted for `MultipleOf(0.1)` while `0.3000000001` is not; NaN and infinity are rejected, and a zero divisor throws `ArgumentException`.

### Enum semantics

`ZodEnum` (string values) and `ZodNativeEnum<TEnum>` validate against defined members; they do not parse or convert values.

### Temporal sentinel export

`[NonSentinel]` exports exact `pattern` constraints for `DateOnly`, `TimeOnly`, the deterministic `DateTime`
wire forms (`Unspecified`, `Utc`, or a zero offset), and `DateTimeOffset`. The `DateTimeOffset` pattern is
offset-aware: it correlates the local time with the offset and rejects every RFC 3339 representation of the
instant bounds, including offset-equivalent forms, without an `x-zodsharp-unsupported` marker.

A `DateTimeKind.Local` value can carry an arbitrary machine offset, and System.Text.Json converts an
offset-bearing `DateTime` into the receiver's time zone, so that form is not portable; use `Unspecified`,
`Utc`, or `DateTimeOffset` (whose sentinel pattern is exact) for a cross-platform contract.
`DateTimeOffsetSerialization.Utc` is an optional narrower contract that requires a zero offset and a
converter that rejects non-UTC input on read; see
[JSON Schema Export](JsonSchema-Export.md#temporal-sentinels-and-the-datetimeoffset-wire-form).

`TimeOnly` is exported as a local-time `pattern` instead of `format: time`. JSON Schema's `format: time` is an
RFC 3339 full-time and requires an offset, while System.Text.Json writes `TimeOnly` as a local
`HH:mm:ss[.fffffff]`; a strict `format: time` consumer would reject every value.

### JSON Schema import scope

`Z.FromJsonSchema` supports **local** `$ref` (`#/...`) references only; external `$ref` targets throw a `NotSupportedException` that names the unsupported reference. The integration packages' `JsonSchemaSerializerOptions` read and write the JSON Schema keyword names (`$schema`, `$id`, `$ref`, `$defs`), so exported definitions round-trip; the core `JsonSchemaDefinition` type itself stays free of serializer annotations. Import types live in package-specific namespaces (`ZodSharp.JsonSchema.SystemTextJson` / `ZodSharp.JsonSchema.NewtonsoftJson`).

### Referencing both JSON integration packages

`Purview.ZodSharp.SystemTextJson` and `Purview.ZodSharp.NewtonsoftJson` are **mutually exclusive** integrations — pick the one that matches your JSON library. Both packages can be referenced from the same project **without `extern alias`**, because every JSON Schema import type is declared in a package-specific namespace (`ZodSharp.JsonSchema.SystemTextJson`, `ZodSharp.JsonSchema.NewtonsoftJson`) rather than an identical full name. The deserialize/serialize extension methods remain in the `ZodSharp` namespace in both packages, so import exactly one package namespace per file: importing both makes calls such as `schema.DeserializeAndValidate(json)` or `Z.FromJsonSchema(...)` ambiguous at the call site.

## Custom rules

Custom rules and their DataAnnotations-style attributes are a first-class extension point. Rules can be attached to a property or to the schema type itself (validating the value object as a unit), and a generic rule can be closed with the target type so one rule serves every scalar of a given shape. A rule written against a `Purview.ValueObjects` scalar's underlying value is adapted automatically when it is applied to a `[Scalar]` type, so one rule also serves every scalar backed by the same primitive. See [Custom Rules](Custom-Rules.md) for the rule contract, the public `AddRule`/`Rule` API, and how to map a rule to a `ValidationAttribute` that the source generator honours, and [Value Objects Integration](Value-Objects-Integration.md) for the `[Scalar]` walkthrough and error code / message definitions.

## Contract vs. underlying libraries

- `System.ComponentModel.DataAnnotations` semantics are honoured where documented — e.g. `[Length]` treats `null` as valid unless `[Required]` is present; `[RegularExpression]` runs only on non-empty strings.
- Validation failure payloads map to `ProblemDetails`/`HttpValidationProblemDetails` in the ASP.NET Core package (see [ASP.NET Core Integration](AspNetCore-Integration.md)).
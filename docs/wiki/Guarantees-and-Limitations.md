# Guarantees and Limitations

## Guarantees

- **Zero-allocation on valid inputs for the core schema types.** Primitives, strings, arrays, and objects (and their generated `[ZodSchema]` validators) validate valid inputs without allocating. See [Performance](Performance.md) for the measurements. The exceptions are listed under [Limitations](#limitations).
- **No reflection on hot paths.** The runtime library uses expression trees only in the opt-in `CompiledValidator` and to compile a one-off discriminator accessor per (type, discriminator) pair for `ZodDiscriminatedUnion`. After that first use, validation runs direct property access; the source generator emits direct typed codegen.
- **Deterministic, reviewable generated code.** The `[ZodSchema]` generator output is stable and de-duplicated; there are no scope leaks in emitted code.
- **Cross-platform parity.** The C# implementation is exercised against TypeScript/Zod fixtures (see [Cross-Platform Interop](Cross-Platform-Interop.md)).
- **Multi-targeting.** Packages target `net8.0`, `net9.0`, and `net10.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.
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

Rules evaluated by the base `Validate` pipeline produce `validation_failed` errors with an empty path. Structured `too_small`/`too_big` issues (with `Origin`, `Minimum`/`Maximum`, and `Inclusive`) are produced by `ZodArray` and by the source generator's size validators.

### String transforms allocate

`ToLower`, `ToUpper`, and `Trim` produce new strings on every validation (transform outputs are new strings by nature).

### Number semantics

`ZodNumber` operates on `double`. `Int()`, `Safe()`, and `Finite()` are validation rules, not conversions; `.Int()` rejects fractional values rather than rounding them. `Positive()`/`Negative()` are strict (they reject `0`; use `NonNegative()`/`NonPositive()` for inclusive bounds). `MultipleOf` compares the quotient to its nearest integer with a relative tolerance (`1e-12`), so `0.3` is accepted for `MultipleOf(0.1)` while `0.3000000001` is not; NaN and infinity are rejected, and a zero divisor throws `ArgumentException`.

### Enum semantics

`ZodEnum` (string values) and `ZodNativeEnum<TEnum>` validate against defined members; they do not parse or convert values.

### JSON Schema import scope

`Z.FromJsonSchema` supports **local** `$ref` (`#/...`) references only; external `$ref` targets throw `NotSupportedException`. `FromJsonSchemaOptions` is currently empty (reserved for future options).

### Referencing both JSON integration packages

`Purview.ZodSharp.SystemTextJson` and `Purview.ZodSharp.NewtonsoftJson` both declare types with identical full names (`ZodSharp.ZExtensions`, `ZodSharp.JsonSchema.FromJsonSchemaOptions`, `FromJsonSchemaParser`, `JsonSchemaSerializerOptions`). Reference one JSON integration package; referencing both requires `extern alias`.

## Custom rules

Custom rules and their DataAnnotations-style attributes are a first-class extension point. Rules can be attached to a property or to the schema type itself (validating the value object as a unit), and a generic rule can be closed with the target type so one rule serves every scalar of a given shape. See [Custom Rules](Custom-Rules.md) for the rule contract, the public `AddRule`/`Rule` API, and how to map a rule to a `ValidationAttribute` that the source generator honours.

## Contract vs. underlying libraries

- `System.ComponentModel.DataAnnotations` semantics are honoured where documented — e.g. `[Length]` treats `null` as valid unless `[Required]` is present; `[RegularExpression]` runs only on non-empty strings.
- Validation failure payloads map to `ProblemDetails`/`HttpValidationProblemDetails` in the ASP.NET Core package (see [ASP.NET Core Integration](AspNetCore-Integration.md)).
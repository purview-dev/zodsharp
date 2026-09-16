# Guarantees and Limitations

## Guarantees

- **Zero-allocation on valid inputs.** Primitives, strings, arrays, objects, discriminated unions, and first-option unions validate without allocating when the input is valid. See [Performance](Performance.md).
- **No reflection on hot paths.** The runtime library uses expression trees only in the opt-in `CompiledValidator`; the source generator emits direct typed codegen.
- **Deterministic, reviewable generated code.** The `[ZodSchema]` generator output is stable and de-duplicated; no scope leaks in emitted code.
- **Cross-platform parity.** The C# implementation is exercised against TypeScript/Zod fixtures (see [Cross-Platform Interop](Cross-Platform-Interop.md)).
- **Multi-targeting.** Packages target `net8.0`, `net9.0`, and `net10.0`; the source generator targets `netstandard2.0` so it runs in any compiler host.
- **Immutable, shareable schemas.** Composing returns new schemas; schemas are safe to cache and share across threads.

## Limitations

### Attribute flags that are not yet honoured

`SchemaName`, `GenerateValidateMethod`, and `GenerateParseMethod` on `[ZodSchema]` are parsed but ignored: the schema class is always `{TypeName}Schema` and `Validate`/`Parse` are always emitted. `EnableComposition` and the `IValidateOptions` flags are honoured.

### Generated size-failure Origin

The generator reports `Origin = "array"` for both arrays and collections — there is no `"collection"` origin in generated code, even though `ValidationError.Origin` supports it.

### Async validation is synchronous underneath

`ValidateAsync` wraps the synchronous `Validate` pipeline in a `ValueTask`. The only genuinely async path is the source generator's custom async validation method (`CustomValidationAsync`), which is awaited after the sync validation.

### JSON Schema import scope

`Z.FromJsonSchema` supports **local** `$ref` (`#/...`) references only; external `$ref` targets throw `NotSupportedException`. `FromJsonSchemaOptions` is currently empty (reserved for future options).

### Referencing both JSON integration packages

`Purview.ZodSharp.SystemTextJson` and `Purview.ZodSharp.NewtonsoftJson` both declare types with identical full names (`ZodSharp.ZExtensions`, `ZodSharp.JsonSchema.FromJsonSchemaOptions`, `FromJsonSchemaParser`, `JsonSchemaSerializerOptions`). Reference one JSON integration package; referencing both requires `extern alias`.

### Typed union allocation

`ZodTypedUnion`/`ZodUnion` allocate while attempting non-matching options and on failure. Discriminated unions dispatch directly and stay zero-allocation.

### Rule errors

Rules evaluated by the base `Validate` pipeline produce `validation_failed` errors with an empty path. Structured `too_small`/`too_big` issues (with `Minimum`/`Maximum`/`Inclusive`) come from the array schema and the source generator's size validators.

### String transforms allocate

`ToLower`, `ToUpper`, and `Trim` produce new strings on every validation (transform outputs are new strings by nature).

### `IStringValidationRule`

The span-based `IStringValidationRule` interface is declared but not implemented by any shipped rule struct; span validation is available through `ZodString.ValidateSpan`.

### Number semantics

`ZodNumber` operates on `double`. `Int()`, `Safe()`, and `Finite()` are validation rules, not conversions; `.Int()` rejects fractional values rather than rounding them. `MultipleOf` uses a tolerance-based comparison and rejects a zero divisor.

### Enum semantics

`ZodEnum` (string values) and `ZodNativeEnum<TEnum>` validate against defined members; they do not parse or convert values.

## Contract vs. underlying libraries

- `System.ComponentModel.DataAnnotations` semantics are honoured where documented — e.g. `[Length]` treats `null` as valid unless `[Required]` is present; `[RegularExpression]` runs only on non-empty strings.
- Validation failure payloads map to `ProblemDetails`/`HttpValidationProblemDetails` in the ASP.NET Core package (see [ASP.NET Core Integration](AspNetCore-Integration.md)).
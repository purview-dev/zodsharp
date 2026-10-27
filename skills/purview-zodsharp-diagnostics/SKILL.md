---
name: purview-zodsharp-diagnostics
description: >-
  Use when a Purview.ZodSharp build reports a ZODSGEN or ZODSASP diagnostic - the [ZodSchema]
  generator's configuration and usage rules, the custom-rule mapping rules, async-only rules,
  Microsoft.Extensions.Validation and Razor interplay, error-message placeholders, and the ErrorType
  analyzers - to find what triggered it and how to fix it.
license: MIT
metadata:
  product: Purview.ZodSharp
  repository: purview-dev/zodsharp
---

# Purview.ZodSharp diagnostics

Two families, both enabled by default:

- **`ZODSGEN*`** — the `[ZodSchema]` generator's analyzer (category `ZodSharp.SourceGenerator`).
- **`ZODSASP*`** — the `ErrorType` factory's analyzer and generator, which ship in the **core**
  package and need no ASP.NET Core dependency.

`ZODSGEN033`, `ZODSGEN037`–`ZODSGEN040`, `ZODSGEN042`, `ZODSGEN045`, `ZODSGEN046`, `ZODSGEN048`, `ZODSGEN049`,
`ZODSGEN051` and `ZODSGEN052` are warnings; `ZODSGEN041`, `ZODSGEN043` and `ZODSGEN047` are
informational; the rest are errors.

IDs `ZODSGEN002` and `ZODSGEN022`–`ZODSGEN026` are intentionally unused. Rule identifiers are never
renumbered or reused, so a sighting of one means stale tooling or a stale suppression.

## Generator failures and DataAnnotations usage

| ID | Meaning |
| --- | --- |
| `ZODSGEN001` | Unhandled generator exception. |
| `ZODSGEN003` | `[Length]` with `min > max`. |
| `ZODSGEN004` | `[Length]` on an unsupported target. |
| `ZODSGEN005` | Error-message resource name without type, or type without name. |
| `ZODSGEN006` | Unsupported DataAnnotations usage: a string-only attribute on a non-string, `[AllowedValues]`/`[DeniedValues]` on an unsupported type, `[RegularExpression]` on a non-string, `[Range]` on an unsupported type. |
| `ZODSGEN020` | `[Compare]` references an unknown property. |
| `ZODSGEN021` | `System.ComponentModel.DataAnnotations` reference is missing. |
| `ZODSGEN048` | *(warning)* The type has rules that only run asynchronously (an `AsyncValidationAttribute` member or `IAsyncValidatableObject`), so the generated synchronous `Validate` throws `InvalidOperationException`. Call `ValidateAsync`. |
| `ZODSGEN052` | *(warning)* An attribute's `ErrorMessage` uses a positional placeholder the attribute does not supply (for example `{3}` on `[Required]`) or has an unbalanced brace. Messages are formatted with a never-throw engine, so the text is shown literally; fix the placeholder. |

## Custom async validation method

All of these mean the method the generator was told to call cannot be called:

| ID | Meaning |
| --- | --- |
| `ZODSGEN007` | Configured but not found. |
| `ZODSGEN008` | Return type is not `ValueTask<ValidationResult<T>>`. |
| `ZODSGEN009` | The parameters are neither `(T value, CancellationToken ct)` nor the service-aware `(T value, IServiceProvider services, CancellationToken ct)`. |
| `ZODSGEN010` | First parameter is not the model type. |
| `ZODSGEN011` | The last parameter is not `CancellationToken`. |
| `ZODSGEN012` | The method is generic. |
| `ZODSGEN013` | Declared on the model type but not `static`. |
| `ZODSGEN014` | Inaccessible from the generated validator (private/protected). |
| `ZODSGEN015` | Ambiguous overloads — only when at least two valid candidates exist. |
| `ZODSGEN016` | The configured name is not a valid C# identifier. |
| `ZODSGEN017` | The method is abstract. |
| `ZODSGEN018` | An unimplemented partial method. |
| `ZODSGEN019` | Uses `ref`/`in`/`out`/`params`/`scoped` parameters. |

## `IValidateOptions`

| ID | Meaning |
| --- | --- |
| `ZODSGEN027` | Requested, but the `Microsoft.Extensions.Options` reference is missing. |
| `ZODSGEN028` | Requested on a struct — it requires a class. |
| `ZODSGEN050` | *(error)* The generated `IValidateOptions` validator would conflict with a hand-authored `[OptionsValidator]` class (`SYSLIB1205`), so it is not generated. Remove `[OptionsValidator]` or set `[ZodSchema(SuppressIValidateOptions = true)]`. |

## The refinement hook

| ID | Meaning |
| --- | --- |
| `ZODSGEN029` | The model declares **both** `OnZodValidate` and an async custom validation method. Exactly one is allowed. |
| `ZODSGEN034` | `OnZodValidate` is on a type that is not `partial`, or whose containing types are not all `partial`, so the generated declaration cannot be emitted. |
| `ZODSGEN035` | Not declared as `partial void OnZodValidate(RefineCtx<T> context)` — wrong modifiers, return type or parameters. |
| `ZODSGEN036` | A member still uses the retired `IEnumerable<ValidationError> Validate()` contract, which the generator no longer binds. Implement `OnZodValidate`. |

## Custom rules and rule attributes

| ID | Meaning |
| --- | --- |
| `ZODSGEN030` | A rule mapped through `[ZodRule(typeof(…))]` does not implement `IValidationRule<T>` for the property type — including an unbound generic rule that cannot be closed with it. |
| `ZODSGEN031` | A rule constructor parameter could not be mapped from the attribute. |
| `ZODSGEN032` | No validation attribute could be generated for a rule marked `[ZodRule]`. |
| `ZODSGEN033` | *(warning)* A rule-mapped attribute is applied to a type that gets no generated schema — no `[ZodSchema]`, and not reachable as a complex property — so the rule never runs. |
| `ZODSGEN037` | *(warning)* A `[ZodRule]`-derived attribute name (`XRule` → `XAttribute`) is already declared by hand, so no attribute is generated and that declaration's own mapping governs every usage. |
| `ZODSGEN038` | *(warning)* A hand-authored attribute's `[ZodRule(typeof(…))]` mapping does not address every rule declared under the name it encodes (`XAttribute` → `XRule`), so some usages resolve to no rule. |
| `ZODSGEN039` | *(warning)* A rule accepts a `code`/`origin` constructor parameter but does not implement `IZodRule`, so the value never reaches the reported error identity. |
| `ZODSGEN040` | *(warning)* An attribute argument has no effect: the resolved rule has no matching constructor parameter, and the value is not part of the reported identity. |
| `ZODSGEN042` | *(warning)* A rule does not expose a public `const string ErrorCode` **and** a public `const string MessageFormat`, so its error identity cannot be asserted in tests without duplicating literals. |
| `ZODSGEN043` | *(info)* A built-in rule marked `[ZodRule]` generates no attribute, because a constructor parameter cannot be represented as an attribute property. The rule is still reachable through the runtime/fluent API. A name colliding with DataAnnotations is **not** skipped — it is emitted with a `Zod` suffix. |

## Collections

| ID | Meaning |
| --- | --- |
| `ZODSGEN046` | *(warning)* `[CollectionBehavior]` is applied to a property that is not a collection of nullable elements or values (a scalar, a collection whose element cannot be `null`, or a dictionary whose value cannot be `null`), so the setting can never take effect. |

## Microsoft.Extensions.Validation and Razor

| ID | Meaning |
| --- | --- |
| `ZODSGEN047` | *(info)* `[ValidatableType]` on a `[ZodSchema]` type is redundant: the ZodSharp bridge (`AddZodSharpValidation()`) resolves the generated validator first, and the attribute only makes the Microsoft.Extensions.Validation generator emit a second, DataAnnotations-only validator. A code fix removes the attribute. |
| `ZODSGEN051` | *(warning)* A `[ZodSchema]` type is declared in a `.razor` file. Razor compiles components with its own source generator, and generators cannot see each other's output, so no schema is generated. Move the type to a `.cs` file. |

## Unions, value objects, JSON Schema

| ID | Meaning |
| --- | --- |
| `ZODSGEN041` | *(info)* A typed `Z.Union` whose option types are all reference types can use the allocation-free native C# 15 union from `Z.NativeUnion` on .NET 11+. A code fix is offered. |
| `ZODSGEN044` | A `[ZodSchema]` type using an automatic `[Scalar]` form (`[Scalar<TValue>]` / `[Scalar(typeof(TValue))]`) cannot produce a usable schema, so the value-object generator's `Create` would reference a `{Type}Schema` that does not exist or cannot validate. Reported when `GenerateValidateMethod = false` suppresses the `Validate` it calls, or the declared underlying type cannot be represented. |
| `ZODSGEN045` | A type referenced by an exported type has `GenerateJsonSchema = false`, so the cross-platform contract surface is incomplete. |
| `ZODSGEN049` | *(warning)* A member is typed as a C# 15 closed class whose derived type has no `[ZodSchema]`, so a value of that derived type is not validated. Add `[ZodSchema]` to the derived type. |

## `ErrorType` — `ZODSASP*`

These ship with the **core** package, alongside the `ErrorType` factory, the `[ErrorType]` attribute
and the generator that emits `Create`/`Throw` helpers:

| ID | Severity | Meaning |
| --- | --- | --- |
| `ZODSASP001` | Warning | A `MessageFormat` placeholder is not declared in `ErrorType.Parameters`. |
| `ZODSASP002` | Warning | The containing type of an `[ErrorType]` field is not `partial`. |
| `ZODSASP003` | Warning | An `[ErrorType]` field is not `static readonly`. |
| `ZODSASP100` | Error | Unhandled exception in the `ErrorType` source generator. |
| `ZODSASP101` | Error | The `Parameters` of an `[ErrorType]` field could not be extracted. |

`ZODSASP001`–`ZODSASP003` explain why `Create`/`Throw` helpers were **not** generated — a silent
absence otherwise. `ZODSASP100`/`ZODSASP101` are fatal generator failures and name the field.

## Suppressing

Standard mechanisms: `#pragma warning disable ZODSGEN006`, a `NoWarn` entry, or an `.editorconfig`
severity override. The canonical catalogue is the analyzer's `AnalyzerReleases.Shipped.md` /
`AnalyzerReleases.Unshipped.md` in the generator project — a new or changed rule must be recorded
there in the same change.

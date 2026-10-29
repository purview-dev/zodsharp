# Source Generator Diagnostics

The `[ZodSchema]` generator ships an analyzer (category `ZodSharp.SourceGenerator`) that reports configuration and usage problems at compile time. Every diagnostic below is enabled by default; `ZODSGEN033`, `ZODSGEN037`–`ZODSGEN040`, `ZODSGEN042`, `ZODSGEN045`, `ZODSGEN046`, `ZODSGEN048`, `ZODSGEN049`, `ZODSGEN051` and `ZODSGEN052` are warnings, `ZODSGEN041`, `ZODSGEN043` and `ZODSGEN047` are informational, and the rest are errors.

| ID | Meaning |
|---|---|
| ZODSGEN001 | Unhandled generator exception (`"Source generator failed for {0}: {1}"`) |
| ZODSGEN003 | Invalid `[Length]` configuration (min > max) |
| ZODSGEN004 | Unsupported `[Length]` target |
| ZODSGEN005 | Invalid DataAnnotations error-message resource configuration (name without type, or type without name) |
| ZODSGEN006 | Unsupported DataAnnotations usage (string-only attributes on non-string targets; `[AllowedValues]`/`[DeniedValues]` on unsupported types; `[RegularExpression]` on non-strings; `[Range]` on unsupported types) |
| ZODSGEN007 | Custom validation method configured but not found (when a name is explicitly configured) |
| ZODSGEN008 | Custom method return type is not `ValueTask<ValidationResult<T>>` |
| ZODSGEN009 | Custom method does not take two or three parameters: `(T value, CancellationToken ct)` or the service-aware `(T value, IServiceProvider services, CancellationToken ct)` |
| ZODSGEN010 | First custom method parameter is not the model type |
| ZODSGEN011 | Last custom method parameter is not `CancellationToken` |
| ZODSGEN054 | A three-parameter (service-aware) custom method's second parameter is not `IServiceProvider` |
| ZODSGEN012 | Custom method is generic |
| ZODSGEN013 | Custom method must be static when defined on the model type |
| ZODSGEN014 | Custom method is inaccessible from the generated validator (private/protected) |
| ZODSGEN015 | Ambiguous custom method overloads (only when at least two valid candidates exist) |
| ZODSGEN016 | Configured method name is not a valid C# identifier |
| ZODSGEN017 | Custom method is abstract |
| ZODSGEN018 | Custom method is an unimplemented partial method |
| ZODSGEN019 | Custom method uses `ref`/`in`/`out`/`params`/`scoped` parameters |
| ZODSGEN020 | `[Compare]` references an unknown property |
| ZODSGEN021 | `System.ComponentModel.DataAnnotations` reference missing |
| ZODSGEN027 | `IValidateOptions` requested but `Microsoft.Extensions.Options` reference is missing |
| ZODSGEN028 | `IValidateOptions` requested on a struct (requires a class) |
| ZODSGEN029 | A model declares both the `OnZodValidate` refinement hook and an async custom validation method (only one is allowed) |
| ZODSGEN030 | A custom rule mapped through `[ZodRule(typeof(...))]` does not implement `IValidationRule<T>` for the property type (including an unbound generic rule that cannot be closed with it) |
| ZODSGEN031 | A custom rule constructor parameter could not be mapped from the attribute (`[ZodRule]`) |
| ZODSGEN032 | A validation attribute could not be generated for a rule marked `[ZodRule]` |
| ZODSGEN033 | (warning) A rule-mapped attribute is applied to a type that gets no generated schema (no `[ZodSchema]` and not reachable as a complex property), so the rule never runs |
| ZODSGEN034 | The `OnZodValidate` refinement hook is implemented on a type that is not `partial` (or whose containing types are not all `partial`), so the generated declaration cannot be emitted |
| ZODSGEN035 | The `OnZodValidate` refinement hook is not declared as `partial void OnZodValidate(RefineCtx<T> context)` (wrong modifiers, return type, or parameters) |
| ZODSGEN036 | A member still uses the retired synchronous refinement contract (`IEnumerable<ValidationError> Validate()`); implement `OnZodValidate` instead |
| ZODSGEN037 | (warning) A rule marked `[ZodRule]` derives an attribute name (`XRule` → `XAttribute`) that a hand-authored type already declares, so no attribute is generated and that declaration's own `[ZodRule]` mapping governs every usage |
| ZODSGEN038 | (warning) A hand-authored rule attribute's `[ZodRule(typeof(...))]` mapping does not address every rule declared under the name the attribute encodes (`XAttribute` → `XRule`), so some usages of the attribute resolve to no rule |
| ZODSGEN039 | (warning) A rule accepts a `code`/`origin` constructor parameter but does not implement `IZodRule`, so the value never reaches the reported error identity |
| ZODSGEN040 | (warning) An attribute argument has no effect: the resolved rule has no matching constructor parameter and the value is not part of the reported error identity |
| ZODSGEN041 | (info) A typed union (`Z.Union`) whose option types are all reference types can use the allocation-free native C# 15 union returned by `Z.NativeUnion` on .NET 11+; a code fix is offered |
| ZODSGEN042 | (warning) A validation rule (a type implementing `IValidationRule<T>`) does not expose a public `const string ErrorCode` and a public `const string MessageFormat`, so its error identity cannot be asserted in tests without duplicating literals |
| ZODSGEN043 | (info) A built-in rule marked `[ZodRule]` does not generate a validation attribute, because a constructor parameter cannot be represented as an attribute property; the rule is still reachable through the runtime/fluent API. A derived name that collides with `System.ComponentModel.DataAnnotations` is *not* skipped — the attribute is emitted under a `Zod` suffix instead |
| ZODSGEN044 | A `[ZodSchema]` type that uses an automatic `[Scalar]` form (`[Scalar<TValue>]` or `[Scalar(typeof(TValue))]`) cannot produce a usable schema, so the value-object generator's generated `Create` would reference a `{Type}Schema` that does not exist or cannot validate. Reported when `[ZodSchema(GenerateValidateMethod = false)]` suppresses the `Validate` method the value-object generator calls, or when the declared underlying type cannot be represented |
| ZODSGEN045 | (warning) A type that exports JSON Schema references a nested `[ZodSchema]` type whose export is disabled (`[ZodSchema(GenerateJsonSchema = false)]` or the assembly-level `ZodSharpGenerateJsonSchemas=false` switch), so the parent's exported schema falls back to an empty schema for that property (`"Property '{0}' references type '{1}', which disables JSON Schema generation; the exported schema cannot describe it"`) |
| ZODSGEN046 | (warning) A `[CollectionBehavior]` attribute has no applicable option: `AllowNulls` requires nullable elements/values; `DefaultImmutableArrayBehavior` applies to `ImmutableArray<T>` (including nullable wrappers) |
| ZODSGEN047 | (info) `[ValidatableType]` (Microsoft.Extensions.Validation) on a `[ZodSchema]` type is redundant: the ZodSharp bridge resolves the generated validator first, and the attribute only makes the Microsoft.Extensions.Validation generator emit a second, DataAnnotations-only validator for the same type. A code fix removes the attribute |
| ZODSGEN048 | (warning) A type has rules that only run asynchronously (an `AsyncValidationAttribute` member or an `IAsyncValidatableObject` target) but a synchronous `Validate` is generated; that `Validate` throws `InvalidOperationException` rather than skip them, so call `ValidateAsync` |
| ZODSGEN049 | (warning) A member is typed as a C# 15 `closed` class whose derived type has no `[ZodSchema]`, so a value of that derived type is not validated (`"Property '{0}' is typed as the closed type '{1}', but the derived type '{2}' has no [ZodSchema], ..."`); add `[ZodSchema]` to the derived type |
| ZODSGEN050 | A generated `IValidateOptions` validator would conflict with a hand-authored `[OptionsValidator]` class (SYSLIB1205), so the ZodSharp validator is not generated. Keep one generator: remove `[OptionsValidator]`, or set `[ZodSchema(SuppressIValidateOptions = true)]` |
| ZODSGEN051 | (warning) A `[ZodSchema]` type is declared in a Razor component (`.razor`). Razor compiles components with its own source generator, which the ZodSharp generator cannot see, so no schema is generated; move the type to a `.cs` file |
| ZODSGEN052 | (warning) An attribute's `ErrorMessage` uses a positional placeholder the attribute does not supply, or an unbalanced brace (`"The ErrorMessage of [{0}] on '{1}' contains '{2}', which the attribute does not supply; it is shown literally"`). Messages are formatted with a never-throw engine, so the text is shown literally at runtime; the diagnostic catches the typo at build time |
| ZODSGEN053 | (warning) `[MinLength]`, `[MaxLength]` or `[Length]` is applied to a collection member of a `[ZodSchema]` type in a project with `PublishTrimmed`, `PublishAot`, `IsTrimmable` or `IsAotCompatible` (`"[{0}] on the collection member '{1}' is not trim-safe (its constructor is [RequiresUnreferencedCode]); use [{2}] from ZodSharp.Rules, which the generator validates identically"`). The BCL attributes raise IL2026 on every usage; `[MinCount]`, `[MaxCount]` and `[CountRange]` report the same errors. A code fix swaps the attribute. See [Trimming and Native AOT](Trimming-and-AOT.md#collection-counts-mincount-maxcount-and-countrange) |

IDs `ZODSGEN002` and `ZODSGEN022`–`ZODSGEN026` are intentionally unused; rule identifiers are never renumbered or re-used.

The `[ErrorType]` generator, shipped in the same analyzer assembly, reports `ZODSASP001`–`ZODSASP003`,
`ZODSASP100` and `ZODSASP101`; they are described in [ASP.NET Core Integration](AspNetCore-Integration.md).

## Suppressing

Diagnostics can be suppressed per-project or per-site with the standard `#pragma warning disable ZODSGEN006` / `NoWarn` mechanisms. Refer to the analyzer's shipped release notes (`AnalyzerReleases.Shipped.md` / `AnalyzerReleases.Unshipped.md` in the generator project) for the canonical catalog.

The package also ships one **suppression** that is not a reportable rule: `ZODSUP001` removes `CA1849` ("call async
methods when in an async method") for ZodSharp's synchronous validation entry points — generated `Validate` and
`ValidateMember`, the validator adapters and the fluent schemas. CA1849 fires there only because `ValidateAsync`
exists; the synchronous pipeline is the cheap path and never blocks, and a type whose graph has asynchronous rules is
already covered by `ZODSGEN048`. The suppression is by call target, so `Task.Wait`, `Thread.Sleep` and another
library's `Validate`/`ValidateAsync` pair still report CA1849.

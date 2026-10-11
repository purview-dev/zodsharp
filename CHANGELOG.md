# Changelog

All notable changes to this repository are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). `package.json` is the authoritative version; see
[Release Flow](docs/wiki/Release-Flow.md).

Released versions correspond to `v<version>` GitHub releases. Entries below the `Unreleased` heading have not
been published to NuGet.

## Unreleased

> **This release contains breaking changes.** See [Breaking changes](#breaking-changes) below before
> upgrading from `2.0.0`. The analyzer catalogue records them under `2.0.2`.
>
> Note the changes below are consumer-visible: a rule's public `Code` constant was renamed to `ErrorCode`,
> and rule constructor signatures changed, so attribute usages and code referencing those constants may not
> compile against this version. Read the migration notes before upgrading.

### Breaking changes

- **Rule error-identity constants renamed from `Code` to `ErrorCode`.** Every validation rule now exposes its
  error identity as public `const string ErrorCode` and `const string MessageFormat`. Code that referenced a
  rule's `Code` constant must use `ErrorCode`. The new `ZODSGEN042` analyzer enforces the convention on
  source-declared rules.
- **Rule constructors are standardised.** Every rule now has a consistent constructor, extension method and
  generated attribute. Generated rule attributes carry the rule's *value* parameters as constructor arguments:
  a parameter declared without a default becomes a required positional argument, a parameter with a default
  keeps it, and `message`/`code`/`origin` remain properties. Attribute usages for required values become
  positional — `[MinValue(3)]`, `[Regex("^[a-z]+$")]`. Usages that previously set a required value by property
  name no longer compile.
- **`NonSentinelRule<T>.MessageFormat` reworded and the `{0}` argument now describes the sentinel.** The format
  is now `Value has a sentinel value, but got {0}`, and `{0}` is a description (`an empty GUID`,
  `DateTime.MinValue`, `an empty string`, …) instead of the raw value. Code or tests that assert against the old
  string must be updated.

### Added

- **`UUIDRule` now validates a `Guid` as well as a `string`.** A `Guid` is a UUID, so its bytes are checked
  directly; `[UUID]` / `[UUID(UuidVersion.Vx)]` can therefore constrain a `Guid` member or a `[Scalar<Guid>]`
  value object, and the constraint is exported as `format: "uuid"` plus a version-pinning `pattern`.

- **A type-level rule on a `[Scalar]` type now names the scalar in its default message.** Because a scalar's
  error reports an empty path, the generator replaces the leading subject word `Value` in the resolved rule's
  `MessageFormat` with the scalar's name, so `[NonSentinel]` on `[Scalar] TenantId` reports
  `TenantId has a sentinel value, but got an empty GUID`. A rule whose format does not contain `Value`, and any
  explicit `message`/`ErrorMessage`, are left unchanged.

- **Native C# 15 union support.** `Z.NativeUnion` and `ZodTypedNativeUnion` return a native union on `net11.0`,
  giving allocation-free, exhaustively pattern-matchable options. `ZODSGEN041` suggests it where a typed union's
  option types are all reference types.
- `net11.0` added to the target frameworks (now `net8.0`, `net9.0`, `net10.0`, `net11.0`).
- **Per-framework `Microsoft.Extensions.*` dependencies.** All target frameworks previously resolved
  `Microsoft.Extensions.Options` and `…DependencyInjection.Abstractions` 10.0.12, so referencing this package
  dragged a **net8.0 (LTS)** application onto .NET 10 assemblies. Each framework now gets its matching line:
  net8.0 → 8.0.x, net9.0 → 9.0.20, net10.0 → 10.0.12. On net11.0 the SDK prunes them entirely because the
  targeted framework supplies them, which is why the net11.0 dependency group in the `.nuspec` is empty —
  that is expected, and a net11.0 consumer resolves them from the framework (verified).
- **Auto-generated attributes for all rule types**, including non-generic and open-generic rules under a single
  attribute, and expanded downstream rule generation for `Purview.ValueObjects` consumers.
- A new `enum` rule, applied automatically on generated schemas, plus additional explicit rules.
- `IZodRule` organisation with a supporting analyzer, and `RuleMessage` is now public.
- Eight new diagnostics: `ZODSGEN037`–`ZODSGEN044`, recorded in `AnalyzerReleases.Shipped.md` under
  `## Release 2.0.2`. See [Source Generator Diagnostics](docs/wiki/Source-Generator-Diagnostics.md).
- **Automatic scalar value-object support.** `[ZodSchema]` now works with the automatic scalar forms
  `[Scalar<TValue>]` and `[Scalar(typeof(TValue))]` (Purview.ValueObjects 1.0.1+), where the value-object
  generator declares the underlying property. ZodSharp resolves the underlying type and property name from
  the attribute, generates the `{Type}Schema` (and validator adapter) without the member being visible, and
  adapts type-level `[RequiredZod]`/`[ZodRule]` rules to the underlying value. Nullable scalars
  (`Nullable = true`, or a `T?` value type) accept and round-trip JSON `null`. Property-level DataAnnotations
  have no host on the generated property; use Zod-native type-level rules or the manual `[Scalar]` form.
- **`NullOrNonWhiteSpaceRule` / `[NullOrNonWhiteSpace]`.** A null-tolerant string rule that accepts `null`
  but rejects an empty or whitespace-only value, for nullable scalar value objects where `[RequiredZod]`
  (which always rejects `null`) cannot express the check.
- **`ZODSGEN044`.** A `[ZodSchema]` type using an automatic `[Scalar]` form that cannot produce a usable
  schema (a suppressed `Validate` method, or an unrepresentable underlying type) is reported instead of
  leaving a dangling `{Type}Schema` reference. Recorded in `AnalyzerReleases.Unshipped.md`.
- **A `[ZodSchema]` schema now exports a complete JSON Schema.** The generated `{Type}Schema` class emits a
  static `BuildJsonSchema()` method and the generated `{Type}SchemaValidator` implements the new
  `ZodSharp.JsonSchema.IJsonSchemaProvider`, so `Z.ToJsonSchema(new {Type}SchemaValidator())` returns a real
  definition instead of an empty one. The definition is built from the same metadata as `Validate`, so nested
  objects, native enums, nullable/required fields, scalar value objects (`[Scalar]` export as their underlying
  value), `[JsonPropertyName]`/camelCase property names, DataAnnotations and `[ZodRule]`-mapped rules are all
  reflected. Previously only hand-built runtime schemas could be exported.
- **Rules describe their own JSON Schema constraint.** The new `ZodSharp.JsonSchema.IJsonSchemaRule` is
  implemented by the built-in rules (length, bounds, `multipleOf`, integer/safe-integer, string formats,
  `pattern`, sentinel, allowed/denied values, enum), and `ToJsonSchemaConverter` and the generated
  `BuildJsonSchema()` apply them identically. A rule that cannot be represented — or a consumer rule that has
  not opted in — is listed under the `x-zodsharp-unsupported` extension on the exported schema instead of
  being dropped silently.
- `ZodNativeEnum<TEnum>` and `ZodEnum` now export as JSON Schema `enum` (via the new `IJsonSchemaEnumInfo`
  seam), and `JsonSchemaDefinition` gained `not`, `contentEncoding` and `x-zodsharp-unsupported`.
- **JSON Schema generation can be opted out, per type or per assembly.** `[ZodSchema(GenerateJsonSchema =
  false)]` omits the export members for one type, and the `ZodSharpGenerateJsonSchemas=false` MSBuild property
  does so for the whole assembly — keeping validation-only types out of the cross-platform contract surface.
  An exporting type that references a type whose export is disabled falls back to an empty schema and is
  reported as the new **ZODSGEN045** warning instead of failing to compile or exporting silently.

### Changed — trimming and Native AOT

- `Purview.ZodSharp` is now marked `IsAotCompatible`, enabling `IsTrimmable` and both analyzers. Getting
  there meant replacing the JSON Schema exporter's name-based reflection with type-checked internal seams
  (`IJsonSchemaArrayInfo`, `IJsonSchemaInnerSchema`, `IJsonSchemaNullableInfo`, `IJsonSchemaLiteralInfo`,
  plus internal accessors on the rules and `ZodType`). That removed all 17 trim warnings *and* the six
  silent export defects above — both were symptoms of the same root cause.
- **`RegisterFromAssembly` is now genuinely trim- and AOT-safe, not merely annotated.** It used to rebuild
  the validator's type name as a string and ask `Assembly.GetType(string)` for it — untrimmable, so the
  validators were removed from a published application and registration threw at runtime. The generator now
  records the validator in the attribute as a `typeof`, which roots it, and
  `ZodSchemaGeneratedAttribute.ValidatorType` is annotated
  `[DynamicallyAccessedMembers(PublicParameterlessConstructor)]` so its constructor survives. No string
  resolution remains, and the `[RequiresUnreferencedCode]` annotation has been removed entirely. Discovery
  is also now proportional to the number of generated schemas rather than the size of the assembly.
  - **Breaking:** `ZodSchemaGeneratedAttribute` now takes `(Type targetType, Type validatorType)`. The
    attribute is generator-emitted, so it regenerates on rebuild; only hand-written usages need updating.
- **`Purview.ZodSharp.SystemTextJson` is AOT-clean and marked.**
  - The validating `JsonConverter<T>` resolves a `JsonTypeInfo<T>` from the serializer options instead of
    calling the reflection-based `JsonSerializer` overloads, so it defers to whatever resolver the host
    configured — a source-generated `JsonSerializerContext` in a trimmed or AOT application. It also caches
    the converter-excluding options copy per instance; it previously rebuilt them on every read and write,
    which meant a cold metadata cache on each call, not just an allocation.
  - Every extension method now comes in a pair: a `JsonTypeInfo<T>` overload that is safe everywhere, and
    the existing `JsonSerializerOptions` overload annotated `[RequiresUnreferencedCode]`/
    `[RequiresDynamicCode]`. This is the same shape `JsonSerializer` itself uses.
  - JSON Schema import and export use this package's own source-generated `JsonSchemaJsonContext`, so they
    need nothing from the consumer. To make that possible the custom `JsonSchemaNamingPolicy` was removed —
    a naming policy is a runtime object the source generator cannot reproduce — and the four `$`-prefixed
    keyword names are now declared with `[JsonPropertyName]` on `JsonSchemaDefinition`. **The wire format is
    unchanged**, and the test for it now asserts the emitted JSON rather than the naming mechanism.
- `Purview.ZodSharp.AspNetCore` is AOT-clean and marked. Graph-based assembly scanning
  (`ScanAssemblyGraphs`) stays reflective by nature and is documented as the path to avoid in a trimmed or
  AOT host, in favour of naming assemblies explicitly or registering validators directly.
- The discriminated-union discriminator accessor falls back to plain reflection when
  `RuntimeFeature.IsDynamicCodeSupported` is false, instead of relying on expression compilation that Native
  AOT does not provide.
- `Purview.ZodSharp.NewtonsoftJson` is **not** marked AOT-compatible and cannot be: Newtonsoft.Json is
  reflection-based throughout. Use `Purview.ZodSharp.SystemTextJson` in a trimmed or Native AOT
  application.

### Changed

- Clarified in `SourceGenerators.csproj` that `EnforceExtendedAnalyzerRules` and `TreatWarningsAsErrors` are
  supplied and enforced by `Purview.BuildSdk` for every `IsRoslynComponent` project, rather than being optional
  project settings. Both were already `true`; the commented-out block implied otherwise.

### Security

- **Regex match timeouts on every pattern the library compiles.** `RegexRule(string)` and the generated
  `[RegularExpression]`/`[Regex]` support previously compiled patterns with **no** timeout, so a
  catastrophically backtracking pattern could hang the calling thread — and the generated path, which is how
  ASP.NET Core request DTOs are validated, dropped the 2-second default that
  `System.ComponentModel.DataAnnotations.RegularExpressionAttribute` provides. JSON Schema import was worse:
  both the pattern and the input come from outside the application. All of these now carry
  `RegexRule.DefaultMatchTimeout`, and exceeding it is reported as a validation failure rather than an
  exception escaping into the host.
- The shared budget is 2 seconds, matching `RegularExpressionAttribute`. The built-in `Email`, `Url` and
  `Duration` rules previously used 100 ms, which proved too tight: under full-suite parallel load their
  matches exceeded it and threw `RegexMatchTimeoutException`, rejecting valid input. Bounding the work is
  what defeats ReDoS; an aggressive bound only adds false negatives.

### Fixed

- **Guid and UUID export semantics now match the runtime validator.** A C# `Guid` was exported as
  `format: "uuid"`, so a consumer applied its RFC UUID validator (Zod's `z.uuid()`, which `z.fromJSONSchema`
  also uses for `format: "guid"`) and rejected valid values that C# accepts — including every
  `FastGuid.NewSqlServerGuid()`/`NewPostgreSqlGuid()` time-bound identifier, which is deliberately not an RFC
  UUID. A `Guid` (and a `[Scalar<Guid>]` value object) now exports as a canonical hexadecimal `pattern`; a
  versioned `UUIDRule`/`[UUID(UuidVersion.Vx)]` keeps its version as an additional `pattern`; and two rules that
  both constrain `pattern` (for example a Guid plus `[NonSentinel]`) are composed under `allOf` instead of one
  silently overwriting the other. The JSON Schema importer now intersects every `allOf` branch (previously it
  took only the first) so an exported schema round-trips.
- **Exported enum schemas now match the System.Text.Json wire form.** The generated JSON Schema always
  enumerated an enum's numeric values, so an enum serialized as a string (a per-type or per-property
  `[JsonConverter(typeof(JsonStringEnumConverter))]`, or a global converter) exported a schema that rejected
  the actual JSON. The generator now detects the string-enum converter, honours `[JsonStringEnumMemberName]`,
  and supports the assembly-level `ZodSharpEnumSerialization=String` / `ZodSharpEnumNamingPolicy=CamelCase`
  switches for a global converter and naming policy.
- **A `[Flags]` enum exported an `enum` list, rejecting valid combinations.** The generated validator never
  validates a flags enum against its defined members (a combination is a valid value without being a defined
  member), but the generated JSON Schema enumerated them, so a value such as `Read | Write` failed the
  exported schema. A flags enum now exports as `type: integer`.
- **Exported requiredness and null acceptance now follow the nullable reference-type annotation.** The
  generator treated every reference type as nullable, so a non-nullable `string` with `[NonSentinel]` was
  wrapped in `anyOf: [ …, { "type": "null" } ]`, omitted from `required`, and its rule was skipped for null
  (null passed validation). A non-nullable reference type is now required, its rules run unconditionally and
  reject null, and the generated validator reports a null value rather than skipping it — matching the
  exported schema. A `string?` keeps its null branch, and a rule that rejects an absent value still makes the
  property non-null; a rule that accepts null (such as the built-in `NullOrNonWhiteSpaceRule`) keeps the
  property nullable.
- **A `[ZodSchema]` type referenced as a nested property could lose its generated DI adapter.** A type is
  reached twice by the generator — once from its own `[ZodSchema]` attribute (the primary schema) and once as
  a nested property of another schema (a secondary copy). Deduplication kept whichever the attribute contexts
  happened to produce first, so a secondary copy could displace the primary, dropping the primary-only members:
  the `{Type}SchemaValidator` adapter and its `[assembly: ZodSchemaGenerated]` registration. Value-object
  structs that were referenced by an earlier-declared type (for example a `[Scalar]` id nested in a record)
  silently vanished from `IZodSchemaFactory` assembly scanning. Deduplication now always prefers the primary
  schema.
- **`[RequiredZod]` now rejects an absent value exactly like `[Required]`.** A custom rule bound to a
  reference-type member was emitted inside the generator's non-null guard, so a missing value never reached
  `RequiredRule<T>` and options/schema validation passed. The rule now implements the new `IRequiredRule`
  marker and the generator emits it before the guard, reporting `missing_field` for `null` (and, unless
  allowed, empty or whitespace-only strings). The attribute's `AllowEmptyString` property was renamed to
  `AllowEmptyStrings` to match `RequiredAttribute.AllowEmptyStrings`, so `[RequiredZod]` is a drop-in
  replacement for `[Required]`.
- **`ZodArray` built corrupt error paths for nested element failures.** It used the two-argument
  `ImmutableArray.CopyTo(destination, destinationIndex)` — which copies *into* that index — and then
  overwrote the copied element with the index segment. For an element error at path `["email"]` the result
  was `[null, "[0]"]` instead of `["[0]", "email"]`: the field name was destroyed and a null segment
  introduced. `ProblemDetails` therefore reported the key as `[0]`, so an API client could not tell which
  field of which array element failed. Only arrays whose element schema produces a path (arrays of objects,
  nested arrays) were affected, which is why the existing containment-based assertion did not catch it.
- **`ZodArray` enforces its length constraints before validating any element.** They depend only on the
  array's length, but were checked last — so an array of a million elements against `.Max(10)` ran a
  million element validations and allocated a million `ValidationError`s, each with an interpolated index
  string, before reporting that the array was simply too long. A single request could pin a core and a
  large heap allocation on input the schema had already declared out of range. Note this changes which
  error is reported when an array is both the wrong length *and* has invalid elements: the length failure
  now wins, which matches the container constraint being the outer one.
- **`CompiledValidator` no longer leaks a `DynamicMethod` per call.** It built an expression tree on every
  invocation that compiled to a single `IZodSchema.Validate` call — no inlining, no devirtualisation, just a
  `LambdaExpression.Compile` whose emitted method is never reclaimed in a non-collectible load context. The
  documented inline usage therefore leaked managed and native memory for the process lifetime. The delegate
  now binds `Validate` directly and is cached per schema instance with weak keys, which is behaviourally
  identical, strictly cheaper, and removes the `RequiresDynamicCode` dependency.
- **JSON Schema export was silently dropping most of the schema.** `ToJsonSchemaConverter` reached into the
  library's own types by reflecting on type and field *names*, and several of those names matched nothing.
  `GetField` returns null rather than throwing, so each lookup failed quietly. Fixed, with tests:
  - **Object properties were always empty.** It looked for a field `_shape` on `ZodObject`; the shape is a
    primary-constructor parameter and `ZodObject.Shape` has been public all along.
  - **Object property types were empty even once the shape was found**, because the builders wrap each field
    schema to present it untyped and the wrapper fell through to the generic fallback.
  - **Array `Min`/`Max` never appeared.** It looked for rules named `MinItemsRule`/`MaxItemsRule` reading
    fields `_minItems`/`_maxItems`. None of those four names exists anywhere in the library.
  - **Array element schemas never appeared**, from a `_elementSchema` field that does not exist.
  - **Optional properties exported as an empty "any" schema**, from a `_innerSchema` field that does not exist.
  - **Unions exported with an empty `anyOf`**, from a `_options` field that does not exist — and the cast
    expected an array where the member is an `IReadOnlyList`.
  - **Optional properties were still listed as required**, because the required check matched the wrapper's
    type name rather than asking `IOptionalSchema`.
- Scalar schema generation, which had regressed.
- The `uuid` attribute rule required both constructors to be present to generate.
- Nullability annotations on `IZodRule` properties.
- **A scalar rule adapted over a nullable reference value no longer emits a nullability-mismatched adapter.**
  `CustomRuleResolver` built the `ScalarRuleAdapter<TSelf, TValue, TRule>` identity from a `TypeIdentity`,
  which drops nullable reference annotations, while the wrapped rule was closed over a `TypeReference` that
  keeps them. A scalar backed by `string?` therefore emitted
  `ScalarRuleAdapter<T, string, NonSentinelRule<string?>>`, whose `TValue` satisfied neither the adapter's
  `IValidationRule<TValue>` constraint nor the value object's `IScalarValueObject<TSelf, TValue>` — reporting
  CS8631 at every consumer. The value argument is now carried as a `TypeReference`, so both sides stay in step.

### Added — packed generator smoke test

- `just smoke-packed-generator` (and a `packed-generator` CI job) packs the package, then builds and runs a
  throwaway consumer against it with an isolated package cache. Every in-repo generator test runs against
  the **unmerged** generator by design, and pack validation only checks the IL-merged assembly is *present*
  in the `.nupkg` — nothing checked that it loads and generates. That merge has regressed twice (`#36`,
  `#38`), both times silently breaking consumers while this repository's suite stayed green. Verified to
  have teeth: with generation disabled the consumer fails to compile with
  `CS0103: The name 'PersonSchema' does not exist`.

### Documentation

- **Removed two unsubstantiated performance claims.** "10x faster than reflection-based validation" appeared
  twice; the benchmark suite measures this library against itself across scenarios and contains no
  comparison with another validation library, so there was nothing behind it. The README now says so
  explicitly and points you at measuring your own schemas. Also removed "Array pooling via `ArrayPool<T>`
  for zero-allocation helpers": `ArrayPool` appears only inside `ZeroAllocationHelpers`, which is
  `internal` and has no callers anywhere — the claim described dead code. The `Span<T>` bullet now names
  where that work actually is (`IStringValidationRule`, `ZodString.ValidateSpan`/`IsValidSpan`,
  `EmojiRule`). The measured sub-microsecond timings are unchanged; those are benchmark-backed.
- **Fixed a thread-safety contradiction.** `Compiled-Validators-and-Caching.md` claimed "Schemas are
  immutable and shareable", while `Guarantees-and-Limitations.md` correctly documents that the fluent rule
  methods mutate the receiver in place. A reader who believed the first would cache a schema and then
  mutate it from a request path. That page now states the build-then-share rule, cross-references the
  limitation, and documents three properties of `SchemaCache` that matter because it is process-global: a
  shared key space, no type check on retrieval, and no eviction.
- `Compiled-Validators-and-Caching.md` also described the old expression-tree implementation and claimed it
  removed interface dispatch. Neither was true: the tree was a single `Validate` call. The page now
  describes what the type actually does — and says plainly that it does not make validation faster.
- `Performance.md` records that its figures were produced with BenchmarkDotNet 0.15.8 while the repository
  now pins 0.16.0-preview.2, so the numbers are from a different version than the suite builds against.
- Corrected the target-framework list in `README.md` and linked `LICENSE.md` from the licence section.
- The vitest cross-platform suite now runs in CI, and it **fails** rather than silently passing when the C#
  cross-platform fixtures are absent. It also now reads the C# output recursively — the previous flat directory
  read never matched a file, so the C#-to-Zod handshake had never actually been asserted.

## 2.0.0

First stable release. See the
[`v2.0.0`](https://github.com/purview-dev/zodsharp/releases/tag/v2.0.0) release notes, and
[What's new in v2](README.md#whats-new-in-v2) for the summary of the `Purview.*` package IDs, the JSON
integrations, JSON Schema interoperability, the ASP.NET Core `ProblemDetails` integration and the expanded
DataAnnotations support.

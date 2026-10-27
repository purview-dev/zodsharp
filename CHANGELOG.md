# Changelog

All notable changes to this repository are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). `package.json` is the authoritative version; see
[Release Flow](docs/wiki/Release-Flow.md).

Released versions correspond to `v<version>` GitHub releases.

## [Unreleased]

### Added

- **`Purview.ZodSharp.Extensions.Validation`** (new package, `net11.0`): the bridge to .NET 11's
  Microsoft.Extensions.Validation, so minimal APIs, Blazor Server and Blazor WebAssembly validate ZodSharp types
  with ZodSharp. `AddZodSharpValidation()` registers `ZodValidatableInfoResolver` as the first resolver whether
  `AddValidation()` is called before, after or not at all; errors carry the owning `Container` and leaf `Name`
  Blazor needs to place messages on nested, collection and dictionary fields; parameters follow
  Microsoft.Extensions.Validation's path conventions; `MaxDepth` and synchronous-validation behaviour match the
  platform. Full-fidelity errors are available through `ValidateContext.ZodReport` and an optional
  `IZodValidationReportSink`; `StringLocalizerZodMessageFormatter` localizes messages (auto-enabled when an
  `IStringLocalizerFactory` is registered, honouring `ValidationOptions.LocalizerProvider`); and
  `ValidationOptions.ValidateAsync<T>` validates outside HTTP and Blazor. References the
  `Microsoft.Extensions.Validation` package only - never the ASP.NET Core shared framework - so it works in
  WebAssembly clients.
- **Minimal APIs on .NET 11: full-fidelity ProblemDetails from the built-in validation pipeline.** On
  `net11.0`, `Purview.ZodSharp.AspNetCore` now depends on `Purview.ZodSharp.Extensions.Validation` (that target
  framework only). `AddZodSharpProblemDetails()` registers a scoped `ZodRequestValidationReport` (the bridge's
  `IZodValidationReportSink`) and composes `ProblemDetailsOptions.CustomizeProblemDetails`. The ZodSharp callback
  runs first, then any callback configured before or after it. It rewrites the pipeline's
  `HttpValidationProblemDetails` with `ZodProblemDetailsWriter.Apply`: DataAnnotations entries are merged with
  `dataannotations` issues, and the `ErrorType`, `StatusCodeSelector` and `StatusCodeOverride` status is mirrored
  to the response. A DataAnnotations-only failure only gains the `issues` projection. Endpoints and route
  groups validated by `AddZodSharpValidation()` therefore return the same body, byte for byte apart from
  `traceId`, as the endpoint filter, `ZodExceptionHandler` and the manual results. `WithZodSharpValidation()`
  on an endpoint the pipeline also validates skips the parameters the bridge covers, logged once at `Debug`, so
  nothing is validated twice.
- **`IZodMessageFormatter`** in `ZodSharp.Core`: formats an error's user-facing message for a culture at a host
  boundary.
- **`Purview.ZodSharp.AspNetCore.OpenApi`** (new package, `net11.0`). `options.AddZodSharp()` on
  `OpenApiOptions` registers schema, operation and document transformers so the ASP.NET Core 11 OpenAPI
  document (3.1 and 3.2) describes what ZodSharp enforces: lengths, bounds (exclusive bounds in the 3.1+ form),
  `multipleOf`, integer narrowing, patterns (several promote to `allOf`), formats (including `[EmailAddress]`,
  which the platform does not map), `enum`/`not`, `contentEncoding`, required members and `type: [x, "null"]`
  nullability, with ZodSharp winning when a DataAnnotation disagrees. Polymorphic types are described with
  `oneOf` + `discriminator`; C# unions keep the platform's `anyOf` and get their cases constrained. Rules with
  no JSON Schema keyword are described with `x-zod-*` vendor extensions. Operations that bind a ZodSharp-validated
  parameter list `400` (and `422` when a registered `ErrorType` uses it) as `application/problem+json` with the
  shared `ZodValidationProblemDetails`/`ZodValidationIssue` components, merging into a declared
  `ProducesValidationProblem()` response. Types ZodSharp does not know are left exactly as the platform produced
  them.
- **JSON Schema export.** `ToJsonSchemaConverter.ConvertUntyped(object)` converts a schema or validator known only
  at runtime (including the `ZodSchemaValidator<T>`/`ZodUntypedValidator<T>` adapters, which now unwrap);
  `JsonSchemaDefinition.GetVendorExtensions()`/`SetVendorExtension()` record `x-zod-*` information for hosts that
  carry it, without changing the serialized JSON Schema. `ZodDiscriminatedUnion` exports as `oneOf` (it exported
  the empty schema). `IPAddressRule` exports `format: ipv4`/`ipv6`, `DurationRule` `format: duration`,
  `DateStringRule` `format: date`, `DatetimeStringRule` `format: date-time` plus its UTC pattern,
  `TimeStringRule` its local-time pattern, and `Base64StringRule` `contentEncoding: base64`; `CidrRule`,
  `JWTRule` and an any-family `IPAddressRule` stay listed as unsupported and record `x-zod-cidr`, `x-zod-jwt`
  and `x-zod-ipVersion`.
- **Source generator — field-level validation and member resolution.** Every generated `{Type}Schema` now has
  `ValidateMember(value, memberName)`, `ValidateMemberAsync(value, memberName, services, ct)` and
  `IsValidatableMember(memberName)` (the member's own rules only: no nested recursion, type rules,
  `IValidatableObject` or refinement), `TryResolveMember(value, path, out container, out memberName)`
  (reflection-free), `Paths.{Member}` `ZodPath` constants, a `JsonNames.Get(member)` table and
  `ToJsonNamedPath(path, namingPolicy)`. `Validate` calls one method per member. The `{Type}SchemaValidator`
  adapter implements `IZodMemberValidator<T>`, `IZodMemberResolver<T>`, `IZodUntypedValidator` and the new
  `IZodJsonPathMapper`.
- **Source generator — async graph.** `{Type}Schema.ValidateAsync(value, services, ct)` awaits nested schemas'
  async validation and threads the caller's services; it completes synchronously when nothing in the graph is
  asynchronous (`{Type}Schema.RequiresAsyncValidation` is a compile-time constant). A custom async method may take
  `(T value, IServiceProvider services, CancellationToken ct)`, and `[ZodAsyncMember(nameof(X))]` makes
  `ValidateMemberAsync` run it for that member only.
- **Source generator — DataAnnotations inputs.** `IValidatableObject` runs after the members and type rules (as in
  DataAnnotations and Microsoft.Extensions.Validation); on .NET 11 `IAsyncValidatableObject` and
  `AsyncValidationAttribute` members run in `ValidateAsync`. `[Display(Name, ResourceType)]` reads the resource
  property at runtime.
- **Source generator — C# 15.** A member typed as a union dispatches to the active case's schema, and a member
  typed as a closed class to the derived type's schema (paths do not include the union's `Value`).
- **Source generator — trim-safe registration.** Each assembly gets
  `ZodSharp.Generated.{Assembly}ZodSharpSchemas.Register(IZodSchemaFactory)` and, with dependency injection,
  `services.AddZodSharp{Assembly}Schemas()`; both name every validator directly (no attribute scanning or
  `Activator`). `AddZodSharpFactory` applies every `ZodSchemaRegistration` added with `AddZodSchemaRegistration`.
- **Source generator — options.** On .NET 11 the generated `{Type}Validator` also implements
  `IAsyncValidateOptions<T>` when the type has asynchronous rules.
- `ZodValidationDepth` (`MaxDepth`, default 32): generated validators throw `InvalidOperationException` at the
  limit, like Microsoft.Extensions.Validation, instead of overflowing the stack on a cyclic graph.
- `ErrorType.Origin` (init-only); the generated `Create*` helpers apply it and the `MessageFormat`.
- `RegexRule.DefaultMatchTimeoutMilliseconds`.
- Diagnostics `ZODSGEN047` (redundant `[ValidatableType]`, with a code fix), `ZODSGEN048` (async-only rules
  behind a synchronous `Validate`), `ZODSGEN049` (closed-hierarchy derived type without `[ZodSchema]`),
  `ZODSGEN050` (generated options validator conflicts with `[OptionsValidator]`, SYSLIB1205),
  `ZODSGEN051` (`[ZodSchema]` type in a Razor file) and `ZODSGEN052` (an `ErrorMessage` placeholder the
  attribute does not supply).
- **Shared error identities.** `ZodIssueCodes` (codes), `ZodMessages` (named-placeholder templates),
  `ZodIssues` (factories for null/invalid-type/missing-field/refinement/union/collection-size errors and the
  `GetUnionErrors()` extension) and `ZodTypeNames` (`Of(Type)`/`OfValue(object)`: Zod type names such as
  `string`, `number`, `array`) in `ZodSharp.Core`, so every producer reports the same failure identically.
- **`ZodErrorOptions.IncludeReceivedValues`** (default `false`). When enabled, rule failures carry the raw input
  as the `received` parameter. Development only; built-in messages never contain the input either way.
- **`IZodFailureDescriber<T>` / `IZodStringFailureDescriber` / `ZodRuleFailure`.** A rule can describe the
  value-dependent part of its failure (category by direction, `count`); `ZodRuleErrors` applies it.
- **Structured rule identity.** Every built-in rule reports `Origin` (`string`, `number`, `date`, `enum`, …),
  `Category` (the Zod v4 issue code), `Lower`/`Upper` bounds with the correct inclusivity, and parameters such as
  `format`, `divisor`, `pattern`, `prefix`, `options`, `version` and `count`.
- **`RefineCtx<T>.AddIssue(ValidationError)`** and **`AddIssue(code, message, ZodPath, parameters, category)`**,
  plus `RefineCtx<T>.FullPath` and a `RefineCtx(T, ZodPath)` constructor.
- `ZodIssueParameters.Format`, `Divisor`, `Pattern`, `Prefix`, `Suffix`, `Includes`, `Version` and `Key`.
- `ZodMessageFormat` renders a sequence parameter (an options or keys list) as its elements joined by `, `.

- **One ProblemDetails writer, `ZodProblemDetailsWriter`** (`Purview.ZodSharp.AspNetCore`). The endpoint filter,
  `ZodExceptionHandler`, `ZodTypedResults` and every `ToHttpValidationProblemDetails`/`ToValidationProblemDetails`/
  `ToValidationProblem` overload delegate to it, so all paths produce the same body (snapshot-tested). Public
  `Create(ReadOnlySpan<ValidationError>|IEnumerable<ValidationError>, HttpContext?)`, `Apply(existing, errors, ...)`
  (rewrites an existing `HttpValidationProblemDetails` and keeps non-ZodSharp `errors` entries, projected as
  `dataannotations` issues), `WriteAsync(...)` (through `IProblemDetailsService`, so `CustomizeProblemDetails` runs)
  and `GetOrCreate(IServiceProvider?)`.
- **`WithZodSharpValidation()` for any `IEndpointConventionBuilder`**, including route groups. An endpoint filter
  factory selects the parameters to validate when the endpoint is built (skipping services, `HttpContext`,
  `CancellationToken`, `[FromServices]`, keyed services and `[SkipValidation]`), validates every one through
  `IZodSchemaFactory.TryGetUntypedValidator`, keys errors like Microsoft.Extensions.Validation (complex parameters
  unprefixed, sequences and root errors prefixed with the parameter name) and adds 400
  `application/problem+json` response metadata for OpenAPI. `ZodEndpointValidationOptions` (`ThrowIfNoValidator`,
  `OnBindingFailure`, `ProducesStatusCode`) and `ZodBindingFailureBehavior`.
- **`ZodTypedResults.ValidationProblem(result|exception, HttpContext)`** (typed `ValidationProblem` for
  `Results<…>` unions) and **`ZodTypedResults.Problem(...)`** (`ZodProblemDetailsResult`, keeps the computed status).
  New `ToValidationProblem(HttpContext)`, `ToHttpValidationProblemDetails(HttpContext)` and
  `ZodProblemDetailsOptions` overloads.
- **`ZodProblemDetailsOptions`:** `PathNaming` (`ZodPathNaming.MemberName` default, or `Json` from the minimal-API
  `JsonOptions` naming policy), `IncludeIssues`, `MergeParametersIntoExtensions` (default `true`),
  `ProjectDataAnnotationsIssues`, `ErrorTypeResolver`, `DefaultStatusCode` and `StatusCodeOverride`.
- **`ValidationIssue` serializes a Zod-style shape:** `path` (dotted key), `segments` (strings and integer
  indexes), `code`, `category`, `origin`, typed `minimum`/`maximum` from `Lower`/`Upper` (numbers, or ISO-8601 for
  dates), `inclusive`, `message`, safe camel-cased `parameters` and nested `unionErrors`. The CLR `Path`,
  `Minimum`, `Maximum` and `Parameters` members are kept, unserialized.
- **Native AOT:** the ProblemDetails payload has source-generated JSON metadata, added to the minimal-API
  `JsonOptions` by `AddZodSharp`/`AddZodSharpProblemDetails`.
- **`IZodMessageFormatter` and `DefaultZodMessageFormatter`** (core). The writer renders messages through the
  registered formatter in `CultureInfo.CurrentUICulture`; without one, messages are used as produced.
- **`ZodPathNaming`** (core): `MemberName` or `Json`, shared by every host.
- **Options validation on .NET 11:** `ZodSchemaOptionsValidator<T>` implements `IAsyncValidateOptions<T>`, so
  `ValidateOnStart()` runs asynchronous and service-aware rules (in a scope); `OptionsBuilder<T>.AddZodSchemaValidator`
  registers through `Validate<TValidator>()`. New `AddZodSchemaValidator(missingValidatorBehavior, validateOnStart,
  configurationSection)` overload, `BindAndValidateWithZod(section)` (AspNetCore package) and
  `ZodValidateOptionsResult` (the failure with its full `Errors`). `Microsoft.Extensions.Options` 11 on net11.0.
- **Examples.MinimalApi sample** (`src/src/Examples.MinimalApi`, `net11.0`, not packed), with its `[ZodSchema]`
  request models in `src/src/Examples.Shared` for reuse by later samples. It shows generated trim-safe registration
  (`AddZodSharp{Assembly}Schemas()`), route groups validated by the .NET 11 pipeline (`AddZodSharpValidation()`), the
  `WithZodSharpValidation()` filter, a manual `ZodTypedResults.ValidationProblem`, a service-aware async uniqueness
  rule, async options validation on start, French messages, the OpenAPI transformers and an `.http` file. It
  publishes with `PublishAot=true` and zero IL2xxx/IL3xxx warnings: a new `aot-sample` PR job publishes it for
  `linux-x64` with `-warnaserror` and smoke-tests the native binary, `just publish-aot-sample` runs the same publish
  locally, and `Examples.MinimalApi.IntegrationTests` starts it in memory.
### Breaking behaviour changes

- **`[Required]` on a string now matches `RequiredAttribute`** (source generator, defect G1). The generated
  check was inverted: the default (`AllowEmptyStrings = false`) only rejected `null`, so `""` passed, while
  `AllowEmptyStrings = true` rejected `""`. The default now rejects `null`, `""` and whitespace-only text, and
  `AllowEmptyStrings = true` rejects only `null`. *Migration:* a model that relied on `""` passing a bare
  `[Required]` must either supply a value or set `AllowEmptyStrings = true`.
- **Generated error messages are formatted at runtime** from a named template kept in
  `ValidationError.MessageFormat`, with `displayName`, `member`, `declaringType` and the rule's values in
  `Parameters`. Attribute-specific texts are unchanged; positional placeholders in a custom `ErrorMessage` keep
  their DataAnnotations meaning. Shared failures now use the runtime schemas' identity:
  - `[Required]`: `"{displayName} is required"` (was `"Required field '{0}' is null"`), category `invalid_type`.
  - A null non-nullable member, a null root and a rejected null element: `"Expected {expected}, received null"`
    (was `"Field '…' cannot be null"`, `"Value cannot be null"`, `"Field '…' cannot contain null …"`).
  - `ApplyAnd`/`ApplyOr`/`ApplyRefine` failures: code `refinement_failed`, category `custom` (was
    `validation_failed`).
  - `[StringLength]`/`[Length]` on a string report the runtime rules' code `invalid_length` (was
    `too_small`/`too_big`), with the direction in `Category` and both bounds.
- **Typed generated paths.** Collection and dictionary errors are located with index and key segments
  (`Items[0].Name`, `Map["a.b"].Value` from `FullPath.ToDottedString()`); keys are rendered with the invariant
  culture. Nested re-pathing keeps every field of the nested error.
- **`[Range]`** reports `Category` `too_small`/`too_big`, origin from the value's Zod type and `Lower`/`Upper`
  with `MinimumIsExclusive`/`MaximumIsExclusive` honoured. **`[Length]`'s custom message** binds `{1}` to the
  minimum and `{2}` to the maximum, like `LengthAttribute` (it was swapped).
- **Generated options validators** report `"{path}: {message} ({code})"` per failure (was the message only).
- **Async-only inputs fail loudly**: a synchronous `Validate` of a type with an `AsyncValidationAttribute` member or
  `IAsyncValidatableObject` throws `InvalidOperationException`.
- **Nested types' async rules run**: `ValidateAsync` now awaits nested schemas, so a nested type's custom async
  method reports its errors (under the member's path) where they were silently dropped.
- **Index paths.** `ZodArray` and `ZodTuple` element errors are located with typed index segments; the legacy
  `ValidationError.Path` renders them as `"0"` (was `"[0]"`), matching Zod. `FullPath.ToDottedString()` renders
  `Items[0].Name`.
- **Tuple element errors are prefixed with their index** (they were reported at the tuple root).
- **Null messages** are `"Expected {type}, received null"` for strings, arrays, records, objects, enums, tuples and
  discriminated unions (were `"Expected array, but got null"` and similar).
- **Invalid-type messages use Zod type names** (`"Expected number, received string"`), never CLR names such as
  `Int32`, `Double` or `JsonElement`; `expected`/`received` parameters carry the same names.
- **Missing fields:** `ZodObject` reports `"{key} is required"` (was `"Required field '{key}' is missing"`), category
  `invalid_type`. `RequiredRule` reports `"Value is required"` (was `"Field is required"`) with the shared
  `"{displayName} is required"` template as `MessageFormat`; its `{0}` placeholder is now the display name.
- **Unrecognized keys:** a strict object reports one `unrecognized_keys` issue at the object's path with a `keys`
  parameter (was one `unrecognized_key` issue per key, at the key's path).
- **Unions** report per-option issues under `unionErrors` (`ImmutableArray<ImmutableArray<ValidationError>>`) instead
  of a flattened mutable list under `errors`; typed and native unions now keep them (they were discarded) and name
  the options with Zod type names. Category `invalid_union`.
- **Discriminated unions** report `missing_discriminator`/`invalid_discriminator` at the discriminator's path with
  `discriminator` and `options` parameters, list options in declaration order, and no longer echo the received
  discriminator value.
- **Enums and literals** no longer echo the received value (`"Expected one of: a, b"`, `"Expected literal 1.5"`);
  `ZodEnum` lists its options in ordinal order; `ZodNativeEnum` reports exactly what `EnumRule` reports.
- **Array size messages:** `Min`, `Max` and `Length` keep separate custom messages (a shared one reported the wrong
  message), and the defaults report the count (`"Array must have at least 2 elements, but got 1"`).
- **Default rule messages no longer contain the input** (email, credit card, JWT, phone, compare, starts/ends
  with, includes, regex, UUID/GUID and every other string format, allowed/denied values, null-or-non-whitespace).
  A custom message can still use the positional placeholders. `UUIDRule` custom and default messages now receive
  the same arguments (`{0}` value, `{1}` version as an integer); `RegexRule`'s `{0}` is the pattern, as documented.
- Rules no longer store their default message as the custom message (`message.Or(MessageFormat)`), so a rule built
  without a message and `default(TRule)` behave the same.
- `ZodIntersection` reports an issue raised identically by both sides once.
- Refinement and `SuperRefine` issues carry category `custom`; `ZodTransform` failures carry category `custom`.


- **JSON read failures use one code, `invalid_json`.** `DeserializeAndValidate`/`DeserializeAndValidateAsync`
  in both JSON packages used to report `json_error` (with the serializer's exception text in the message) or
  `deserialization_failed` (for a `null` document). Every failure to read the JSON now reports a single
  `invalid_json` error (`SystemTextJsonExtensions.InvalidJsonCode`, `NewtonsoftJsonExtensions.InvalidJsonCode`)
  with category `invalid_type` and the fixed message `"The JSON value could not be converted to {expected}."`,
  where `expected` is a Zod type name. The serializer's message is no longer surfaced, because it names CLR
  types and can quote the input.
- **Validating converters throw a structured exception.** The System.Text.Json converter throws
  `ZodJsonValidationException` (a `JsonException`) and the Newtonsoft converter throws
  `ZodNewtonsoftJsonValidationException` (a `JsonSerializationException`). Both still carry the
  `"Validation failed: …"` message, so existing `catch` blocks keep working, and `DeserializeAndValidate` now
  returns the schema's full errors instead of one flattened message.
- **JSON Schema import messages describe JSON kinds.** Type mismatches read `"Expected string, but got number"`
  instead of naming `JsonElement`/`JToken`/CLR types, `minItems`/`maxItems` failures read
  `"Array must have at least 2 elements, but got 1"` (matching `ZodArray`), and a literal mismatch no longer
  echoes the received value.

- **ProblemDetails messages from an `ErrorType.MessageFormat`** replace the error's message only when every
  placeholder resolves (from parameters or the built-ins `path`, `code`, `origin`, `minimum`, `maximum`,
  `displayName`); a partly-filled template used to be returned with `{placeholders}` left in.
- **`StatusCodeSelector` wins.** It runs after the highest `ErrorType.HttpStatus` is computed (it was overridden by
  it, contrary to the docs).
- **Parameters are merged into the top-level extensions on every overload** (they were merged only by the
  registry/lookup overloads), and only safe scalar values are serialized. A parameter whose camel-cased name is a
  reserved member (`type`, `title`, `status`, `detail`, `instance`, `errors`, `issues`, `traceId`) is written as
  `param_{name}`. `issues[].parameters` keys are camel-cased (they kept the raw key).
- **`issues[].path` is the dotted key** (`"Items[0].Quantity"`, was a string array); the array is `segments`, with
  integer indexes. `minimum`/`maximum` come from the typed bounds.
- **`traceId` is on every path** and matches ASP.NET Core's writer: `Activity.Current?.Id`, else
  `HttpContext.TraceIdentifier` (the exception handler used `TraceIdentifier` only).
- **The endpoint filter honours `ZodProblemDetailsOptions` and `ErrorTypeRegistry`** (it always answered 400), writes
  through `IProblemDetailsService`, and validates every matching argument (only the first was validated).
  `WithZodSharpValidation<T>()` resolves the validator when the endpoint is built and fails then - naming the
  handler and parameter - when no factory or validator is registered (it threw on every request).
- **MVC `ValidationProblemDetails` copies every extension** (only `issues` was copied).
- **`ToValidationProblem()` returns a `ProblemHttpResult` that keeps the computed status** (it was always 400).
- **Options failures name the setting:** each failure reads `{name}: {path}: {message} ({code})`, with the
  configuration key (`Smtp:Servers:0:Host`) when a section was recorded; the name is omitted for the default
  options name. `OptionsBuilder<T>.AddZodSchemaValidator()` validates only the builder's options name (it validated
  every name).
### Fixed

- Source generator: a custom `ErrorMessage` containing `{1}` or a stray brace no longer fails the build or throws at
  validation time (G2, G14); `[Display(ResourceType)]` is honoured (G19); `[JsonPropertyName]` is available to
  hosts through `JsonNames`/`ToJsonNamedPath` (G3); allowed/denied enum values are listed by name (G17); custom
  string rules and enum rules report an origin (G16, G18); `IValidatableObject` is no longer ignored (G23);
  cyclic graphs no longer overflow the stack (G24); a rule without constructor arguments no longer breaks the
  generated JSON Schema.
- Re-pathing an element, field, catchall, record value or tuple element error no longer drops its origin,
  bounds, inclusivity, category or message format (`WithPathPrefix` everywhere).
- `ZodArray.Min(2).Max(5)` reported a too-small failure with the maximum's message.
- A null tuple element is handed to the element schema when the element type can hold `null`.
- `GreaterThanRule`/`LessThanRule` report exclusive bounds; `SafeIntegerRule` reports `invalid_type` for a
  fraction and `too_small`/`too_big` by direction (was always `too_big`).
- `ZodString` span validation reports the rule's full structured identity.
- `default(IPAddressRule)` no longer formats a `null` message.



- **JSON read failures keep their location.** The serializer's path becomes the error's `FullPath`
  (`$.items[1].age` and Newtonsoft's `items[1].age` both become `items[1].age`), and its line and position
  become the `line` and `position` parameters.
- **System.Text.Json `NotSupportedException` and `InvalidOperationException`** thrown while deserializing are
  reported as `invalid_json` instead of escaping `DeserializeAndValidate`.
- **Validating-converter errors keep every field and their path**, located under the converter's JSON path
  (`Owner.Name` for a failing nested `Owner`), including when validating converters are nested.
- **Newtonsoft `DeserializeAndValidateAsync` validates asynchronously.** It called the synchronous `Validate`,
  so asynchronous refinements never ran; it now awaits `ValidateAsync`.
- **JSON Schema import paths and metadata.** Array element and record value errors are re-pathed with typed
  index and key segments and keep every field (origin, bounds, category, parameters); `minItems`/`maxItems`
  failures carry origin `array`, both bounds, inclusivity and the `too_small`/`too_big` category, like
  `ZodArray`. A Newtonsoft `JValue` is no longer treated as an empty array.

- **ASP.NET Core (D1-D12):** dotted ProblemDetails keys with escaping, complete-template-only message formatting,
  consistent parameter handling, reserved-name protection, filter/registry parity, `FormatMessages` honoured,
  selector precedence, full MVC copies, a safe issue DTO, .NET 11 binding failures passed through (a route, query,
  header or form binding failure keeps ASP.NET Core's 400 unless `OnBindingFailure = ConvertToProblemDetails`), and
  every argument validated with a build-time registration check.
- **Options (O1):** failures keep their path, code and options name; asynchronous rules run at startup on .NET 11.
## 2.4.1

### Fixed

- **Nullable value-type scalars.** `Purview.ValueObjects` 1.2.1 applies `Nullable = true` to a value type
  by wrapping it in `Nullable<T>`. ZodSharp only applied the option to reference types, so
  `[Scalar<DateTimeOffset>(Nullable = true)]` closed its rules over `DateTimeOffset` while the value object
  implemented `IScalarValueObject<TSelf, DateTimeOffset?>`, which failed the generated adapter's constraint
  (`CS0315`). The automatic scalar forms now mirror the value-object generator for both reference and value
  types.
- **Rules on a nullable value scalar are null-guarded and unwrapped.** A type-level or property-level rule
  written against the underlying value is resolved against the non-nullable value type and run only when the
  value is present, mirroring how a `Nullable<T>` property is validated. This also lets constrained rules
  (`MinValueRule<T>`, numeric rules, comparison rules, `EnumRule<T>`, ...) resolve, because
  `Nullable<T>` does not implement their constraints. `[RequiredZod]` still rejects an absent value, and the
  exported JSON Schema no longer advertises a `null` branch when a required rule is present.
- **Nullable scalar properties validate their unwrapped value.** A property declared as `T?` where `T` is a
  scalar value object passed the `T?` value to the nested `TSchema.Validate(T)` call (`CS1503`). The nested
  schema is now invoked with `T` inside the null guard.

### Upgrade notes

- A nullable value scalar now accepts `null` unless it carries `[RequiredZod]`; a rule written against its
  underlying value is skipped for `null` and receives the non-nullable value otherwise. This matches the
  behaviour of a nullable `Nullable<T>` property.

## 2.4.0

### Added

- **Dictionary validation.** A property whose type implements `IDictionary<TKey, TValue>` or
  `IReadOnlyDictionary<TKey, TValue>` (`Dictionary`, `SortedDictionary`, `ConcurrentDictionary`,
  `ImmutableDictionary`, custom implementations) is now a map rather than a sequence. The generated validator
  iterates the entries, validates every **value** against the value type's nested schema — discovered
  recursively, with or without its own `[ZodSchema]` — and reports a failure at the entry's key
  (`["Variants", "blue"]`), matching Zod's `z.record` issue path. Keys are used only in error paths.
  `AllowNulls`, and therefore `[CollectionBehavior]`, `[ZodSchema(AllowNulls = ...)]`, the assembly default
  and `ZodSharpAllowNulls`, now govern a map's values. `[Length]`/`[MinLength]`/`[MaxLength]` count entries.
- **Dictionary JSON Schema export.** A dictionary exports as
  `{ "type": "object", "additionalProperties": <value> }` instead of an array of pairs, and its size
  constraints export as `minProperties`/`maxProperties` (new `JsonSchemaDefinition.MinProperties` and
  `MaxProperties` members). A nullable value exports a `null` branch on `additionalProperties`.
- **Record JSON Schema export and import.** `Z.ToJsonSchema(Z.Record(valueSchema))` now exports the same
  object/`additionalProperties` shape, and `Z.FromJsonSchema` imports an object with `additionalProperties`
  as a record in both JSON integration packages (`additionalProperties: true` treats every member as `any`).
  See [JSON Schema Export](docs/wiki/JsonSchema-Export.md#dictionaries) and
  [JSON Schema Import](docs/wiki/JsonSchema-Import.md#records).
- **`IEnumerable<T>` properties validate their elements.** A property declared as the enumerable interface
  itself now resolves its element type, validates elements and exports its `items` schema, like `T[]`.

### Fixed

- **Collection element resolution for dictionaries.** The element type was taken from whichever
  `IEnumerable`-derived interface appeared first, so a `Dictionary<string, T>` resolved its **key** type
  (`string`) as the element. The generated validator then emitted `if (item is null)` over a
  `KeyValuePair<string, T>` (`CS0037`), exported `items: {"type":"string"}`, and skipped the value schema
  entirely. The per-entry type now comes from `IEnumerable<T>` / the dictionary value.
- **`ZodRecord` null values.** A null value is now the value schema's decision: `Z.Record(Z.String())`
  rejects it (matching `z.record(z.string(), T)`), while a nullable value schema accepts it. Previously null
  values were silently accepted and dropped. Re-keyed errors also preserve the inner issue's `origin`,
  `minimum`, `maximum`, `inclusive` and `category`.
- **Nullable value-type elements export their base schema.** `List<int?>` / `Dictionary<string, int?>` now
  export `integer` (plus the `null` branch where allowed) instead of the empty schema `Nullable<int>`
  produced.

### Upgrade notes

- A `Dictionary<TKey, TValue>` property is now validated per value rather than per key. If a dictionary was
  relying on the old (incorrect) behaviour, add the value type's `[ZodSchema]` where desired; no source change
  is required otherwise.
- `Z.Record(nonNullableValueSchema)` now rejects a null dictionary value. Use a nullable/optional value
  schema to keep accepting one.

## 2.3.0

### Added

- **Temporal `[NonSentinel]` JSON Schema mappings.** `NonSentinelRule<DateOnly>`, `<TimeOnly>`, `<DateTime>`
  and `<DateTimeOffset>` now export exact `pattern` constraints that reject the System.Text.Json wire forms of
  their bounds, so no temporal rule appears under `x-zodsharp-unsupported`. The `DateTime` mapping covers the
  deterministic `Unspecified`, `Utc` and zero-offset forms. The `DateTimeOffset` pattern is offset-aware: it
  correlates the local time with the offset and rejects every RFC 3339 representation of both instant bounds.
  `Z.Date().NonSentinel()` also exports `type: "string"`, `format: "date-time"` (previously it fell back to
  the empty schema). See
  [JSON Schema Export](docs/wiki/JsonSchema-Export.md#temporal-sentinels-and-the-datetimeoffset-wire-form).
- **Optional exact UTC `DateTimeOffset` contract.** `DateTimeOffsetSerialization.Utc` narrows
  `[NonSentinel] DateTimeOffset` to a zero-offset wire form. Configure it per project with
  `<ZodSharpDateTimeOffsetSerialization>Utc</ZodSharpDateTimeOffsetSerialization>` (for example in
  `Directory.Build.props`), per assembly with
  `[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = ...)]`, or per type/property with
  `[DateTimeOffsetBehavior(...)]`. Precedence is property, type, assembly, MSBuild, then `PreserveOffset`.
- **`UtcDateTimeOffsetConverter` and `PreserveOffsetDateTimeOffsetConverter`** in both JSON integration
  packages (`ZodSharp.Json.Serialization.SystemTextJson` and `...NewtonsoftJson`). The UTC converter writes
  UTC and rejects a value without an explicit `Z`/zero offset (or with a non-zero offset) on read, which is what
  keeps it in agreement with the UTC schema; the
  preserve-offset converter supports overriding a project-wide UTC converter. The Newtonsoft converters
  require `DateParseHandling.None` so they can inspect the original offset;
  `UseZodSharpUtcDateTimeOffset()` / `UseZodSharpPreserveOffsetDateTimeOffset()` configure both together.
- `NonSentinelRule<T>.DateTimeOffsetUtcJsonSchemaPattern` and a
  `JsonSchemaRules.Apply(schema, rule, DateTimeOffsetSerialization)` overload for manual schemas.
- **`Z.DateTimeOffset()` and runtime manual-schema options.** `ZodDateTimeOffset` is the fluent counterpart to
  a generated `DateTimeOffset` property, with the same `NonSentinel()` override.
  `ToJsonSchemaOptions.DateTimeOffsetSerialization` lets a hand-built schema select the default offset-aware
  mapping or the narrower UTC contract. `JsonSchemaRules.TimeOnlyPattern` is the local-time base pattern used
  for a generated `TimeOnly` property.

### Changed

- `NonSentinelRule<T>` no longer reports `x-zodsharp-unsupported` for a type whose sentinel it does not
  inspect. Such a rule always passes, so an unconstrained schema is complete.
- A generated `TimeOnly` property now exports a local-time `pattern` instead of `format: time`. JSON Schema's
  `format: time` requires an RFC 3339 offset, which System.Text.Json never writes for `TimeOnly`; strict
  consumers previously rejected every value.

### Known limitations

- A `DateTimeKind.Local` sentinel can carry a machine-specific offset; use `Unspecified` or `Utc` for a
  portable `DateTime` contract.
- The optional UTC contract deliberately rejects non-UTC input, including otherwise valid timestamps, because
  normalizing it on read would reintroduce the offset-equivalence gap.

## 2.2.0

### Added

- **Collection null items are rejected by default.** A generated schema now rejects a `null` element in a
  collection property with `invalid_type` at the element's own path (`["Items", 0]`), matching Zod's
  per-element issue path. Previously null elements were silently skipped. Control it per property with
  `[CollectionBehavior(AllowNulls = true/false)]`, per element type with `T?`, per type with
  `[ZodSchema(AllowNulls = ...)]`, per assembly with `[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]`,
  or with the `ZodSharpAllowNulls` MSBuild property. The most specific statement wins; the built-in default is
  to reject. A collection that allows null elements exports a `null` branch on its `items` JSON Schema, and
  `[CollectionBehavior]` on a property that cannot hold null elements is reported as `ZODSGEN046`.
- **The `ZodSharp*` MSBuild switches are declared compiler-visible** in the package's `buildTransitive` props,
  so `ZodSharpGenerateJsonSchemas`, `ZodSharpEnumSerialization`, `ZodSharpEnumNamingPolicy`,
  `ZodSharpAutoGenerateOptionsValidators`/`ZodSharpAutoGenerateOptionsValidatorSuffixes` and the new
  `ZodSharpAllowNulls` reach the generator in a consumer build. They previously took effect only in this
  repository's own tests, because nothing declared them as `CompilerVisibleProperty`.

## 2.1.0

Consolidates the `2.0.1`, `2.0.1.1` and `2.0.2` incremental releases: upgrading from `2.0.0` goes straight to
`2.1.0`, and the notes below cover that whole arc. The analyzer catalogue records each diagnostic under the
release that introduced it, so `AnalyzerReleases.Shipped.md` shows `ZODSGEN037`–`ZODSGEN044` under
`## Release 2.0.2` and `ZODSGEN045` under `## Release 2.1.0`.

> **Upgrading from `2.0.0`?** Start with [Upgrade notes](#upgrade-notes). A few rule and attribute APIs were
> renamed and standardised across these releases, so some usages need a mechanical edit before they compile.
> Each note names the old form, the new form, and the edit to make.

### Upgrade notes

- **A rule's error-identity constants are now `ErrorCode` and `MessageFormat`.** Every validation rule exposes
  its error identity as public `const string ErrorCode` and `const string MessageFormat`.
  - *Previously:* a rule's identity constant was named `Code`.
  - *Now:* it is named `ErrorCode`.
  - *To upgrade:* rename references from `SomeRule.Code` to `SomeRule.ErrorCode`. `ZODSGEN042` reports
    source-declared rules that do not follow the convention, so the compiler points at each one.
- **Rule constructors, extension methods and generated attributes now follow one shape.** A generated rule
  attribute carries the rule's *value* parameters as constructor arguments: a parameter declared without a
  default becomes a required positional argument, a parameter with a default keeps it, and
  `message`/`code`/`origin` remain properties.
  - *Previously:* a required value could be supplied by property name — the property derived from the rule's
    parameter, so `MinValueRule<T>(T minValue, …)` was settable as `[MinValue(MinValue = 3)]`.
  - *Now:* required values are positional — `[MinValue(3)]`, `[Regex("^[a-z]+$")]`.
  - *To upgrade:* move a required value from a property assignment to a positional argument. Defaulted values
    may still be set by property name.
- **`NonSentinelRule<T>.MessageFormat` is reworded, and `{0}` now describes the sentinel.**
  - *Previously:* `{0}` interpolated the raw value.
  - *Now:* the format is `Value has a sentinel value, but got {0}`, and `{0}` is a description — `an empty
    GUID`, `DateTime.MinValue`, `an empty string`, and so on.
  - *To upgrade:* update any code or test that asserts against the old message text. Asserting against
    `NonSentinelRule<T>.ErrorCode` instead keeps the assertion stable across future wording changes.
- **`ZodSchemaGeneratedAttribute` now takes `(Type targetType, Type validatorType)`.** See
  [`RegisterFromAssembly` is now genuinely trim- and AOT-safe](#changed--trimming-and-native-aot) for why.
  - *To upgrade:* nothing, in the normal case — the attribute is generator-emitted and regenerates on rebuild.
    Only a hand-written usage needs the second argument.

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
  leaving a dangling `{Type}Schema` reference. Recorded under `## Release 2.0.2`.
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
  - `ZodSchemaGeneratedAttribute` now takes `(Type targetType, Type validatorType)`. The attribute is
    generator-emitted, so it regenerates on rebuild; only hand-written usages need updating. Listed under
    [Upgrade notes](#upgrade-notes).
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

- **NuGet package validation.** Every packable project now enables `EnablePackageValidation` against the
  previous stable release (`2.0.2`), so an unintended change to the public API fails `dotnet pack` rather than
  shipping. All four packages validate clean with no suppressions — the rule and attribute changes under
  [Upgrade notes](#upgrade-notes) landed in the `2.0.x` releases, so they are already part of the baseline, and
  `2.1.0` is additive over it. This is the guard that was missing when those changes went out.
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

### Fixed — analyzer release tracking guard

- **`AnalyzerReleaseTrackingTests` was passing vacuously, and `ZODSGEN045` was never recorded as shipped.** The
  guard that asserts the unshipped file declares no rules matched a rule row only in the pipe-delimited form
  (`| ZODSGEN045 |`), but `AnalyzerReleases.Unshipped.md` is written without the leading pipe — so a rule parked
  there parsed as *no rules at all* and the assertion compared two empty sets. `ZODSGEN045` sat unshipped with a
  green suite. The row pattern now accepts both table styles, `ZODSGEN045` is recorded under a new
  `## Release 2.1.0` block, and the test asserts that block exists. The guard now fails when a diagnostic is left
  unshipped, which is the only thing it was ever meant to do.

### Added — the vitest suites run from the .NET test run

- **`just test` now proves both halves of the cross-platform guarantee.** The TypeScript/Zod suites were driven
  only by the dedicated `cross-platform` CI job, so a Zod-side regression could not fail a local run — and a gap
  in that job's own wiring went unnoticed for a whole release. `Repository.UnitTests` now runs each
  `tests/ts/*.test.ts` suite as a child process through **CliWrap** (one test per suite, so a failure names the
  file and the child's stdout/stderr becomes the assertion message). The CI job remains the authoritative gate,
  because it controls producer ordering and installs bun from scratch.
- `VitestSuites_GivenEveryTestFileOnDisk_AreAllCovered` fails when a vitest suite has no entry in the C# list, so
  a new suite cannot end up driven by the CI job alone — the same class of gap as the parity producer above.
- **These tests never skip, and they run on bun or node.** A skipped test proves nothing, so the runner is
  resolved per run — bun when it is on PATH, otherwise node against vitest's own entry point — and the test
  **fails** when neither is available, or when the install or the run fails. `node_modules/` is gitignored, so
  vitest is installed first when it is missing (`bun install --frozen-lockfile`, or `npm install` when only
  node is present). The node path deliberately avoids `npx` and `node_modules/.bin`: the `.bin` shims belong to
  whichever package manager installed the tree, and on Windows `npx` is a `.cmd`/`.ps1` shim that cannot be
  started as a child process.
- Every path is verified, not just the green one: an injected parity violation fails the .NET test with the
  vitest output attached and names the runner that produced it; an unlisted `.test.ts` file fails the coverage
  guard; the node fallback is confirmed to execute with bun removed from PATH; and with both removed the suite
  tests fail rather than skipping.

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
- **The CI cross-platform job never produced the Guid/UUID parity artifacts, so `bun run test` failed there.**
  The job selects the C# producers with a treenode filter, and carried only one —
  `/*/ZodSharp.Json/*CrossPlatformTests/*`, which writes `cross-platform/output/`. The parity artifacts
  `tests/ts/guid-uuid-parity.test.ts` reads come from a *second* producer added with the Guid/UUID export work,
  in a different assembly, namespace and class
  (`ZodSharp.SourceGenerators/ZodSchemaGeneratorTests/JsonSchema_GivenGeneratedGuidSchemas_WritesZodParityArtifacts`),
  which that filter does not match. `cross-platform/guid-parity/` was therefore always empty in CI and the
  parity suite failed every run. The job now runs a step per producer. They cannot be combined into one
  OR-ed filter: `a|b` is not honoured as a top-level alternation — it silently selects a broader set and still
  skips the parity producer, which was verified when fixing this. `just test` was unaffected, because it runs
  the whole suite.

## 2.0.0

First stable release. See the
[`v2.0.0`](https://github.com/purview-dev/zodsharp/releases/tag/v2.0.0) release notes, and
[What's new in v2](README.md#whats-new-in-v2) for the summary of the `Purview.*` package IDs, the JSON
integrations, JSON Schema interoperability, the ASP.NET Core `ProblemDetails` integration and the expanded
DataAnnotations support.

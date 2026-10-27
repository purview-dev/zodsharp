# Core Concepts

## The validation pipeline

Every schema derives from `ZodType<TOutput, TInput>` (namespace `ZodSharp.Core`). Validation is a two-phase pipeline:

1. **ParseInternal** — each schema overrides this hook to perform its type check and traversal (rejecting `null` where not allowed, coercing types, walking objects/arrays/tuples/unions, producing structured failures).
2. **Rules** — on success, the accumulated `IValidationRule<TOutput>` structs are evaluated. A failing rule emits a `ValidationError` carrying the rule's code (`too_small`, `too_big`, `not_multiple_of`, `not_finite`, `invalid_string`, `invalid_type`, `invalid_value`, …; `validation_failed` for custom rules without a code) and its structured identity: the Zod issue code as `Category`, an `Origin`, typed `Lower`/`Upper` bounds and parameters (see [Error fields](#error-fields)). Each rule exposes its reported code and message as public `const string ErrorCode` / `MessageFormat` constants, so tests can assert against the rule instead of duplicating literals (a rule that omits them is reported as `ZODSGEN042` — see [Custom Rules](Custom-Rules.md)).

```csharp
ValidationResult<TOutput> result = schema.Validate(value);
```

## Validate, SafeParse, Parse, ValidateAsync

| Member | Behaviour |
|---|---|
| `Validate(TInput value)` | Returns a `ValidationResult<TOutput>`. Never throws. |
| `SafeParse(TInput value)` | Alias of `Validate`. |
| `Parse(TInput value)` | Returns the validated `TOutput`, or throws `ZodException` on failure (via `GetValueOrThrow()`). |
| `ValidateAsync(TInput value, CancellationToken)` | `ValueTask` wrapper around `Validate`. The pipeline is synchronous; the token is observed before validation and throws `OperationCanceledException` when already cancelled. Genuinely async work only happens in the source generator's custom async validation. |

## ValidationResult<T>

`ValidationResult<T>` is a `readonly record struct` in `ZodSharp.Core`:

- `bool IsSuccess` — annotated `[MemberNotNullWhen(true, nameof(Value))]`.
- `T? Value` — the validated value; valid only when `IsSuccess`.
- `ImmutableArray<ValidationError> Errors` — populated on failure.

Static factories: `Success(value)`, `Failure(ValidationError)`, `Failure(ImmutableArray<ValidationError>)`, `Failure(IEnumerable<ValidationError>)`, and `Merge(lhs, rhs)` (succeeds only if both succeed; concatenates errors).

## ValidationError

`ValidationError` is a `readonly record struct` carrying machine-readable metadata about one issue. Equality
compares every field, including the path segments and parameters by value, so two identical errors are equal.

## Error fields

| Member | Meaning |
|---|---|
| `Code` | ZodSharp's specific identity: a rule's `ErrorCode` (`too_small`, `invalid_string`, `invalid_length`, …) or a schema code from `ZodIssueCodes` (`invalid_type`, `missing_field`, `invalid_union`, `unrecognized_keys`, `refinement_failed`, `transform_error`, …). The same failure reports the same code whichever producer reports it (runtime schema, generated validator, JSON Schema import). |
| `Category` | The Zod v4 issue code (`ZodIssueCategories`): `invalid_type`, `too_small`, `too_big`, `invalid_format`, `not_multiple_of`, `unrecognized_keys`, `invalid_union`, `invalid_key`, `invalid_element`, `invalid_value`, `custom`. Every built-in error sets it, so a client can treat ZodSharp and Zod issues alike. |
| `Message` | The rendered message, formatted with the invariant culture. Built-in messages never quote string input (see [PII](#pii)). |
| `MessageFormat` | The template that produced `Message`, with named placeholders (`"{displayName} is required"`), or `null` when the message was supplied literally. Hosts re-format it in the request culture (see [Localization](Localization.md)). |
| `FullPath` | The authoritative, typed location (`ZodPath`); see [Paths](#paths). |
| `Path` | The legacy `ImmutableArray<string>` projection of `FullPath`: members and keys as-is, indexes as digits (`["Items", "0", "Name"]`). Derived from `FullPath`, so the two never disagree. |
| `Origin` | The kind of value that failed: `string`, `number`, `bigint`, `date`, `array`, `object`, `enum`, … |
| `Lower` / `Upper` | `ErrorBound?` - the bound's `Value` (any comparable: a number, decimal or date) and whether it is `Inclusive`. `GreaterThan`/`LessThan` and exclusive `[Range]` bounds report `Inclusive = false`. |
| `Minimum` / `Maximum` / `Inclusive` | The legacy integer bounds of size issues, kept for existing consumers. `Lower`/`Upper` fall back to them. |
| `Parameters` | `ErrorTypeParameters?` (also an `IReadOnlyDictionary<string, object?>`) with the values behind the message. Well-known names are constants on `ZodIssueParameters`: `expected`, `received`, `options`, `keys`, `key`, `count`, `format`, `divisor`, `pattern`, `prefix`, `suffix`, `includes`, `version`, `discriminator`, `displayName`, `member`, `declaringType`, `unionErrors`. |

```csharp
var error = Z.Number().Gt(5).Validate(1).Errors[0];
// error.Code     == "too_small"
// error.Category == ZodIssueCategories.TooSmall
// error.Origin   == "number"
// error.Lower    == new ErrorBound(5d, Inclusive: false)
```

The shared identities live in `ZodSharp.Core`, so every producer reports a failure the same way:

| Type | Holds |
|---|---|
| `ZodIssueCodes` | The schema-level codes (`InvalidType`, `MissingField`, `RefinementFailed`, `InvalidUnion`, `MissingDiscriminator`, `InvalidDiscriminator`, `InvalidEnumValue`, `InvalidLiteral`, `InvalidTupleLength`, `UnrecognizedKeys`, `TooSmall`, `TooBig`, `TransformError`, …). |
| `ZodIssueCategories` | The Zod v4 issue codes used as `Category`. |
| `ZodMessages` | The default templates with named placeholders: `NullValue` (`"Expected {expected}, received null"`), `InvalidType` (`"Expected {expected}, received {received}"`), `Required` (`"{displayName} is required"`), `InvalidUnionOf`, `UnrecognizedKeys`, `ArrayTooSmall`, … |
| `ZodIssues` | Factories for the common errors: `NullValue`, `InvalidType`, `MissingField`, `RefinementFailed`, `InvalidUnion`, `CollectionSize`, `Formatted`, plus the `GetUnionErrors()` extension. |
| `ZodTypeNames` | Zod's type names (`string`, `number`, `bigint`, `boolean`, `date`, `object`, `array`, …) and `Of(Type)`/`OfValue(object?)`/`OfJsonKind(JsonValueKind)`, so CLR names such as `Int32` or `JsonElement` never reach a client. |

A failed union reports one `invalid_union` error whose per-option issues are available through
`error.GetUnionErrors()` (an `ImmutableArray<ImmutableArray<ValidationError>>`, stored under the `unionErrors`
parameter); see [Unions and Discriminated Unions](Unions-and-Discriminated-Unions.md).

Create custom issues with `ValidationError.Create(code, message, path, parameters, origin, minimum, maximum, inclusive, category)`;
`path` may be a `ZodPath`, an `ImmutableArray<string>` or (through the constructor) a `string[]`. Fields not in
the factory (`Lower`, `Upper`, `MessageFormat`, `Category`, `Origin`) are `init`-only, so set them with `with`:

```csharp
var error = ValidationError.Create("out_of_stock", "Not enough stock", ZodPath.Member("Quantity")) with
{
    Category = ZodIssueCategories.TooBig,
    Upper = ErrorBound.Including(10),
};
```

## Paths

`ZodPath` is an immutable sequence of typed `ZodPathSegment`s. Keeping the kind of each segment is what lets an
index, a dictionary key and a member be told apart afterwards, so every host renders a location the same way.

| Segment | Created with | Rendered |
|---|---|---|
| `ZodPathSegmentKind.Member` | `ZodPathSegment.Member("Items")` | `.Items` |
| `ZodPathSegmentKind.Index` | `ZodPathSegment.At(0)` | `[0]` |
| `ZodPathSegmentKind.Key` | `ZodPathSegment.Key("en")`, `ZodPathSegment.Key(42)` | `[en]`, or `["…"]` when ambiguous |

`ZodPathSegment.Key<TKey>(key)` renders the key with the invariant culture, so a path never depends on the
thread's culture.

`FullPath.ToDottedString()` is the canonical rendering, used for ProblemDetails keys, Microsoft.Extensions.Validation
paths, Blazor field mapping, `ZodException` messages and options failures. It matches
Microsoft.Extensions.Validation: members joined with `.` and indexes bracketed. Keys are bracketed too, and
quoted when they would otherwise be ambiguous - an all-digit key (which would read back as an index) or a key
containing `.`, `[`, `]`, quotes or whitespace. The root path renders as `""`.

| Path | Dotted |
|---|---|
| member `Items`, index `0`, member `Name` | `Items[0].Name` |
| member `Map`, key `en` | `Map[en]` |
| member `Map`, key `"0"` | `Map["0"]` |
| member `Map`, key `a.b`, member `Value` | `Map["a.b"].Value` |
| root | `` (empty) |

Other renderings and parsers:

| Member | Output for `Map["a.b"].Items[0].Name` |
|---|---|
| `ToDottedString(IZodPathNamer? namer = null)` | `Map["a.b"].Items[0].Name` |
| `ToJsonPointer(namer)` | `/Map/a.b/Items/0/Name` (RFC 6901 escaping) |
| `ToJsonPath(namer)` | `$.Map['a.b'].Items[0].Name` |
| `ToConfigurationKey()` | `Map:a.b:Items:0:Name` |
| `ToZodPath()` | `["Map", "a.b", "Items", 0, "Name"]` (Zod's issue shape: integers for indexes) |
| `ZodPath.ParseDotted(string)` | the inverse of `ToDottedString` (a bracketed all-digit value is an index, anything else a key) |
| `ZodPath.ParseJsonPath(string)` | System.Text.Json (`$.items[1].age`) and Newtonsoft (`items[1].age`) paths |
| `ZodPath.FromLegacy(IEnumerable<string>)` | legacy segments; `"0"` and `"[0]"` become indexes |

```csharp
var path = ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(0), ZodPathSegment.Member("Name"));

path.ToDottedString();                                                            // Items[0].Name
path.ToDottedString(ZodPathNamer.FromFunc(JsonNamingPolicy.CamelCase.ConvertName)); // items[0].name
```

**Naming.** Member segments hold C# member names (or object keys for `Z.Object()` schemas). `ZodPathNaming`
selects how hosts render them: `MemberName` (the default everywhere) keeps the C# name, which is what
Microsoft.Extensions.Validation, MVC model state and Blazor's `FieldIdentifier` use, so a ZodSharp type and a
DataAnnotations type in the same API produce the same key shape. `Json` renders JSON names; generated validators
supply them without reflection through `IZodJsonPathMapper` (`[JsonPropertyName]` first, then the naming policy).
Index and key segments are never renamed.

**Re-pathing.** `error.WithPathPrefix(ZodPathSegment)` and `error.WithPathPrefix(ZodPath)` return a copy located
under the prefix and keep every other field (origin, bounds, category, parameters, message format). They are the
supported way to nest an error; do not rebuild an error from its code and message.

> [!NOTE]
> The legacy `Path` projection renders an index as `"0"`, matching Zod. Runtime `ZodArray`/`ZodTuple` used to
> produce `"[0]"`; parse `FullPath` rather than the strings if you need to tell indexes and keys apart.

## Culture

Validation is culture-independent; only the host that shows a message to a user applies a culture.

| Data | Culture |
|---|---|
| `Code`, `Category`, `Origin`, parameter values, path key segments, bounds | Invariant, always |
| `Message` produced at validation time | Invariant (deterministic and stable across machines) |
| A message rendered by an `IZodMessageFormatter` at a host boundary | The culture the host passes - `CultureInfo.CurrentUICulture` in ASP.NET Core and Blazor |

`IZodMessageFormatter.Format(in ValidationError, CultureInfo)` is the hook; `DefaultZodMessageFormatter`
re-formats `MessageFormat` when every placeholder resolves and otherwise returns `Message`. The
`ZodMessageFormat` engine behind it never throws: unknown placeholders and unbalanced braces are written
literally, `{{`/`}}` escape braces, and named placeholders resolve from the parameters and then the built-in
fields (`displayName`, `member`, `path`, `code`, `category`, `origin`, `minimum`, `maximum`, `inclusive`; `displayName` is the last member of the path, or `Value` at the root). A
sequence parameter (an options or keys list) renders as its elements joined by `, `. See
[Localization](Localization.md).

## PII

Errors leave the process - in ProblemDetails, logs and UI - and the input may be a card number, a token or another
secret. Built-in default messages therefore never quote the input - formats, patterns, prefixes, allowed and
denied values, enums, literals, discriminators, numbers (`"Value must be between 1 and 10"`,
`"Number must be a multiple of 5"`, `"Expected integer, received float"`) and JSON read failures describe what was
expected, not what was received. Size rules state the count (`"…, but got 1"`), which is not the input.

`ZodErrorOptions.IncludeReceivedValues` (process-wide, default `false`) adds the raw input as the `received`
parameter of rule failures; with it off, rule failures carry no `received` parameter at all. It is for local
development and diagnostics only: **never enable it in production**. Built-in messages stay value-free either way;
a custom message can still use a rule's positional placeholders (the value is still passed, for example as `{1}`
of `MinValueRule` or `{2}` of `RangeRule`).

Every built-in rule reports its message template as `MessageFormat`, with named placeholders (`{minimum}`,
`{maximum}`, `{count}`, `{divisor}`, `{pattern}`, …) that resolve from the error, so a host can localize a runtime
schema's errors by template exactly as it does a generated validator's. A custom message is reported with its
positional placeholders renamed (`"At least {0}"` becomes `"At least {minimum}"`); the value's position becomes
`{received}`, which only resolves when `IncludeReceivedValues` is on, so a formatter falls back to `Message`.

## Depth limit

Generated validators walk the object graph with a depth counter. `ZodValidationDepth.MaxDepth` (process-wide,
default `ZodValidationDepth.DefaultMaxDepth`, `32`, matching Microsoft.Extensions.Validation's
`ValidationOptions.MaxDepth`) is the limit: a graph nested deeper - almost always a cycle - throws
`InvalidOperationException` instead of overflowing the stack. The root is validated at depth `0`.

## ZodException

`ZodException` (namespace `ZodSharp.Core`) is thrown by `Parse` and `GetValueOrThrow()`. It exposes `ImmutableArray<ValidationError> Errors`. Its `Message` lists up to five issues as `"{path}: {Message} ({Code})"` (the root path is shown as `(root)`) followed by `"and N more"`, and `ToString()` adds one line per error in the same form.

```csharp
try
{
    var value = Z.String().Min(3).Parse("A");
}
catch (ZodException ex)
{
    // ex.Message == "Validation failed with 1 error(s): (root): String must be at least 3 characters long, but got 1 (too_small)"
    foreach (var error in ex.Errors)
        Console.WriteLine($"{error.FullPath.ToDottedString()}: {error.Message}");
}
```
## Schemas and rules

- Schemas are classes deriving from `ZodType<TOutput, TInput>`; the fluent methods return `this` (or a wrapping schema) so chains read naturally.
- Rules are `readonly record struct` implementations of `IValidationRule<T>` (`bool IsValid(in T value)`, `string GetErrorMessage(in T value)`) — zero allocation.
- Custom rules can be attached with the public `AddRule`/`Rule` methods and surfaced as DataAnnotations-style attributes; see [Custom Rules](Custom-Rules.md).
- `ValidateSpan(ReadOnlySpan<char> value)` is available on `ZodString` for span-based validation; use `IsValidSpan(value, out errors)` for an allocation-free check.
- A schema's `Description` is set with `.Describe("...")`.

## Composition model

`ZodType` composes via wrappers rather than mutation. The base type guards that input and output types match, then returns a new schema:

- `Transform<TNew>(Func<TOutput, TNewOutput>)` → `ZodTransform<TOutput, TNewOutput>`.
- `Refine(Func<TOutput, bool>, string? message)` → `ZodRefinement<TOutput>`.
- `SuperRefine(Action<RefineCtx<TOutput>>)` → `ZodSuperRefinement<TOutput>`.
- `Pipe<TTarget>(IZodSchema<TTarget, TOutput>)` → `ZodPipe<TOutput, TTarget>`.
- `Catch(TOutput | Func<TOutput, ImmutableArray<ValidationError>, TOutput>)` → `ZodCatch<TOutput>`.
- `Prefault(TOutput)` → `ZodPrefault<TOutput>`.
- `Default(TOutput)` → `ZodDefault<TOutput>`.
- `And(IZodSchema<TOutput, TOutput>)` → `ZodIntersection<TOutput>`.
- `Or<TOther>(IZodSchema<TOther, TOther>)` → `ZodTypedUnion<TOutput, TOther>`.

See [Composition and Transforms](Composition-and-Transforms.md) for details and semantics.

## Optionality and null handling

Schemas report optionality through the internal `IOptionalSchema` interface:

- `IsOptional` — `true` for `ZodOptional`, `ZodNullable`, `ZodDefault`, `ZodPrefault`.
- `ProvidesValueOnMissing` — `true` for `ZodDefault` and `ZodPrefault`; object fields route missing values through `Validate(null!)` to inject the produced value.
- `IAcceptsNull.ValidateNull()` is implemented by nullable/optional/default/prefault schemas and `ZodNull`; object-field and union wrappers route `null` input to it instead of failing coercion.

## Type coercion

Object fields and union options wrap typed schemas so boxed values from a `Dictionary<string, object?>` can be validated:

- `null` routes to `IAcceptsNull.ValidateNull()` where supported.
- Exact-typed values reuse the original boxed value.
- Numeric coercion uses `IConvertible` (invariant culture), so a boxed `long` can satisfy a `Z.Number()` field.
- Non-nullable value types reject `null`; anything else falls through to failure.

## Dependency injection

`IZodSchemaFactory` is the registry that resolves validators by type. See [Dependency Injection](Dependency-Injection.md).
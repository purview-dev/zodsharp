# The ZodSharp error model in detail

Everything here is in `ZodSharp.Core`.

## Code versus Category

`Code` is ZodSharp's own identity and is what tests and clients branch on when they need precision.
`Category` is the Zod v4 issue code, so a client that already handles TypeScript Zod issues can treat
ZodSharp issues the same way.

| Failure | `Code` | `Category` |
| --- | --- | --- |
| null where a value is required | `invalid_type` | `invalid_type` |
| missing object field / generated `[Required]` | `missing_field` | `invalid_type` |
| `Min(3)` on a string | the rule's `ErrorCode` | `too_small` |
| `[StringLength]`/`[Length]` | `invalid_length` | `too_small` or `too_big` by direction |
| email, uuid, regex, prefix, … | the rule's `ErrorCode` | `invalid_format` |
| `MultipleOf` | the rule's `ErrorCode` | `not_multiple_of` |
| strict object with extra keys | `unrecognized_keys` (one issue, `keys` parameter) | `unrecognized_keys` |
| union with no matching option | `invalid_union` | `invalid_union` |
| enum / literal / allowed values | `invalid_enum_value`, `invalid_literal`, rule code | `invalid_value` |
| `Refine`, `SuperRefine`, `ApplyRefine`, custom method | `refinement_failed` (or your code) | `custom` |

Every rule declares `public const string ErrorCode` and `MessageFormat` (diagnostic `ZODSGEN042`), so
assert against `MinLengthRule.ErrorCode` rather than a literal.

## Bounds

`Lower` and `Upper` are `ErrorBound?` - `record struct ErrorBound(object Value, bool Inclusive)` with
`ErrorBound.Including(value)` / `ErrorBound.Excluding(value)`. They hold numbers, decimals and dates.
`GreaterThan`/`LessThan` and `[Range(MinimumIsExclusive = true)]` report `Inclusive = false`.
`Minimum`/`Maximum`/`Inclusive` (`int?`) are still populated for integer size bounds; `Lower`/`Upper`
fall back to them.

## Message templates

Built-in messages come from named templates (`ZodMessages`, each rule's `MessageFormat`), kept on the
error as `MessageFormat`. `ZodMessageFormat` resolves `{name}` and `{name:format}` from the error's
parameters, then from the built-ins `displayName`, `member`, `path`, `code`, `category`, `origin`,
`minimum`, `maximum` and `inclusive`. `{{`/`}}` escape braces; an unknown placeholder or an unbalanced
brace is written literally. It never throws - a stray brace in a custom message cannot turn a failure
into an exception. `TryFormatComplete` succeeds only when every placeholder resolved.

Every built-in rule reports its template through `IZodRule.MessageFormat`, so runtime-schema errors carry
one too (`Z.String().Min(3)`: `"String must be at least {minimum} characters long, but got {count}"`). A
custom rule message is reported with its positional placeholders renamed (`{0}` → `{minimum}`); the
value's position becomes `{received}`.

```csharp
using ZodSharp.Core;

var error = ValidationError.Create(
	"too_short",
	"Name is too short",
	ZodPath.Member("Name"),
	new Dictionary<string, object?> { [ZodIssueParameters.DisplayName] = "Name" },
	category: ZodIssueCategories.TooSmall
) with
{
	Lower = ErrorBound.Including(3),
	MessageFormat = "{displayName} must have at least {minimum} characters",
};

var text = ZodMessageFormat.Format(error.MessageFormat!, error); // "Name must have at least 3 characters"
```

## Localization hook

```csharp
public interface IZodMessageFormatter
{
	string Format(in ValidationError validationError, CultureInfo culture);
}
```

Hosts (ProblemDetails writer, the Microsoft.Extensions.Validation bridge, Blazor) call it with
`CultureInfo.CurrentUICulture`. `DefaultZodMessageFormatter.Instance` re-formats `MessageFormat` when
every placeholder resolves and otherwise returns `Message`. `StringLocalizerZodMessageFormatter`
(`Purview.ZodSharp.Extensions.Validation`) looks keys up in this order: the `MessageFormat` (or literal
`Message`), `{DeclaringType}_{Member}_{Code}`, `{DeclaringType}_{Code}`, `{Code}`; otherwise the message
is kept. `{displayName}` is localized from `{DeclaringType}_{Member}_DisplayName`. Implementations must
never throw.

## Paths

- Members join with `.`; indexes are `[n]`; keys are `[key]`, quoted as `["key"]` when the key is all
  digits or contains anything other than ASCII letters, digits, `_` or `-`.
- `ZodPath.FromLegacy(strings)` classifies `"0"` and `"[0]"` as indexes, so string paths passed to
  `RefineCtx.AddIssue(code, message, string[])` keep working.
- `RefineCtx<T>.FullPath` is the refinement's base path; `AddIssue(code, message, ZodPath, parameters,
  category)` and `AddIssue(ValidationError)` add typed issues relative to it.
- Generated `{Type}Schema.Paths.{Member}` constants give typed member paths for refinement hooks.

## Unions

A union failure is one `invalid_union` issue at the union's path. Its per-option issues are in the
`unionErrors` parameter as `ImmutableArray<ImmutableArray<ValidationError>>`; read them with
`error.GetUnionErrors()`. The ProblemDetails `issues[]` projects them as nested `unionErrors`.

## PII

`ZodErrorOptions.IncludeReceivedValues` is process-wide and defaults to `false`. When off, a rule failure
carries no `received` parameter and an invalid-type failure's `received` holds only the type name
(`"string"`). Built-in default messages never contain the input either way - numeric rules included
(`"Value must be between 1 and 10"`); a custom message can still opt in with the rule's positional value
placeholder (`{0}`, or `{1}`/`{2}` after the bounds).

## Depth

`ZodValidationDepth.MaxDepth` (default `ZodValidationDepth.DefaultMaxDepth`, 32) is process-wide. The
root is depth 0; a generated validator throws `InvalidOperationException` when it reaches the limit,
matching Microsoft.Extensions.Validation's `ValidationOptions.MaxDepth`.

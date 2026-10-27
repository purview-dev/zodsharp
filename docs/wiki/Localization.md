# Localization

ZodSharp validation is culture-independent: codes, categories, paths, bounds and parameters are invariant data, and
built-in messages are produced with the invariant culture. Messages are localized at the **host boundary**, which is
the ProblemDetails writer, the Microsoft.Extensions.Validation bridge, Blazor or an options failure. That keeps
validation deterministic and allocation-free on success, and lets each host render messages in the request culture.

## The message model

Every built-in error carries two message fields:

| Field | Meaning |
|---|---|
| `Message` | The message as produced, formatted with the invariant culture. |
| `MessageFormat` | The template that produced it, with named placeholders. It is `null` when the message was supplied literally. |

```csharp
var error = OrderSchema.Validate(new Order { CustomerName = "x" }).Errors[0];

// error.Message       "Field 'CustomerName' must contain at least 3 characters."
// error.MessageFormat "Field '{displayName}' must contain at least {minimum} characters."
```

Placeholders resolve from the error's `Parameters`, then from its built-in fields. See
[Core Concepts](Core-Concepts.md#error-fields) for the full list of error fields.

## `ZodMessageFormat`

`ZodMessageFormat` is the one formatting engine every producer and host uses. Unlike `string.Format`, it never
throws, because a stray brace in a custom message must not turn a validation failure into an exception. Its
placeholder syntax:

| Syntax | Meaning |
|---|---|
| `{name}` | A named placeholder, resolved from the parameters and then from the built-ins below. |
| `{name:format}` | A named placeholder with a format string. |
| `{0}`, `{1}` | Positional placeholders, kept for DataAnnotations-style custom messages. |
| `{{`, `}}` | Literal braces. |
| An unknown placeholder or an unbalanced brace | Written out literally. |

When formatting against an error, these built-in fields are available as placeholders: `displayName`, `member`,
`path`, `code`, `category`, `origin`, `minimum`, `maximum` and `inclusive`.

```csharp
using ZodSharp.Core;

var text = ZodMessageFormat.Format("{displayName} must be at least {minimum} ({code})", error);
// "CustomerName must be at least 3 (too_small)"

if (ZodMessageFormat.TryFormatComplete("{displayName}: {missing}", error, out var message))
{
    // Not reached: {missing} does not resolve, so the template is not used.
}
```

`TryFormatComplete` succeeds only when every placeholder resolves. Use it when a template replaces an
already-good message, so a template is never shown with its placeholders unfilled. The ProblemDetails writer uses it
for `ErrorType.MessageFormat`.

## `IZodMessageFormatter`

`IZodMessageFormatter` (in `ZodSharp.Core`) is the localization hook:

```csharp
public interface IZodMessageFormatter
{
    string Format(in ValidationError validationError, CultureInfo culture);
}
```

An implementation must never throw. It must fall back to `ValidationError.Message` when it has nothing better, so a
missing resource can never turn a validation failure into an exception.

`DefaultZodMessageFormatter.Instance` re-formats `MessageFormat` in the requested culture when every placeholder
resolves, and otherwise returns `Message` unchanged. It has no dependency on `Microsoft.Extensions.Localization`.

## `StringLocalizerZodMessageFormatter`

`StringLocalizerZodMessageFormatter` (package `Purview.ZodSharp.Extensions.Validation`) localizes through
`IStringLocalizer`. It uses the same lookup shape that Microsoft.Extensions.Validation uses for DataAnnotations, so one
resource file serves both. Keys are tried in order, and the first one found is the template:

1. The error's `MessageFormat` or, for a literal message, its `Message`. This localizes by the English template.
2. `{DeclaringType}_{Member}_{Code}`.
3. `{DeclaringType}_{Code}`.
4. `{Code}`.
5. Otherwise the error's own `Message` is kept.

`{DeclaringType}` is the **simple name** of the type that owns the failing member, with any generic arity removed
(`Order`, not `Contoso.Orders.Order`). This matches Microsoft.Extensions.Validation's key segment. It comes from the
declaring type passed by the host, or from the error's `declaringType` parameter, which generated validators emit.
`{Member}` comes from the `member` parameter, or from the last member segment of the path.

The template found is formatted with `ZodMessageFormat`, so it can use named placeholders and never throws.
`{displayName}` is itself localized from the key `{DeclaringType}_{Member}_DisplayName` when that key exists.

| Key | Example value |
|---|---|
| `Field '{displayName}' must contain at least {minimum} characters.` | `Le champ '{displayName}' doit contenir au moins {minimum} caractères.` |
| `Order_CustomerName_too_small` | `Le nom du client est trop court.` |
| `Order_missing_field` | `Ce champ de la commande est obligatoire.` |
| `too_small` | `La valeur est trop petite.` |
| `Order_CustomerName_DisplayName` | `Nom du client` |

```csharp
using Microsoft.Extensions.Localization;
using ZodSharp.Extensions.Validation;

var formatter = new StringLocalizerZodMessageFormatter(
    localizerFactory,
    localizerProvider: null,            // defaults to factory.Create(type)
    sharedResourceType: typeof(ValidationMessages));
```

| Constructor parameter | Meaning |
|---|---|
| `factory` | The `IStringLocalizerFactory`. |
| `localizerProvider` | Resolves the localizer for a declaring type. Pass `ValidationOptions.LocalizerProvider` so ZodSharp and DataAnnotations share resources. Defaults to `factory.Create(type)`. |
| `sharedResourceType` | The resource type used when the declaring type is unknown, which is the case for the plain `Format(in ValidationError, CultureInfo)` overload. When `null`, such errors keep their own message. |

A `Format(in ValidationError, Type? declaringType, CultureInfo)` overload looks keys up in the localizer of a known
declaring type. The bridge uses it.

## Where each host applies the formatter

| Host | Formatter | Culture |
|---|---|---|
| ProblemDetails (`ZodProblemDetailsWriter`: endpoint filter, `ZodExceptionHandler`, `ZodTypedResults`, the .NET 11 pipeline enrichment) | The `IZodMessageFormatter` registered in DI; without one, messages are used as produced | `CultureInfo.CurrentUICulture` |
| Microsoft.Extensions.Validation bridge (minimal APIs, Blazor) | `ZodValidationOptions.MessageFormatter`; when `null`, a `StringLocalizerZodMessageFormatter` built from the validation's `IStringLocalizerFactory` and `ValidationOptions.LocalizerProvider`, if one is registered; otherwise the message as produced | `CultureInfo.CurrentUICulture` |
| Validation itself (`Validate`, `ValidateAsync`) | None | Invariant |

With ASP.NET Core request localization, `CurrentUICulture` follows `Accept-Language`:

```csharp
using Microsoft.Extensions.Localization;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

builder.Services.AddLocalization();
builder.Services.AddSingleton<IZodMessageFormatter>(sp => new StringLocalizerZodMessageFormatter(
    sp.GetRequiredService<IStringLocalizerFactory>(),
    sharedResourceType: typeof(ValidationMessages)));
builder.Services.AddZodSharpProblemDetails();

var app = builder.Build();
app.UseRequestLocalization("en", "fr");
```

The bridge localizes automatically once `AddLocalization()` registers an `IStringLocalizerFactory`, the same trigger
Microsoft.Extensions.Validation uses. The ProblemDetails writer uses only a formatter you register.

## Display names

Generated validators put the member's display name in the `displayName` parameter. They also emit `member` and
`declaringType`, so the type and member keys above work without reflection.

- `[Display(Name = "...")]` sets the display name.
- `[Display(Name = nameof(Resources.CustomerName), ResourceType = typeof(Resources))]` reads the resource property
  **at runtime**, so the display name follows the current culture.
- `ErrorMessageResourceType`/`ErrorMessageResourceName` on a DataAnnotations attribute are honoured for the
  message template.

See [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md).

## Culture policy

| Data | Culture |
|---|---|
| `Code`, `Category`, `Origin`, `Parameters` values, `FullPath` key segments, bounds | Invariant, always |
| `Message` produced at validation time | Invariant (deterministic, and stable across the cross-platform fixtures) |
| A message rendered by an `IZodMessageFormatter` at a host boundary | `CultureInfo.CurrentUICulture` |

Dictionary keys in paths are rendered with the invariant culture (`ZodPathSegment.Key<TKey>`), so a path never
depends on the thread's culture. See [Core Concepts](Core-Concepts.md#culture).

## PII

Messages leave the process: they appear in ProblemDetails bodies, logs and UI. The input could be a card number, a
token or another secret, so:

- No built-in default message contains the input value.
- The `received` parameter holds only the input's Zod type name (for example `"string"`) by default.
- `ZodErrorOptions.IncludeReceivedValues = true` (process-wide, default `false`) makes rule failures carry the raw
  input as `received`. It is intended for local development only. Never enable it in production.
- The ProblemDetails `issues[].parameters` projection serializes only invariant scalar values and arrays of them.
  It never serializes raw objects or nested errors.

A custom message can still opt in to the value through positional placeholders. See
[Core Concepts](Core-Concepts.md#pii).

## See also

- [ASP.NET Core Integration](AspNetCore-Integration.md)
- [Microsoft.Extensions.Validation](Microsoft-Extensions-Validation.md)
- [Custom Rules](Custom-Rules.md)

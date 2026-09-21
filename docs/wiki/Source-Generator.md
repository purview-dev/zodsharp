# Source Generator

Mark a class, struct, or record with `[ZodSchema]` and the generator emits a static, zero-allocation validator at compile time. The `[ZodSchema]` attribute is generated into the `ZodSharp` namespace by the generator itself (assembly `Purview.ZodSharp.SourceGenerators`), so no extra package is needed beyond `Purview.ZodSharp`.

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

[ZodSchema]
public class User
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int Age { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

var result = UserSchema.Validate(user);
var validated = UserSchema.Parse(user); // throws ZodException on failure
```

## Generated types

For a `[ZodSchema]` target type `{TypeName}`, the generator emits:

| Artifact | Shape |
|---|---|
| `{TypeName}Schema` | static partial class — the validator; access mirrors the target (public/internal/private for private nested types); contains `Validate`, `Parse`, and (when composition is enabled) `ApplyAnd`, `ApplyOr`, `ApplyRefine` |
| `{TypeName}SchemaValidator` | `partial class {TypeName}SchemaValidator : IZodSchemaValidator<{TypeName}>` — DI-friendly adapter with `Validate` / `ValidateAsync`; emitted only for the primary schema |
| `{TypeName}Validator` | `sealed partial class {TypeName}Validator : IValidateOptions<{TypeName}>` — emitted only when `IValidateOptions` support is enabled (and the target is a class) |
| `[assembly: ZodSchemaGenerated(typeof({TypeName}))]` | registration marker consumed by `IZodSchemaFactory` assembly scanning; emitted only for primary, non-nested schemas |

```csharp
// Value-first composition methods (EnableComposition, default true):
var adult = UserSchema.ApplyRefine(user, u => u.Age >= 18, "Must be adult");
var both = UserSchema.ApplyAnd(user, u => u.Name.Length > 5, "Name too short");
var either = UserSchema.ApplyOr(user, u => u.Age < 18, "Must be an adult or a minor with consent");
```

## Attribute options

All options are optional.

| Property | Default | Purpose |
|---|---|---|
| `SchemaName` | `null` | Reserved — the schema class is always named `{TypeName}Schema`. |
| `GenerateValidateMethod` | `true` | Reserved — `Validate` is always emitted. |
| `GenerateParseMethod` | `true` | Reserved — `Parse` is always emitted. |
| `EnableComposition` | `true` | Emits `ApplyAnd`, `ApplyOr`, `ApplyRefine` value-first composition methods. |
| `CustomValidationMethodName` | `null` | Name of an async custom validation method; default lookup name `CustomValidationAsync`. Mutually exclusive with the synchronous refinement method. |
| `RefinementMethodName` | `null` | Name of a synchronous refinement method; default lookup name `Validate` (an instance method on the model). Mutually exclusive with the async custom validation method. |
| `GenerateIValidateOptions` | `false` | Force `IValidateOptions<T>` generation. |
| `SuppressIValidateOptions` | `false` | Opt out even when auto-detection would enable it. |

> [!NOTE]
> `SchemaName`, `GenerateValidateMethod`, and `GenerateParseMethod` are parsed by the attribute but not yet honoured by the generator — the class is always `{TypeName}Schema` with `Validate` and `Parse`.

## Custom async validation

Declare a partial `{TypeName}SchemaValidator` (or a static method on the model type):

```csharp
public partial class UserSchemaValidator
{
    public async ValueTask<ValidationResult<User>> CustomValidationAsync(User value, CancellationToken ct)
    {
        await Task.Delay(1, ct);
        return ValidationResult<User>.Success(value);
    }
}
```

Requirements:

- Signature `ValueTask<ValidationResult<T>> Name(T value, CancellationToken ct)`.
- Default lookup name `CustomValidationAsync` unless overridden with
  `CustomValidationMethodName` on the `[ZodSchema]` attribute.
- A method declared on the model type must be `static`; a method on the generated
  `{TypeName}SchemaValidator` partial may be an instance method.
- The generated `ValidateAsync` runs the synchronous `Validate`, then awaits the custom method, and
  merges the error sets.

> [!WARNING]
> The async custom validation method is mutually exclusive with the synchronous refinement method. A
> model must declare exactly one of the two — declaring both is an error (ZODSGEN029).

## Synchronous refinement

Declare an instance method on the model (default name `Validate`) returning `IEnumerable<ValidationError>`:

```csharp
[ZodSchema(RefinementMethodName = "Validate")]
public class Order
{
    public decimal Total { get; set; }

    public IEnumerable<ValidationError> Validate()
    {
        if (Total < 0)
            yield return ValidationError.Create("invalid_range", "Total cannot be negative", []);
    }
}
```

Requirements:

- The method must be an **instance** method on the model type (it is invoked on the value being
  validated). A `static` method is an error (ZODSGEN024).
- Default lookup name `Validate` unless overridden with `RefinementMethodName` on the `[ZodSchema]`
  attribute.
- Parameterless or `IEnumerable<ValidationError> Validate(RefineCtx<Order> ctx)` variants are
  supported.

> [!WARNING]
> The synchronous refinement method is mutually exclusive with the async custom validation method. A
> model must declare exactly one of the two — declaring both is an error (ZODSGEN029).

## IValidateOptions support

Generated options validators are enabled by:

1. `GenerateIValidateOptions = true` on the attribute, or
2. auto-detection: `GenerateIValidateOptions` unset, target is not a value type, and the type name ends with a configured suffix (default `Options` or `Settings`), or
3. MSBuild override.

MSBuild switches:

| Property | Default | Behaviour |
|---|---|---|
| `DisableZodSharpSourceGenerator` | unset | disables the generator entirely when truthy |
| `ZodSharpAutoGenerateOptionsValidators` | `true` | auto-detect `IValidateOptions` (only explicit `false` disables) |
| `ZodSharpAutoGenerateOptionsValidatorSuffixes` | `Options;Settings` | semicolon/comma-separated suffix list |

## What is validated

- Properties must be public, non-static, non-indexer.
- A property is included when it carries any DataAnnotations attribute or its type is a source-defined complex type with a nested schema.
- Classes, structs, and records are supported; structs do not receive `IValidateOptions` (ZODSGEN028 if requested).
- Nested complex types are discovered recursively and get their own generated `{TypeName}Schema`, even when the nested type does not itself carry `[ZodSchema]`.
- Nullable properties are null-guarded before value-set/type validation; a nullable target rejects `null` with `invalid_type`.

See [Source Generator DataAnnotations](Source-Generator-DataAnnotations.md) for the attribute coverage and structured issue shape, and [Source Generator Diagnostics](Source-Generator-Diagnostics.md) for the `ZODSGEN*` diagnostics.
# Trimming and Native AOT

Every shipping ZodSharp package is marked `IsTrimmable` and `IsAotCompatible` on every target where the analyzers exist
(`net8.0` and later), and builds with no trim (IL2xxx), AOT (IL3xxx) or single-file warnings. An API that cannot work
when trimmed is not hidden behind a suppression: it is annotated `[RequiresUnreferencedCode]` (and
`[RequiresDynamicCode]` where it needs runtime code generation), so a trimmed or AOT application is warned at the call
site, and the message names the trim-safe alternative.

The proof is `src/tests/TrimSafety.Verification`, a console application published with `PublishAot=true` and warnings
as errors. It is published twice: once with every ZodSharp assembly except `Purview.ZodSharp.NewtonsoftJson` rooted
whole (`TrimmerRootAssembly`), so ILC analyses all of their code rather than only what the program calls, and once as a
plain consumer. Each native binary then runs runtime schemas, generated validators, System.Text.Json, JSON Schema,
the Microsoft.Extensions.Validation bridge, ProblemDetails and options validation, and exits non-zero on any mismatch.
Run it with `just verify-trim`; the `verify-trim` PR job runs it on Linux.

## Safe everywhere

These need nothing from the application:

| Area | APIs |
|---|---|
| Runtime schemas | `Z.String()`, `Z.Number()`, `Z.Object()`, `Z.Array()`, unions, `Or()`, `And()`, refinements, `CompiledValidator` |
| Generated validators | `{Type}Schema.Validate`/`ValidateAsync`/`ValidateMember`/`TryResolveMember`/`Parse`, `{Type}SchemaValidator` |
| Registration | the generated `AddZodSharp{AssemblyId}Schemas()` and `ZodSharp.Generated.{AssemblyId}ZodSharpSchemas.Register(factory)`, `IZodSchemaFactory.Register<T>` |
| Metadata registration | `RegisterFromAssembly`, `AddZodSharpAssembly`, `ScanAssemblies`, `ScanLoadedAssemblies` (they read `[ZodSchemaGenerated]`, whose `typeof` roots each validator) |
| JSON Schema | `Z.ToJsonSchema`, `{Type}Schema.BuildJsonSchema()`, `Z.FromJsonSchema` (System.Text.Json), `JsonSchemaSerializerOptions` |
| System.Text.Json | the `JsonTypeInfo<T>` overloads of `DeserializeAndValidate`, `DeserializeAndValidateAsync`, `ValidateAndSerialize`, `ValidateAndSerializeAsync`; `CreateValidatingConverter` |
| ASP.NET Core | `AddZodSharp`, `AddZodSharpProblemDetails`, `ZodProblemDetailsWriter`, `WithZodSharpValidation`, `ZodTypedResults` |
| Validation bridge | `AddZodSharpValidation`, `ValidationOptions.ValidateAsync` |
| Options | `AddZodSchemaValidator`, `ValidateWithZod(section)` |
| Discriminated unions | `Z.DiscriminatedUnion(...).Option<T>(...)` over dictionaries, JSON or CLR types supplied as `T` |

## Annotated, with the alternative

| API | Why | Use instead |
|---|---|---|
| `BindAndValidateWithZod` | binds with the reflection-based configuration binder | `BindConfiguration(section).ValidateWithZod(section)`; the configuration binder source generator intercepts `BindConfiguration` |
| The `JsonSerializerOptions` overloads of `DeserializeAndValidate`/`ValidateAndSerialize` | resolve `T`'s contract reflectively | the `JsonTypeInfo<T>` overloads with a source-generated `JsonSerializerContext` |
| `AddZodSharpAssemblyGraph`, `ZodSchemaFactoryOptions.ScanAssemblyGraphs` | walk `Assembly.GetReferencedAssemblies`, which trimming can invalidate | the generated `AddZodSharp{AssemblyId}Schemas()`, or `AddZodSharpAssembly` |
| `ZodModelSchema.For<TModel>(ZodObject)` (Blazor) | reads the model and nested models by reflection | a `[ZodSchema]` validator, or `ZodModelSchema.For<TModel>(IZodSchema<TModel, TModel>)` |
| `AddZodSharpClientValidation`, `ZodClientValidationProvider`, `ZodClientValidationRules.FromAttribute` | static-SSR client validation needs `AddRazorComponents` (itself not trim/AOT compatible) and reads rule attributes by reflection | omit it in a trimmed app (client rules are UX only; the server validates), or `ZodClientValidationRules.For(IZodRule)`/`FromRule` from an `IClientValidationRuleProvider` |
| Every API in `Purview.ZodSharp.NewtonsoftJson` that reaches Newtonsoft serialization | Newtonsoft.Json is reflection-based | `Purview.ZodSharp.SystemTextJson` |

## The `ZodSharp.Reflection.IsSupported` feature switch

A few fallbacks read members of a type the library was never told about:

- a discriminated union reading the discriminator of a POCO whose type was not added with `Option<T>`;
- the Blazor and Microsoft.Extensions.Validation field walkers, which place an error on a form field when the validator
  does not resolve members itself (generated validators always do);
- assembly reference-graph scanning.

They are guarded by the `ZodSharp.Reflection.IsSupported` feature switch (`[FeatureSwitchDefinition]` and
`[FeatureGuard]` on .NET 9 and later). `Purview.ZodSharp`'s `buildTransitive/Purview.ZodSharp.targets` sets it to
`false` with `Trim="true"` whenever `PublishTrimmed` or `PublishAot` is `true`, so ILLink and ILC remove the branches.
In a trimmed application the effect is deterministic: an unregistered POCO is reported as missing its discriminator
(the union fails closed), an error from a non-generated validator stays on the nearest object the validator resolved,
and graph scanning registers nothing. Use `Option<T>`, generated validators and generated registration instead.

.NET 8's trimming tools do not understand feature guards, so on `net8.0` the guarded calls carry narrow suppressions
whose justification is the same: they are reached only through the annotated opt-in APIs, or for a type the
application did not supply.

## Collection counts: `[MinCount]`, `[MaxCount]` and `[CountRange]`

`System.ComponentModel.DataAnnotations.MinLengthAttribute`, `MaxLengthAttribute` and `LengthAttribute` have
`[RequiresUnreferencedCode]` constructors (they may reflect over a `Count` property), so every usage raises IL2026 in a
trimmed or AOT application, even though the generated validator never runs that reflection. ZodSharp ships trim-safe
equivalents in `ZodSharp.Rules`, generated from `MinCountRule`, `MaxCountRule` and `CountRangeRule`:

```csharp
using ZodSharp.Rules;

[ZodSchema]
public sealed class Order
{
    [MinCount(1)]
    [MaxCount(20)]
    public IList<OrderLine> Lines { get; init; } = [];

    [CountRange(0, 5)]
    public Dictionary<string, Address> Addresses { get; init; } = [];
}
```

The generator validates them through the same collection-size path as `[MinLength]`/`[MaxLength]`/`[Length]`, so the
error identity is identical: `too_small`/`too_big`, the `array` or `record` origin, the bound and the `count`
parameter, and the same default message. `Message` is an alias of `ErrorMessage` and uses the DataAnnotations
placeholders (`{0}` is the display name, `{1}` the bound). They export `minItems`/`maxItems` (or
`minProperties`/`maxProperties` for a dictionary) to JSON Schema. On a member that is not a collection they are
reported as `ZODSGEN030`.

In a project with `PublishTrimmed`, `PublishAot`, `IsTrimmable` or `IsAotCompatible`, the analyzer reports
`ZODSGEN053` for `[MinLength]`, `[MaxLength]` or `[Length]` on a collection member of a `[ZodSchema]` type, with a code
fix that swaps the attribute and imports `ZodSharp.Rules`. On a `string`, use `[StringLength]` (not annotated) or the
generated `[MinLengthZod]`/`[MaxLengthZod]`/`[LengthZod]`.

## Newtonsoft.Json

`Purview.ZodSharp.NewtonsoftJson` is marked `IsAotCompatible` so the analyzers keep it honest, but Newtonsoft.Json itself
is not trim- or AOT-compatible. Every API that reaches it (`DeserializeAndValidate`, `DeserializeAndValidateAsync`,
`ValidateAndSerialize`, `ValidateAndSerializeAsync`, `CreateValidatingConverter`, `Z.FromJsonSchema`,
`FromJsonSchemaParser.Parse` and `JsonSchemaSerializerOptions`) is annotated `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. Use `Purview.ZodSharp.SystemTextJson` with a source-generated context in a trimmed or AOT
application.

## Related

- [Source Generator](Source-Generator.md) — generated registration and validators.
- [Source Generator Diagnostics](Source-Generator-Diagnostics.md) — `ZODSGEN053`.
- [System.Text.Json Integration](SystemTextJson-Integration.md) — the `JsonTypeInfo<T>` overloads.
- [Guarantees and Limitations](Guarantees-and-Limitations.md).

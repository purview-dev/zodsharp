# Trimming and Native AOT

Every shipping ZodSharp package is marked `IsTrimmable` and `IsAotCompatible` on every target (`net10.0` and
`net11.0`), and builds with no trim (IL2xxx), AOT (IL3xxx) or single-file warnings. An API that cannot work
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
`[FeatureGuard]`). `Purview.ZodSharp`'s `buildTransitive/Purview.ZodSharp.targets` sets it to
`false` with `Trim="true"` whenever `PublishTrimmed` or `PublishAot` is `true`, so ILLink and ILC remove the branches.
In a trimmed application the effect is deterministic: an unregistered POCO is reported as missing its discriminator
(the union fails closed), an error from a non-generated validator stays on the nearest object the validator resolved,
and graph scanning registers nothing. Use `Option<T>`, generated validators and generated registration instead.

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

## The rooted publish and IL3054 (generic recursion)

The rooted publish of `src/tests/TrimSafety.Verification` tolerates exactly one warning, IL3054, and the consumer
publish tolerates none. IL3054 is ILC reporting that it stopped expanding a generic cycle:

```text
ZodTypedUnion<__Canon,__Canon>: Generic expansion to
'ZodType<__Canon,Union<Union<Union<Union<Union<Int64,__Canon>,__Canon>,__Canon>,__Canon>,__Canon>>.Or<__Canon>(...)'
was aborted due to generic recursion.
```

The cycle is in the public type shape:

- `ZodType<TOutput, TInput>.Or<TOther>(other)` returns `ZodTypedUnion<TOutput, TOther>`.
- `ZodTypedUnion<T1, T2>` derives from `ZodType<Union<T1, T2>, object>`, so it inherits `Or` (and `And`, `Catch`,
  `Default`, `Prefault`, `Refine`, `SuperRefine`, `Transform` and `Pipe`, which close their own generic types over
  the output type in the same way).
- `Union<T1, T2>` is a `readonly struct`, so `Union<Union<T1, T2>, T3>` is a new, unshareable instantiation at every
  step. A class would canonicalise to `__Canon` and end the expansion; a struct does not.

Nothing in the library calls `Or` (or any of the others) on its own result, so no reachable code needs a deeper
instantiation. ILC only builds the chain because `TrimmerRootAssembly` roots every method of every type, including
the inherited generic methods of each `ZodTypedUnion` it constructs, and the warning's own message ("an exception will
be thrown at runtime if this codepath is ever reached") describes code no application reaches unless it nests `Or`
that deep itself. The consumer publish - nothing rooted, `Or()` and `NativeUnion()` called - compiles with no IL3054,
and so does the packed-consumer gate (`just smoke-packed-consumers`).

It is not fixed in 3.0, because every change that breaks the cycle breaks the 2.4.2 API that package validation holds
the packages to:

| Change | Why it is not taken |
|---|---|
| Seal `ZodTypedUnion<T1, T2>` | Package validation rejects it (`CP0009`: the type gains the `sealed` modifier). It would not help either: measured, the rooted publish still reports 32 IL3054 warnings with the class sealed, because the recursion runs through the methods `ZodTypedUnion` inherits, not through overrides. |
| Make `Or` non-virtual | It already is. So are `And`, `Catch`, `Default`, `Prefault`, `Refine`, `SuperRefine`, `Transform` and `Pipe`. |
| Derive `ZodTypedUnion` from a base without `Or` | Changes the base type of a public class, and every caller that holds a union as `ZodType<Union<T1, T2>, object>`. |
| Make `Union<T1, T2>` a class | Changes a public value type to a reference type: binary-breaking, and an allocation per union result. |
| Move `Or` off `ZodType` (an extension method constrained away from unions) | Removes a public instance member (`CP0002`). |

The tolerance is scoped as narrowly as ILC allows: `IL3054` is in `NoWarn` only when `TrimVerificationMode` is
`Rooted`, and every other trim or AOT warning in the rooted publish, including one in a ZodSharp assembly, still fails
it. Revisit it in the next major version, where the type shape can change.

## Related

- [Source Generator](Source-Generator.md) — generated registration and validators.
- [Source Generator Diagnostics](Source-Generator-Diagnostics.md) — `ZODSGEN053`.
- [System.Text.Json Integration](SystemTextJson-Integration.md) — the `JsonTypeInfo<T>` overloads.
- [Guarantees and Limitations](Guarantees-and-Limitations.md).

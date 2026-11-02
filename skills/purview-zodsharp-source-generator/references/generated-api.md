# The generated validator API

For a `[ZodSchema] partial class Order`, the generator emits these public members on `OrderSchema`
(all `static`):

| Member | Purpose |
| --- | --- |
| `Validate(Order value)` | full synchronous validation; one generated method per member, then type rules, `IValidatableObject`, then the `OnZodValidate` hook |
| `ValidateAsync(Order value, IServiceProvider? services = null, CancellationToken cancellationToken = default)` | full validation including nested async schemas, `IAsyncValidatableObject`, `AsyncValidationAttribute` members and the custom async method |
| `const bool RequiresAsyncValidation` | `true` when anything in the type's graph is asynchronous |
| `IsValidatableMember(string memberName)` | whether the member has rules of its own |
| `ValidateMember(Order value, string memberName)` | the member's own rules only; an unknown member succeeds |
| `ValidateMemberAsync(value, memberName, services, cancellationToken)` | as above plus the member's async rules and any `[ZodAsyncMember]` custom method for it |
| `TryResolveMember(Order value, ZodPath path, out object? container, out string memberName)` | follows an error path to the object that owns the failing member |
| `Paths.{Member}` | `ZodPath` constants |
| `JsonNames.Get(string memberName)` | the member's `[JsonPropertyName]`, or `null` |
| `ToJsonNamedPath(ZodPath path, Func<string, string>? namingPolicy = null)` | renames member segments to JSON names; members without `[JsonPropertyName]` use `namingPolicy` |
| `Parse`, `BuildJsonSchema`, `ApplyAnd`, `ApplyOr`, `ApplyRefine` | as before |

`ValidateCore`/`ValidateCoreAsync`/`AppendJsonNamedPath` overloads with a `depth` or `offset` parameter
are public only so nested schemas can call them; do not call them directly.

## Field-level semantics

`ValidateMember` never recurses into a nested schema and never runs type-level rules,
`IValidatableObject` or refinements, so every error it returns belongs to the member that changed.
Collection members still check for `null` elements. Hosts reach it through `IZodMemberValidator<T>`
or the untyped `IZodUntypedValidator.ValidateMemberAsync`.

`TryResolveMember` returns `true` when it followed the full path. For a path ending in an index or key
it resolves to the owner of the collection member; for a path it cannot follow it resolves to the
nearest object reached. `memberName` is empty for a model-level error.

```csharp
var result = OrderSchema.Validate(order);
foreach (var error in result.Errors)
{
	OrderSchema.TryResolveMember(order, error.FullPath, out var container, out var memberName);
	// error.FullPath = Lines[0].Sku  ->  container = order.Lines[0], memberName = "Sku"
}
```

## JSON names

```csharp
var jsonPath = OrderSchema.ToJsonNamedPath(error.FullPath, JsonNamingPolicy.CamelCase.ConvertName);
var key = jsonPath.ToDottedString(); // customer_name (from [JsonPropertyName]), lines[0].sku
```

The adapter implements `IZodJsonPathMapper`, which is what a host uses when it renders
`ZodPathNaming.Json` keys under trimming.

## Registration

Each assembly gets one registration class naming every generated validator:

```csharp
// Without DI
IZodSchemaFactory factory = new ZodSchemaFactory();
ZodSharp.Generated.ContosoOrdersApiZodSharpSchemas.Register(factory);

// With DI (Microsoft.Extensions.DependencyInjection referenced)
services.AddZodSharpContosoOrdersApiSchemas();
```

`AddZodSharp{Assembly}Schemas()` calls `services.AddZodSchemaRegistration(...)`, which also registers
the factory (`AddZodSharpFactory`) when it is missing; the factory applies every `ZodSchemaRegistration`
when it is first resolved. Unlike `RegisterFromAssembly` and `AddZodSharp(o => o.ScanAssemblies...)`,
nothing is discovered through metadata, so it is the form to use for trimmed and Native AOT apps,
including Blazor WebAssembly.

## Async graph

- Nested schemas' `ValidateAsync` is awaited and its errors are re-pathed under the member, so a nested
  type's custom async method reports its errors (it used to be dropped).
- A custom async method may take `(T value, CancellationToken ct)` or
  `(T value, IServiceProvider services, CancellationToken ct)`. The services are the ones passed to
  `ValidateAsync`; calling `ValidateAsync` without services on a type with a service-aware method
  throws `InvalidOperationException`.
- `[ZodAsyncMember(params string[] members)]` on the custom method makes `ValidateMemberAsync` run it
  when one of the listed members is validated, keeping only the errors it reports for that member.
- The method must be reachable from the generated code: on the model type it must be `static` and
  `internal` or `public` (`ZODSGEN014` for `private`).

## Options validators

The generated `{Type}Validator : IValidateOptions<T>` reports each failure as
`"{path}: {message} ({code})"`. On .NET 11 it also implements `IAsyncValidateOptions<T>` when the type
has asynchronous rules, so `ValidateOnStart()` runs them.

## C# 15 unions and closed hierarchies

A member typed as a C# union validates the active case with that case's schema; a member typed as a
closed class validates with the runtime derived type's schema. Error paths are those of the case (no
`Value` segment). Each derived type of a closed class needs `[ZodSchema]` (`ZODSGEN049`).

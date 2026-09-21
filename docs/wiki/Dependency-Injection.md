# Dependency Injection

Purview.ZodSharp can resolve validators through an `IZodSchemaFactory` registry, and can validate options objects through `IValidateOptions<T>`.

## IZodSchemaFactory

`IZodSchemaFactory` (namespace `ZodSharp.Core`) resolves validators by type:

| Member | Behaviour |
|---|---|
| `Resolve<T>()` | returns `IZodSchemaValidator<T>?` or `null` when unregistered |
| `ResolveRequired<T>()` | returns the validator or throws `InvalidOperationException` |
| `Validate<T>(T value)` | validates through the registered validator for `T` |
| `Register<T>(IZodSchemaValidator<T>)` | registers a validator |
| `Register(Type, IZodSchemaValidator)` | non-generic registration |
| `TryRegister<T>(IZodSchemaValidator<T>)` | returns `false` if already registered |
| `IsRegistered<T>()` | checks for a registration |

The default implementation is `ZodSchemaFactory` (concurrent dictionary keyed by `Type`).

`IZodSchemaValidator<T>` extends `IZodSchema<T>`, so a resolved validator validates like any other schema. Hand-built schemas can be registered by wrapping them:

```csharp
using ZodSharp.Core;

factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(myObjectSchema));
```

## Registering source-generated validators

The source generator emits `[assembly: ZodSchemaGenerated(typeof({Type}))]` attributes and a `{Type}SchemaValidator` adapter. Scan an assembly to register every generated validator:

```csharp
factory.RegisterFromAssembly(typeof(User).Assembly);
// or
factory.RegisterFromAssembly<User>();
```

`ZodSchemaFactoryExtensions.RegisterFromAssembly` looks up `{TypeName}SchemaValidator` in the target type's namespace/assembly and registers it. It throws `InvalidOperationException` when the validator type or `IZodSchemaValidator` implementation is missing.

## Registering the factory in DI

The core package provides a `Microsoft.Extensions.DependencyInjection` extension:

```csharp
builder.Services.AddZodSharpFactory(factory => factory.RegisterFromAssembly(typeof(User).Assembly));
```

`AddZodSharpFactory(Action<IZodSchemaFactory>? configure = null)` registers a singleton `IZodSchemaFactory` and invokes the configuration callback. It does **not** auto-discover source-generated validators — call `RegisterFromAssembly` yourself inside the callback (or register hand-built validators).

> [!IMPORTANT]
> The factory is registered only if one is not already present (`TryAdd` semantics): the **first** `AddZodSharpFactory` (or `AddZodSharp`) call wins, and any later calls — including their `configure` callbacks — are ignored. State is never overwritten, so calling it more than once is safe.

The `Purview.ZodSharp.AspNetCore` package offers the richer `AddZodSharp` with exact assembly scans,
assembly-graph scans, loaded-assembly scans, and additive assembly-contribution helpers — see
[ASP.NET Core Integration](AspNetCore-Integration.md).

### Choosing a registration method

| Method | Package | Registers | Use when |
|---|---|---|---|
| `AddZodSharpFactory(configure)` | core `Purview.ZodSharp` | singleton `IZodSchemaFactory` | Any .NET host (console, worker, library, web) where you want manual control — you register validators yourself in the `configure` callback. |
| `AddZodSharp(options)` | `Purview.ZodSharp.AspNetCore` | singleton `IZodSchemaFactory` + auto-registers generated validators from configured assembly sources | ASP.NET Core apps that want factory registration plus optional assembly-source configuration. |
| `AddZodSharpAssembly(...)`, `AddZodSharpAssemblyGraph(...)`, `AddZodSharpLoadedAssemblies()` | `Purview.ZodSharp.AspNetCore` | additive generated-validator assembly source contributions | Modular ASP.NET Core apps where deeper layers or implementation packages contribute schema assemblies without central coordination. |
| `AddZodSharpProblemDetails(...)` | `Purview.ZodSharp.AspNetCore` | `ZodExceptionHandler` + ProblemDetails services only — does **not** register the factory | Mapping thrown `ZodException`s to `ProblemDetails` automatically; pair it with one of the factory registrations above when you also need DI validator resolution. |
| `AddZodSchemaOptionsValidator<T>(...)` | core `Purview.ZodSharp` | singleton `IValidateOptions<T>` | Validating options objects; requires a factory registered first via `AddZodSharpFactory` or `AddZodSharp`. |

## Validating options objects

Wire generated validators into the options framework so invalid configuration fails fast:

```csharp
builder.Services.AddZodSharpFactory(factory => factory.RegisterFromAssembly(typeof(UserOptions).Assembly));
builder.Services.AddZodSchemaOptionsValidator<UserOptions>();
```

`AddZodSchemaOptionsValidator<T>(MissingValidatorBehavior behavior = MissingValidatorBehavior.Throw)` registers a singleton `IValidateOptions<T>` (`ZodSchemaOptionsValidator<T>`) that resolves `IZodSchemaFactory` from DI and validates `T` when options are instantiated. The factory must already be registered — `AddZodSchemaOptionsValidator<T>` only wires up the validator, and can be called once per options type without affecting factory state.

`MissingValidatorBehavior` controls what happens when no validator is registered for `T`:

- `Throw` (default) — resolves via `ResolveRequired<T>()`, throwing `InvalidOperationException` so a missing schema is surfaced loudly instead of silently skipping validation.
- `Ignore` — passes through untouched (`ValidateOptionsResult.Success`).

The `OptionsBuilder<TOptions>` extension chains for convenience and composes with the rest of the options framework:

```csharp
builder.Services
	.AddOptions<UserOptions>()
	.Bind(configuration.GetSection("User"))
	.AddZodSchemaValidator()
	.ValidateOnStart(); // from Microsoft.Extensions.Hosting — fail at startup, not first access
```

The source generator also auto-generates `IValidateOptions<T>` validators for types whose names end in configurable suffixes — see [Source Generator](Source-Generator.md).

## Example: consuming a factory

```csharp
public sealed class OrderService(IZodSchemaFactory factory)
{
    public void ValidateProduct(Product product)
    {
        var result = factory.Validate(product); // ValidationResult<Product>
    }
}
```

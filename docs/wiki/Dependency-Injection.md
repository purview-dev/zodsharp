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

The generator emits one registration class per assembly, `ZodSharp.Generated.{Assembly}ZodSharpSchemas`, whose
`Register(IZodSchemaFactory)` names every generated validator directly. With dependency injection it also emits
`services.AddZodSharp{Assembly}Schemas()` (`{Assembly}` is the assembly name in PascalCase without separators:
`Contoso.Orders` becomes `ContosoOrders`). Both are trim- and Native-AOT-safe, so they are the recommended way
to register generated validators, including in Blazor WebAssembly:

```csharp
// With dependency injection: one call per assembly that declares [ZodSchema] types.
builder.Services.AddZodSharpContosoOrdersSchemas();
builder.Services.AddZodSharpContosoBillingSchemas();

// Without it.
var factory = new ZodSchemaFactory();
ZodSharp.Generated.ContosoOrdersZodSharpSchemas.Register(factory);
```

The generator also emits `[assembly: ZodSchemaGenerated(typeof({Type}), typeof({Type}SchemaValidator))]`
attributes, which the reflection-based scan reads:

```csharp
factory.RegisterFromAssembly(typeof(User).Assembly);
// or
factory.RegisterFromAssembly<User>();
```

`ZodSchemaFactoryExtensions.RegisterFromAssembly` looks up `{TypeName}SchemaValidator` in the target type's namespace/assembly and registers it. It throws `InvalidOperationException` when the validator type or `IZodSchemaValidator` implementation is missing. It discovers validators through metadata, so it is not trim-safe; prefer the generated registration in trimmed or AOT applications.

## Registering the factory in DI

The core package provides a `Microsoft.Extensions.DependencyInjection` extension:

```csharp
builder.Services.AddZodSharpFactory(factory => factory.RegisterFromAssembly(typeof(User).Assembly));
```

`AddZodSharpFactory(Action<IZodSchemaFactory>? configure = null)` registers a singleton `IZodSchemaFactory`. When
the factory is first resolved it applies every `ZodSchemaRegistration` in the container, then invokes the
configuration callback. It does not scan assemblies — use the generated `AddZodSharp{Assembly}Schemas()`, or call
`RegisterFromAssembly` inside the callback (or register hand-built validators).

`AddZodSchemaRegistration(Action<IZodSchemaFactory> register)` adds a deferred registration and ensures a factory
is registered. It is what the generated extensions call, and it composes: a library can contribute its validators
without knowing whether the application has already called `AddZodSharpFactory`:

```csharp
builder.Services.AddZodSchemaRegistration(factory => factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(myObjectSchema)));
```

> [!IMPORTANT]
> The factory is registered only if one is not already present (`TryAdd` semantics): the **first** `AddZodSharpFactory` (or `AddZodSharp`) call wins, and any later calls — including their `configure` callbacks — are ignored. State is never overwritten, so calling it more than once is safe. `ZodSchemaRegistration`s are not affected by the ordering: each is its own service, and the factory applies all of them whichever call registered it.

The `Purview.ZodSharp.AspNetCore` package offers the richer `AddZodSharp` with exact assembly scans,
assembly-graph scans, loaded-assembly scans, and additive assembly-contribution helpers — see
[ASP.NET Core Integration](AspNetCore-Integration.md).

### Choosing a registration method

| Method | Package | Registers | Use when |
|---|---|---|---|
| `AddZodSharpFactory(configure)` | core `Purview.ZodSharp` | singleton `IZodSchemaFactory` | Any .NET host (console, worker, library, web) where you want manual control — you register validators yourself in the `configure` callback. |
| `AddZodSharp{Assembly}Schemas()` | generated into your assembly | a `ZodSchemaRegistration` for every generated validator in the assembly, plus the factory | Any host, and the only trim-safe option for generated validators (Native AOT, Blazor WebAssembly). |
| `AddZodSchemaRegistration(register)` | core `Purview.ZodSharp` | a deferred `ZodSchemaRegistration`, plus the factory | Libraries contributing validators without owning the factory registration. |
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

`AddZodSchemaOptionsValidator<T>(MissingValidatorBehavior behavior = MissingValidatorBehavior.Throw)` registers a singleton `IValidateOptions<T>` (`ZodSchemaOptionsValidator<T>`) that resolves `IZodSchemaFactory` from DI and validates `T` when options are instantiated. The factory must already be registered — `AddZodSchemaOptionsValidator<T>` only wires up the validator, and can be called once per options type without affecting factory state. It validates every options name. On .NET 11 the validator also implements `IAsyncValidateOptions<T>`, and the platform runs that for an `IValidateOptions<T>` registration, so `ValidateOnStart()` checks asynchronous and service-aware rules (in a scope) just as `OptionsBuilder<T>.AddZodSchemaValidator()` does; synchronous options access checks the synchronous rules only.

`MissingValidatorBehavior` controls what happens when no validator is registered for `T`:

- `Throw` (default) — resolves via `ResolveRequired<T>()`, throwing `InvalidOperationException` so a missing schema is surfaced loudly instead of silently skipping validation.
- `Ignore` — passes through untouched (`ValidateOptionsResult.Success`).

The `OptionsBuilder<TOptions>` extension chains for convenience and composes with the rest of the options framework. It validates only the builder's options name (`AddOptions<T>("Primary")` validates `"Primary"`, not every name):

```csharp
builder.Services
	.AddOptions<UserOptions>()
	.Bind(configuration.GetSection("User"))
	.AddZodSchemaValidator()
	.ValidateOnStart(); // from Microsoft.Extensions.Hosting — fail at startup, not first access
```

The second overload, `AddZodSchemaValidator(missingValidatorBehavior, validateOnStart, configurationSection)`,
calls `ValidateOnStart()` for you and records the configuration section the options are bound from, so a failure
names the setting. It does not bind; bind the section yourself (for example with `BindConfiguration`, which the
configuration binder source generator intercepts under Native AOT):

```csharp
builder.Services
	.AddOptions<SmtpOptions>()
	.BindConfiguration("Smtp")
	.AddZodSchemaValidator(MissingValidatorBehavior.Throw, validateOnStart: true, configurationSection: "Smtp");
```

`Purview.ZodSharp.AspNetCore` adds `BindAndValidateWithZod`, which binds, validates and (by default) validates on
start in one call. It takes a section path or an `IConfigurationSection`, and uses the reflection-based binder, so
it is annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`; use the overload above in an AOT application:

```csharp
builder.Services.AddOptions<SmtpOptions>().BindAndValidateWithZod("Smtp");
// or
builder.Services.AddOptions<SmtpOptions>().BindAndValidateWithZod(builder.Configuration.GetSection("Smtp"));
```

### Failure messages

Each failure in `ValidateOptionsResult.Failures` reads `{name}: {location}: {message} ({code})`:

- `{name}` is the options name, omitted for the default (unnamed) options;
- `{location}` is the configuration key when a section was recorded (`Smtp:Servers:0:Host`), otherwise the dotted
  path (`Servers[0].Host`), and `(root)` for an error on the options object itself.

```text
Smtp:Servers:0:Host: Host is required (missing_field)
```

The returned result is a `ZodValidateOptionsResult` (namespace `ZodSharp.Options`), which keeps `OptionsName`,
`ConfigurationSection` and the full `Errors` (`ImmutableArray<ValidationError>`) alongside the messages:

```csharp
var result = validator.Validate(Options.DefaultName, options);
if (result is ZodValidateOptionsResult { Errors: var errors })
{
    foreach (var error in errors)
        Console.WriteLine($"{error.FullPath}: {error.Code}");
}
```

### Asynchronous rules (.NET 11)

On .NET 11, `ZodSchemaOptionsValidator<T>` also implements `IAsyncValidateOptions<T>`, and
`OptionsBuilder<T>.AddZodSchemaValidator` registers it through the platform's `Validate<TValidator>()`. The platform
calls the asynchronous path only from `ValidateOnStart()`, so asynchronous and service-aware rules (a custom async
method taking `IServiceProvider`, an `AsyncValidationAttribute`, `IAsyncValidatableObject`) run at startup, each
validation in its own service scope. Synchronous options access (`IOptions<T>.Value`, monitor reloads, snapshots)
runs the synchronous rules only — that is the platform's contract. On earlier frameworks only the synchronous path
exists.

The source generator also auto-generates `IValidateOptions<T>` validators for types whose names end in configurable suffixes — see [Source Generator](Source-Generator.md#ivalidateoptions-support). Their failures read `{path}: {message} ({code})`.

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

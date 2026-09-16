# Dependency Injection

ZodSharp can resolve validators through an `IZodSchemaFactory` registry, and can validate options objects through `IValidateOptions<T>`.

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

`AddZodSharpFactory(Action<IZodSchemaFactory>? configure = null)` registers a singleton `IZodSchemaFactory` and invokes the configuration callback.

The `Purview.ZodSharp.AspNetCore` package offers the richer `AddZodSharp` with `ScanAssemblies` — see [ASP.NET Core Integration](AspNetCore-Integration.md).

## Validating options objects

Wire generated validators into the options framework so invalid configuration fails fast at startup:

```csharp
builder.Services.AddZodSchemaOptionsValidator<UserOptions>();
```

`AddZodSchemaOptionsValidator<T>()` registers a singleton `IValidateOptions<T>` (`ZodSchemaOptionsValidator<T>`) that resolves `IZodSchemaFactory` from DI and validates `T` when options are instantiated. Types without a registered validator pass through untouched (`ValidateOptionsResult.Success`).

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
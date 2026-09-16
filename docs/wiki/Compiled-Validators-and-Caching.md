# Compiled Validators and Caching

## CompiledValidator

`CompiledValidator` (namespace `ZodSharp.Expressions`) compiles a schema into an expression tree and a delegate.

```csharp
using ZodSharp;
using ZodSharp.Expressions;

var compiled = CompiledValidator.Compile(schema);
var result = compiled(value); // ValidationResult<T>

var parser = CompiledValidator.CompileParser(schema);
var value = parser(input); // T, throws ZodException on failure
```

| Member | Signature | Returns |
|---|---|---|
| `Compile<T>` | `Func<T, ValidationResult<T>> Compile<T>(IZodSchema<T, T> schema)` | compiled validation delegate |
| `CompileParser<T>` | `Func<T, T> CompileParser<T>(IZodSchema<T, T> schema)` | returns the value or throws `ZodException` |

The expression tree calls `IZodSchema<T, T>.Validate` on the schema bound as a constant, removing interface dispatch overhead.

## SchemaCache

`SchemaCache` (namespace `ZodSharp.Core`) is a `ConcurrentDictionary<string, object>`-backed cache for expensive schema construction.

```csharp
using ZodSharp.Core;

var schema = SchemaCache.GetOrCreate("user", () =>
    Z.Object().Field("name", Z.String()).Build());
```

| Member | Behaviour |
|---|---|
| `GetOrCreate<T>(string key, Func<T> factory)` | returns the cached instance or creates and stores it (`T : class`) |
| `TryGet<T>(string key, out T value)` | typed lookup |
| `Remove(string key)` | removes an entry |
| `Count` | number of cached entries |
| `Clear()` | empties the cache |

Schemas are immutable and shareable, so caching identical definitions avoids repeated construction cost across request boundaries.

## The source generator alternative

For the highest performance, prefer the compile-time source generator: `[ZodSchema]` emits a static validator with no runtime compilation or dispatch overhead. See [Source Generator](Source-Generator.md).
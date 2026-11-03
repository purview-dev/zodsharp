# Compiled Validators and Caching

## CompiledValidator

`CompiledValidator` (namespace `ZodSharp.Expressions`) returns a cached delegate for a schema.

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
| `Compile<T>` | `Func<T, ValidationResult<T>> Compile<T>(IZodSchema<T, T> schema)` | validation delegate |
| `CompileParser<T>` | `Func<T, T> CompileParser<T>(IZodSchema<T, T> schema)` | returns the value or throws `ZodException` |

The delegate binds `IZodSchema<T, T>.Validate` directly, and the result is cached per schema instance with
weak keys — so repeated calls for the same schema return the same delegate, and a discarded schema is still
collectable.

> **It does not make validation faster.** Despite the name, this type adds no optimisation over calling
> `schema.Validate(value)` yourself: the delegate performs the same single interface call. It exists to give
> you a `Func<T, ValidationResult<T>>` where one is wanted — a cached field, a dictionary of validators,
> something that takes a delegate. If you want genuinely faster validation, use the source generator.
>
> Earlier versions built an expression tree and called `LambdaExpression.Compile` on every invocation. That
> removed no dispatch (the tree was a single `Validate` call) and emitted a `DynamicMethod` that is never
> reclaimed in a non-collectible load context, so calling it per request leaked managed and native memory.
> It also made the package incompatible with Native AOT. Both are fixed; nothing is compiled at runtime now.

## SchemaCache

`SchemaCache` (namespace `ZodSharp.Core`) is a `ConcurrentDictionary<string, object>`-backed cache for
expensive schema construction.

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

### What to know before using it

A **fully built** schema is safe to cache and share across threads: validation only reads the rule set and
the description. But building a schema is **not** immutable — the fluent rule methods mutate the receiver
in place (see [Guarantees and Limitations](../quality/Guarantees-and-Limitations.md#fluent-rule-methods-mutate-the-receiver)).
So finish configuring a schema *before* putting it in the cache, and never apply a fluent rule method to a
schema you retrieved from it: that mutates the instance every other caller is sharing.

Three further properties of `SchemaCache` worth knowing, because it is process-global:

- **The key space is shared.** It is a single static dictionary keyed by an arbitrary string, so two
  libraries in the same process that both use `"user"` collide. Prefix your keys.
- **`GetOrCreate<T>` does not verify the cached type.** The same key used with a different `T` throws
  `InvalidCastException` at the point of retrieval.
- **There is no eviction or size bound.** A key derived from user input grows the dictionary for the
  lifetime of the process.

A static field, or your container's singleton lifetime, is usually a better fit than this cache. Prefer it
when you genuinely need lookup by name.

## The source generator alternative

For the highest performance, prefer the compile-time source generator: `[ZodSchema]` emits a static
validator with no runtime compilation or dispatch overhead. See [Source Generator](../generator/Source-Generator.md).

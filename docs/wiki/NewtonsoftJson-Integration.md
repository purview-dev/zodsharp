# Newtonsoft.Json Integration

The `Purview.ZodSharp.NewtonsoftJson` package adds Newtonsoft.Json deserialize-and-validate, validating converters, and JSON Schema import to the core library. The deserialize/serialize extension methods live in the `ZodSharp` namespace; the JSON Schema import types (and `Z.FromJsonSchema`) live in the `ZodSharp.JsonSchema.NewtonsoftJson` namespace.

## Install

```bash
dotnet add package Purview.ZodSharp.NewtonsoftJson
```

## Deserialize and validate

```csharp
using ZodSharp;

var userSchema = Z.Object()
    .Field("name", Z.String().Min(3))
    .Field("age", Z.Number().Min(0).Int())
    .Build();

var json = """{ "name": "John", "age": 30 }""";

var result = userSchema.DeserializeAndValidate(json);
if (result.IsSuccess)
    Console.WriteLine($"Valid: {result.Value}");
```

Async stream and `JToken` overloads:

```csharp
await using var stream = File.OpenRead("user.json");
var result = await userSchema.DeserializeAndValidateAsync(stream);

var jToken = JObject.Parse(json);
var result2 = userSchema.DeserializeAndValidate(jToken);
```

## Validate and serialize

```csharp
var result = userSchema.ValidateAndSerialize(user); // ValidationResult<string>
var result2 = await userSchema.ValidateAndSerializeAsync(user, stream, formatting: Formatting.Indented);
```

## Validating converter

```csharp
var converter = userSchema.CreateValidatingConverter();
var value = JsonConvert.DeserializeObject<User>(json, converter);
```

`CreateValidatingConverter<T>()` returns a non-generic `Newtonsoft.Json.JsonConverter`. Invalid JSON throws `ZodNewtonsoftJsonValidationException` (namespace `ZodSharp.Json`, a `JsonSerializationException`, so existing `catch` blocks keep working) with a `"Validation failed: ..."` message; its `Errors` property holds the full `ValidationError`s, located under the converted value's JSON path. The converter clones the `JsonSerializer` (without itself) to avoid recursion.

## API surface

| Member | Signature |
|---|---|
| `DeserializeAndValidate<T>` | `ValidationResult<T> DeserializeAndValidate<T>(this IZodSchema<T, T> schema, string json, JsonSerializerSettings? settings = null)` |
| `DeserializeAndValidate<T>` | `ValidationResult<T> DeserializeAndValidate<T>(this IZodSchema<T, T> schema, JToken token, JsonSerializer? serializer = null)` |
| `DeserializeAndValidateAsync<T>` | `Task<ValidationResult<T>> DeserializeAndValidateAsync<T>(this IZodSchema<T, T> schema, Stream jsonStream, JsonSerializerSettings? settings = null, CancellationToken cancellationToken = default)` |
| `ValidateAndSerialize<T>` | `ValidationResult<string> ValidateAndSerialize<T>(this IZodSchema<T, T> schema, T value, JsonSerializerSettings? settings = null, Formatting formatting = Formatting.None)` |
| `ValidateAndSerializeAsync<T>` | `Task<ValidationResult<string>> ValidateAndSerializeAsync<T>(this IZodSchema<T, T> schema, T value, Stream output, JsonSerializerSettings? settings = null, Formatting formatting = Formatting.Indented, CancellationToken cancellationToken = default)` |
| `CreateValidatingConverter<T>` | `JsonConverter CreateValidatingConverter<T>(this IZodSchema<T, T> schema)` |

## Failure codes

A failure to read the JSON (malformed JSON, a value that cannot be converted, a `null` document or token) produces one `ValidationError` with code `invalid_json` (`NewtonsoftJsonExtensions.InvalidJsonCode`) and category `invalid_type`. Its `FullPath` is the location the serializer reported (`items[1].age`), its `line` and `position` parameters hold the serializer's one-based line number and line position, and its message is the fixed `"The JSON value could not be converted to {expected}."`, where `expected` is a Zod type name (`string`, `number`, `object`, `array`, …). The serializer's own message, which names CLR types and can quote the input, is never surfaced. A failure raised by a validating converter is returned as the schema's own errors, located under the converter's path. The stream overload validates with `ValidateAsync`, so asynchronous refinements run (it used to call the synchronous `Validate`). A document that fails to read at `{ "Items": [ { "Age": 1 }, { "Age": "x" } ] }` reports `invalid_json` at `Items[1].Age` with `expected` `number`; when the expected type cannot be described, `expected` is the generic `"the expected type"`. A syntax error is located where Json.NET's reader stopped (`Items[1].Age` for `"Age":}` or `"Age":12x`); Json.NET accepts trailing commas, so `"Age":1,}` is not an error. A value that cannot be converted inside a value read by a validating converter keeps its full path (`Items[1].Age`, not `Age`).

## JSON Schema import

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.NewtonsoftJson;

var schema = Z.FromJsonSchema(jsonSchemaString);
```

See [JSON Schema Import](JsonSchema-Import.md) for the supported keywords, `$ref` handling, and the `JsonSchemaSerializerOptions` defaults.

## System.Text.Json vs Newtonsoft.Json

| Aspect | SystemTextJson | NewtonsoftJson |
|---|---|---|
| Async result type | `ValueTask<...>` | `Task<...>` |
| Options parameter | `System.Text.Json.JsonSerializerOptions` | `Newtonsoft.Json.JsonSerializerSettings` |
| Converter return | generic `JsonConverter<T>` | non-generic `JsonConverter` |
| `JToken` overload | no | yes |
| Formatting control | via `JsonSerializerOptions` | explicit `Newtonsoft.Json.Formatting` argument |
| Invalid-data exception | `ZodJsonValidationException` (a `JsonException`) | `ZodNewtonsoftJsonValidationException` (a `JsonSerializationException`) |
| JSON plumbing | `JsonElement` | `JToken`/`JObject`/`JArray` |

> [!NOTE]
> `Purview.ZodSharp.SystemTextJson` and `Purview.ZodSharp.NewtonsoftJson` are mutually exclusive integrations — pick the one that matches your JSON library. Both packages can be referenced from the same project without `extern alias` (their JSON Schema types live in the `ZodSharp.JsonSchema.SystemTextJson` / `ZodSharp.JsonSchema.NewtonsoftJson` namespaces), but both packages declare their extensions in the `ZodSharp` namespace with the same names. With both referenced, a call that passes no options or settings (`schema.DeserializeAndValidate(json)`, `DeserializeAndValidateAsync(stream)`, `ValidateAndSerialize(value)`, …) resolves to System.Text.Json, through its option-less overloads; pass a `JsonSerializerSettings` (or a `JToken`) to use Newtonsoft. `CreateValidatingConverter(schema)` has the same signature in both, so it is ambiguous (CS0121): call `NewtonsoftJsonExtensions.CreateValidatingConverter(schema)` or `SystemTextJsonExtensions.CreateValidatingConverter(schema)` directly.
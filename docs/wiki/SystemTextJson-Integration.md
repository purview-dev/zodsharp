# System.Text.Json Integration

The `Purview.ZodSharp.SystemTextJson` package adds System.Text.Json deserialize-and-validate, validating converters, and JSON Schema import to the core library. The deserialize/serialize extension methods live in the `ZodSharp` namespace; the JSON Schema import types (and `Z.FromJsonSchema`) live in the `ZodSharp.JsonSchema.SystemTextJson` namespace.

## Install

```bash
dotnet add package Purview.ZodSharp.SystemTextJson
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

Async stream overload:

```csharp
await using var stream = File.OpenRead("user.json");
var result = await userSchema.DeserializeAndValidateAsync(stream);
```

## Validate and serialize

```csharp
var result = userSchema.ValidateAndSerialize(user);            // ValidationResult<string>
var result2 = await userSchema.ValidateAndSerializeAsync(user, stream);
```

## Validating converter

```csharp
var converter = userSchema.CreateValidatingConverter();
var options = new JsonSerializerOptions { Converters = { converter } };
var value = JsonSerializer.Deserialize<User>(json, options);
```

`CreateValidatingConverter<T>()` returns a `System.Text.Json.Serialization.JsonConverter<T>`. When the JSON is invalid, deserialization throws `ZodJsonValidationException` (a `JsonException`) with a `"Validation failed: ..."` message; its `Errors` property holds the full `ValidationError`s, located under the converted value's JSON path. The converter strips itself from the options it uses internally to avoid recursion.

## API surface

| Member | Signature |
|---|---|
| `DeserializeAndValidate<T>` | `ValidationResult<T> DeserializeAndValidate<T>(this IZodSchema<T, T> schema, string json, JsonSerializerOptions? options = null)` |
| `DeserializeAndValidateAsync<T>` | `ValueTask<ValidationResult<T>> DeserializeAndValidateAsync<T>(this IZodSchema<T, T> schema, Stream jsonStream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `ValidateAndSerialize<T>` | `ValidationResult<string> ValidateAndSerialize<T>(this IZodSchema<T, T> schema, T value, JsonSerializerOptions? options = null)` |
| `ValidateAndSerializeAsync<T>` | `ValueTask<ValidationResult<string>> ValidateAndSerializeAsync<T>(this IZodSchema<T, T> schema, T value, Stream output, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `CreateValidatingConverter<T>` | `JsonConverter<T> CreateValidatingConverter<T>(this IZodSchema<T, T> schema)` |

## Failure codes

A failure to read the JSON (malformed JSON, a value that cannot be converted, a `null` document, or a type the serializer cannot handle — `JsonException`, `NotSupportedException`, `InvalidOperationException`) produces one `ValidationError` with code `invalid_json` (`SystemTextJsonExtensions.InvalidJsonCode`) and category `invalid_type`. Its `FullPath` is the location the serializer reported (`$.items[1].age` becomes `items[1].age`), its `line` and `position` parameters hold the serializer's zero-based line number and byte position in the line, and its message is the fixed `"The JSON value could not be converted to {expected}."`, where `expected` is a Zod type name (`string`, `number`, `object`, `array`, …). The serializer's own message, which names CLR types and can quote the input, is never surfaced. A failure raised by a validating converter is returned as the schema's own errors, located under the converter's path.

## JSON Schema import

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.SystemTextJson;

var schema = Z.FromJsonSchema(jsonSchemaString);
```

See [JSON Schema Import](JsonSchema-Import.md) for the supported keywords, `$ref` handling, and the `JsonSchemaSerializerOptions` defaults.

## Comparing with Newtonsoft

See the API comparison table on the [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md) page for the differences between the two JSON packages.
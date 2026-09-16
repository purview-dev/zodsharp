# System.Text.Json Integration

The `Purview.ZodSharp.SystemTextJson` package adds System.Text.Json deserialize-and-validate, validating converters, and JSON Schema import to the core library. All extension methods live in the `ZodSharp` namespace.

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

`CreateValidatingConverter<T>()` returns a `System.Text.Json.Serialization.JsonConverter<T>`. When the JSON is invalid, deserialization throws `JsonException` with a `"Validation failed: ..."` message. The converter strips itself from the options it uses internally to avoid recursion.

## API surface

| Member | Signature |
|---|---|
| `DeserializeAndValidate<T>` | `ValidationResult<T> DeserializeAndValidate<T>(this IZodSchema<T, T> schema, string json, JsonSerializerOptions? options = null)` |
| `DeserializeAndValidateAsync<T>` | `ValueTask<ValidationResult<T>> DeserializeAndValidateAsync<T>(this IZodSchema<T, T> schema, Stream jsonStream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `ValidateAndSerialize<T>` | `ValidationResult<string> ValidateAndSerialize<T>(this IZodSchema<T, T> schema, T value, JsonSerializerOptions? options = null)` |
| `ValidateAndSerializeAsync<T>` | `ValueTask<ValidationResult<string>> ValidateAndSerializeAsync<T>(this IZodSchema<T, T> schema, T value, Stream output, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `CreateValidatingConverter<T>` | `JsonConverter<T> CreateValidatingConverter<T>(this IZodSchema<T, T> schema)` |

## Failure codes

Deserialize/validation failures produce `ValidationError` entries with codes `deserialization_failed` and `json_error` in addition to the schema's own codes.

## JSON Schema import

`Z.FromJsonSchema` is available with this package referenced; see [JSON Schema Import](JsonSchema-Import.md).

## Comparing with Newtonsoft

See the API comparison table on the [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md) page for the differences between the two JSON packages.
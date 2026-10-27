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

`CreateValidatingConverter<T>()` returns a `System.Text.Json.Serialization.JsonConverter<T>`. When the JSON is invalid, deserialization throws `ZodJsonValidationException` (namespace `ZodSharp.Json`, a `JsonException`) with a `"Validation failed: ..."` message, so existing `catch (JsonException)` blocks keep working. Its `Errors` property (`ImmutableArray<ValidationError>`) holds the full errors - code, category, origin, bounds and parameters - located under the converted value's JSON path: a nested `Owner` whose `Name` failed reports `Owner.Name`, including when validating converters are nested. `DeserializeAndValidate` unwraps the exception and returns those errors. The converter strips itself from the options it uses internally to avoid recursion.

```csharp
try
{
    var value = JsonSerializer.Deserialize<User>(json, options);
}
catch (ZodJsonValidationException ex)
{
    foreach (var error in ex.Errors)
        Console.WriteLine($"{error.FullPath.ToDottedString()}: {error.Message} ({error.Code})");
}
```

## API surface

| Member | Signature |
|---|---|
| `DeserializeAndValidate<T>` | `ValidationResult<T> DeserializeAndValidate<T>(this IZodSchema<T, T> schema, string json, JsonSerializerOptions? options = null)` |
| `DeserializeAndValidateAsync<T>` | `ValueTask<ValidationResult<T>> DeserializeAndValidateAsync<T>(this IZodSchema<T, T> schema, Stream jsonStream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `ValidateAndSerialize<T>` | `ValidationResult<string> ValidateAndSerialize<T>(this IZodSchema<T, T> schema, T value, JsonSerializerOptions? options = null)` |
| `ValidateAndSerializeAsync<T>` | `ValueTask<ValidationResult<string>> ValidateAndSerializeAsync<T>(this IZodSchema<T, T> schema, T value, Stream output, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` |
| `CreateValidatingConverter<T>` | `JsonConverter<T> CreateValidatingConverter<T>(this IZodSchema<T, T> schema)` |

## Failure codes

A failure to read the JSON (malformed JSON, a value that cannot be converted, a `null` document, or a type the serializer cannot handle — `JsonException`, `NotSupportedException`, `InvalidOperationException`) produces one `ValidationError` with code `invalid_json` (`SystemTextJsonExtensions.InvalidJsonCode`) and category `invalid_type`. Its `FullPath` is the location the serializer reported (`$.items[1].age` becomes `items[1].age`), its `line` and `position` parameters hold the serializer's zero-based line number and byte position in the line, and its message is the fixed `"The JSON value could not be converted to {expected}."`, where `expected` is a Zod type name (`string`, `number`, `object`, `array`, …) found by walking the failing path through the serializer contract, or the generic `"the expected type"` when it cannot be described (for example malformed JSON read into a `Dictionary<string, object?>`). The serializer's own message, which names CLR types and can quote the input, is never surfaced. A failure raised by a validating converter is returned as the schema's own errors, located under the converter's path.

How precise the location is depends on what failed, and is exactly what System.Text.Json reports:

| Failure | `FullPath` for `{"items":[{"age":1},{"age":…}]}` |
|---|---|
| A value that cannot be converted (`"age":"x"`, `"age":true`) | `items[1].age` |
| A syntax error while reading a value (`"age":}`, `"age":12x`) | `items[1].age` |
| A syntax error between members (`"age":1,}`, a missing `:`) | `items[1]` - the element being read |
| Truncated JSON | the next element (`items[2]`) |
| Any failure inside a value read by a validating converter (`CreateValidatingConverter`) | a conversion failure keeps its full path (`items[1].age`); a syntax error is reported at the converted value (`items[1]`), because the converter - and, for a stream, the serializer's read-ahead - buffers the whole value first |

`expected` is found for members, array and list elements and dictionary values on every target framework, and binary members (`byte[]`, `Memory<byte>`, base64 in JSON) are `string`.

```csharp
var result = userSchema.DeserializeAndValidate("""{ "name": "John", "age": }""");
var error = result.Errors[0];
// error.Code                         == SystemTextJsonExtensions.InvalidJsonCode ("invalid_json")
// error.Category                     == ZodIssueCategories.InvalidType ("invalid_type")
// error.MessageFormat                == "The JSON value could not be converted to {expected}."
// error.Parameters["line"], ["position"] - where the serializer stopped
```

These are the same errors ASP.NET Core hosts project into ProblemDetails (see [ASP.NET Core Integration](AspNetCore-Integration.md)); OpenAPI documents describe the response shape (see [OpenAPI Integration](OpenAPI-Integration.md)).

## JSON Schema import

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.SystemTextJson;

var schema = Z.FromJsonSchema(jsonSchemaString);
```

See [JSON Schema Import](JsonSchema-Import.md) for the supported keywords, `$ref` handling, and the `JsonSchemaSerializerOptions` defaults.

## Comparing with Newtonsoft

See the API comparison table on the [Newtonsoft.Json Integration](NewtonsoftJson-Integration.md) page for the differences between the two JSON packages.
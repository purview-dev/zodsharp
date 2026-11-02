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

Each method also has a trim- and AOT-safe overload that takes a `JsonTypeInfo<T>` in place of the options. The two
async methods have a service-aware overload on `IZodSchemaValidator<T>` (a generated `{TypeName}SchemaValidator`), so
rules that resolve services run:

| Member | Signature |
|---|---|
| `DeserializeAndValidateAsync<T>` | `ValueTask<ValidationResult<T>> DeserializeAndValidateAsync<T>(this IZodSchemaValidator<T> validator, Stream jsonStream, JsonTypeInfo<T> jsonTypeInfo, IServiceProvider? services, CancellationToken cancellationToken = default)` |
| `ValidateAndSerializeAsync<T>` | `ValueTask<ValidationResult<string>> ValidateAndSerializeAsync<T>(this IZodSchemaValidator<T> validator, T value, Stream output, JsonTypeInfo<T> jsonTypeInfo, IServiceProvider? services, CancellationToken cancellationToken = default)` |

The package is browser-safe (Blazor WebAssembly). For `HttpClient` bodies and server validation problems, see
[HttpClient Integration](Http-Integration.md).

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

### Values rejected by a strict converter

A converter that rejects a value with ZodSharp errors - a Purview.ValueObjects strict converter throws
`new JsonException(message, zodException)` - is not a failure to read JSON. `DeserializeAndValidate*` (and the Http
package's `ReadFromJsonAndValidateAsync`) return those errors under the member that held the value, with JSON names
mapped back to member names through the supplied `JsonTypeInfo<T>`, and an error at the value object's own `Value`
named after that member:

```csharp
// {"customerEmail":"not-an-email","quantity":5}
var result = await validator.DeserializeAndValidateAsync(stream, AppJsonContext.Default.PlaceOrderRequest);
// result.Errors[0].FullPath  -> CustomerEmail.Value
// result.Errors[0].Message   -> "Field 'CustomerEmail' must be a valid email address."
```

The errors are exactly those validating the hydrated model reports. `ZodJsonExceptions` (core package) exposes the
same re-rooting for any exception (`TryGetErrors`, `TryReroot`, `Reroot`, `ToMemberPath`). A bare `ZodException`
thrown from a converter still propagates unchanged.

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

Both packages can be referenced from one project. Their extensions share the `ZodSharp` namespace and names, so this package also has option-less overloads (`DeserializeAndValidate(json)`, `DeserializeAndValidateAsync(stream)` and `(stream, cancellationToken)`, `ValidateAndSerialize(value)`, `ValidateAndSerializeAsync(value, output)` and `(value, output, cancellationToken)`): a call without options resolves to System.Text.Json instead of failing with CS0121. `CreateValidatingConverter(schema)` is the one call that stays ambiguous; call `SystemTextJsonExtensions.CreateValidatingConverter(schema)` directly.
# Purview.ZodSharp.SystemTextJson

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.SystemTextJson.svg)](https://www.nuget.org/packages/Purview.ZodSharp.SystemTextJson)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

System.Text.Json integration for [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp). Deserialize-and-validate JSON in a single pass, create validating `JsonConverter<T>` instances, and import JSON Schema definitions (`Z.FromJsonSchema`).

## Installation

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

## Validating converter

```csharp
var converter = userSchema.CreateValidatingConverter();
var options = new JsonSerializerOptions { Converters = { converter } };
var value = JsonSerializer.Deserialize<User>(json, options);
```

## Errors

A failure to read the JSON (malformed text, a `null` document, a value the serializer cannot convert, or a
`NotSupportedException`/`InvalidOperationException` thrown while deserializing) is reported as one error with code
`invalid_json` (`SystemTextJsonExtensions.InvalidJsonCode`), category `invalid_type` and the fixed message
`"The JSON value could not be converted to {expected}."`. The serializer's path becomes the error's `FullPath`
(`$.items[1].age` becomes `items[1].age`) and its line and byte position become the `line` and `position`
parameters. The serializer's own message is never surfaced, because it names CLR types and can quote the input.

The validating converter throws `ZodJsonValidationException` (namespace `ZodSharp.Json`, a `JsonException`) carrying the
schema's full `Errors`, located under the converter's JSON path; its message still starts with `"Validation failed: "`,
so existing `catch (JsonException)` blocks keep working. `DeserializeAndValidate` unwraps it into the result.

## UTC DateTimeOffset contract

The default `PreserveOffset` schema is exact and needs no converter. When the generated JSON Schema uses
`DateTimeOffsetSerialization.Utc`, add the matching converter so the serializer writes UTC and rejects a value
without an explicit `Z`/zero offset (or with a non-zero offset) on read:

```csharp
using ZodSharp.Json.Serialization.SystemTextJson;

var options = new JsonSerializerOptions
{
    Converters = { new UtcDateTimeOffsetConverter() },
};
```

`PreserveOffsetDateTimeOffsetConverter` is available to opt a type or property back out of a project-wide UTC
converter. See [JSON Schema Export](https://purview.dev/docs/zodsharp/json-schema-export/#temporal-sentinels-and-the-datetimeoffset-wire-form).

## Import from JSON Schema

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.SystemTextJson;

var schema = Z.FromJsonSchema(jsonSchemaString);
var result = schema.Validate(data);
```

This lets you share schemas defined in TypeScript/Zod with your .NET backend. (Export via `Z.ToJsonSchema` lives in the core package.)

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.SystemTextJson` on NuGet.

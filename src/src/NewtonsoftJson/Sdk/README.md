# Purview.ZodSharp.NewtonsoftJson

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.NewtonsoftJson.svg)](https://www.nuget.org/packages/Purview.ZodSharp.NewtonsoftJson)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

Newtonsoft.Json integration for [Purview.ZodSharp](https://www.nuget.org/packages/Purview.ZodSharp) [![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp). Deserialize-and-validate JSON in a single pass, create validating `JsonConverter` instances, and import JSON Schema definitions (`Z.FromJsonSchema`).

## Installation

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

Async stream overload and `JToken` overload:

```csharp
await using var stream = File.OpenRead("user.json");
var result = await userSchema.DeserializeAndValidateAsync(stream);

var jToken = JObject.Parse(json);
var result2 = userSchema.DeserializeAndValidate(jToken);
```

## Validating converter

```csharp
var converter = userSchema.CreateValidatingConverter();
var value = JsonConvert.DeserializeObject<User>(json, converter);
```

## UTC DateTimeOffset contract

The default `PreserveOffset` schema is exact and needs no converter. When the generated JSON Schema uses
`DateTimeOffsetSerialization.Utc`, add the matching converter so the serializer writes UTC and rejects a value
without an explicit `Z`/zero offset (or with a non-zero offset) on read:

```csharp
using Newtonsoft.Json;
using ZodSharp.Json.Serialization.NewtonsoftJson;

var settings = new JsonSerializerSettings().UseZodSharpUtcDateTimeOffset();
```

The helper sets `DateParseHandling = DateParseHandling.None`, which is required so the converter can inspect
the original offset, and adds the UTC converter. `UseZodSharpPreserveOffsetDateTimeOffset()` is the
per-type/property opt-out equivalent.

`PreserveOffsetDateTimeOffsetConverter` is available to opt a type or property back out of a project-wide UTC
converter. See [JSON Schema Export](https://purview.dev/docs/zodsharp/json-schema-export/#temporal-sentinels-and-the-datetimeoffset-wire-form).

## Import from JSON Schema

```csharp
using ZodSharp;
using ZodSharp.JsonSchema.NewtonsoftJson;

var schema = Z.FromJsonSchema(jsonSchemaString);
var result = schema.Validate(data);
```

This lets you share schemas defined in TypeScript/Zod with your .NET backend. (Export via `Z.ToJsonSchema` lives in the core package.)

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.NewtonsoftJson` on NuGet.

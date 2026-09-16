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

## Import from JSON Schema

```csharp
var schema = Z.FromJsonSchema(jsonSchemaString);
var result = schema.Validate(data);
```

This lets you share schemas defined in TypeScript/Zod with your .NET backend. (Export via `Z.ToJsonSchema` lives in the core package.)

## Documentation

- [Homepage](https://purview.dev/projects/zodsharp/)
- [Documentation](https://purview.dev/docs/zodsharp/)

## License

MIT — see the package metadata in `Purview.ZodSharp.NewtonsoftJson` on NuGet.

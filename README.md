# ZodSharp

[![NuGet version](https://img.shields.io/nuget/v/Purview.ZodSharp.svg)](https://www.nuget.org/packages/Purview.ZodSharp)
[![Release](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/zodsharp/actions/workflows/release.yml)

**ZodSharp** is a high-performance schema validation library for C#, ported from TypeScript [Zod](https://github.com/colinhacks/zod). It features zero-allocation validation, struct-based rules, fluent API, and source generator support for maximum performance.

This project is a fork of [guinhx/ZodSharp](https://github.com/guinhx/ZodSharp), maintained at [github.com/purview-dev/zodsharp](https://github.com/purview-dev/zodsharp) under the `Purview.*` package IDs.

## Key Features

- **Zero-allocation validation** - Minimizes allocations using structs and `Span<T>`
- **Struct-based rules** - Validation rules implemented as structs to avoid GC
- **Fluent API** - Fluent and extensible API similar to original Zod
- **Type-safe** - Strong typing with advanced C# generics
- **High performance** - Sub-microsecond validation times, 10x faster than reflection-based validation
- **Cross-platform** - Works on .NET 8.0, .NET 9.0 and .NET 10.0
- **Source Generators** - Compile-time validator generation with `[ZodSchema]` attribute
- **DataAnnotations Support** - Automatic validation from `[Required]`, `[StringLength]`, `[Length]`, `[MinLength]`, `[MaxLength]`, `[Range]`, `[RegularExpression]`, `[AllowedValues]`, `[DeniedValues]`, `[EmailAddress]`, etc.  

## Installation

Install the core `Purview.ZodSharp` package, plus the optional JSON integration packages you need:

### NuGet Package Manager

```powershell
Install-Package Purview.ZodSharp
Install-Package Purview.ZodSharp.SystemTextJson
Install-Package Purview.ZodSharp.NewtonsoftJson
Install-Package Purview.ZodSharp.AspNetCore
```

### .NET CLI

```bash
dotnet add package Purview.ZodSharp
dotnet add package Purview.ZodSharp.SystemTextJson
dotnet add package Purview.ZodSharp.NewtonsoftJson
dotnet add package Purview.ZodSharp.AspNetCore
```

### PackageReference

```xml
<PackageReference Include="Purview.ZodSharp" Version="2.0.0" />
<PackageReference Include="Purview.ZodSharp.SystemTextJson" Version="2.0.0" />
<PackageReference Include="Purview.ZodSharp.NewtonsoftJson" Version="2.0.0" />
<PackageReference Include="Purview.ZodSharp.AspNetCore" Version="2.0.0" />
```

- **Purview.ZodSharp** — Core validation library and source generator (`[ZodSchema]`). See its [package README](src/src/ZodSharp/Sdk/README.md).
- **Purview.ZodSharp.SystemTextJson** — System.Text.Json integration and JSON Schema import. See its [package README](src/src/SystemTextJson/Sdk/README.md).
- **Purview.ZodSharp.NewtonsoftJson** — Newtonsoft.Json integration and JSON Schema import. See its [package README](src/src/NewtonsoftJson/Sdk/README.md).
- **Purview.ZodSharp.AspNetCore** — ASP.NET Core ProblemDetails integration. See its [package README](src/src/AspNetCore/Sdk/README.md).

## TypeScript and fixture tooling

This repo uses Bun for the TypeScript-side tooling. Bun can execute TypeScript files directly, so `tsx` is not required.

```bash
bun install
bun run test
bun run generate-fixtures
```

The fixture generator script is intentionally run via Bun rather than `npx tsx` or Node because it keeps the TypeScript workflow consistent with the repo's Bun-based setup.

## Differences from the original fork

This repository is a fork of [guinhx/ZodSharp](https://github.com/guinhx/ZodSharp). Compared with the original, this version:

- **Publishes `Purview.*` packages.** A single `ZodSharp` package is split into `Purview.ZodSharp` (core + source generator), plus `Purview.ZodSharp.SystemTextJson`, `Purview.ZodSharp.NewtonsoftJson`, and `Purview.ZodSharp.AspNetCore` integration packages.
- **Multi-targets `net8.0`, `net9.0`, and `net10.0`.** The original targeted .NET 9.0 and .NET Standard 2.1. The source generator remains on `netstandard2.0` so it can run in any compiler host.
- **Adds System.Text.Json integration.** The original shipped Newtonsoft.Json integration only; JSON deserialize-and-validate is now available for both major JSON libraries.
- **Adds JSON Schema interoperability.** Schemas can be exported via `Z.ToJsonSchema` and imported via `Z.FromJsonSchema`, enabling cross-language reuse with TypeScript/Zod.
- **Adds ASP.NET Core ProblemDetails integration.** Failed validation results convert directly to `HttpValidationProblemDetails` via `result.ToHttpValidationProblemDetails()`.
- **Expands DataAnnotations support.** `[Length]`, `[MinLength]`, `[MaxLength]`, `[RegularExpression]`, `[AllowedValues]`, `[DeniedValues]`, `[EmailAddress]`, and more, with structured size failures (`Code`, `Origin`, `Minimum`/`Maximum`, `Inclusive`, `Path`).
- **Changes generated composition methods.** The original `.And()`, `.Or()`, and `.Refine()` are superseded by value-first `.ApplyAnd()`, `.ApplyOr()`, and `.ApplyRefine()`.

## Usage Examples

### Basic Validation

```csharp
using ZodSharp;
using ZodSharp.Core;

// String validation
var nameSchema = Z.String().Min(3).Max(50);
var result = nameSchema.Validate("John");
if (result.IsSuccess)
{
    Console.WriteLine($"Valid name: {result.Value}");
}

// Number validation
var ageSchema = Z.Number().Min(0).Max(120).Int();
var ageResult = ageSchema.Validate(25.0);

// Additional number validations
var positiveSchema = Z.Number().Positive();
var negativeSchema = Z.Number().Negative();
var multipleOfSchema = Z.Number().MultipleOf(10); // Must be multiple of 10
var finiteSchema = Z.Number().Finite(); // Not Infinity
var safeSchema = Z.Number().Safe(); // Safe integer

// Email validation
var emailSchema = Z.String().Email();
var emailResult = emailSchema.Validate("user@example.com");

// URL validation
var urlSchema = Z.String().Url();
var urlResult = urlSchema.Validate("https://example.com");

// UUID validation
var uuidSchema = Z.String().UUID();
var uuidResult = uuidSchema.Validate("550e8400-e29b-41d4-a716-446655440000");

// Version-specific UUID validation (e.g. RFC 9562 version 7)
var uuidV7Schema = Z.String().UUID(UuidVersion.V7);
var uuidV7Result = uuidV7Schema.Validate("0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");

// String transformations
var trimmedSchema = Z.String().Trim();
var upperSchema = Z.String().ToUpper();
var lowerSchema = Z.String().ToLower();

// String prefixes and suffixes
var prefixSchema = Z.String().StartsWith("https://");
var suffixSchema = Z.String().EndsWith(".com");

// Exact length
var exactLengthSchema = Z.String().Length(10);
```

### Object Validation

```csharp
var userSchema = Z.Object()
    .Field("name", Z.String().Min(1))
    .Field("age", Z.Number().Min(0).Max(120))
    .Field("email", Z.String().Email())
    .Build();

var userData = new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "age", 30.0 },
    { "email", "john@example.com" }
};

var result = userSchema.Validate(userData);
if (result.IsSuccess)
{
    var validatedUser = result.Value;
    // Use validatedUser...
}
```

### Array Validation

```csharp
var numbersSchema = Z.Array(Z.Number()).Min(1).Max(10);
var result = numbersSchema.Validate(new[] { 1.0, 2.0, 3.0 });

// Exact length
var exactLengthSchema = Z.Array(Z.String()).Length(5);

// Non-empty array
var nonEmptySchema = Z.Array(Z.String()).NonEmpty();
```

### Optional Fields

```csharp
var optionalSchema = Z.Optional(Z.String());
var result1 = optionalSchema.Validate(null); // Success
var result2 = optionalSchema.Validate("value"); // Success
```

### Error Handling

```csharp
try
{
    var value = nameSchema.Parse("AB"); // Too short - throws
}
catch (ZodException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"  - {string.Join(".", error.Path)}: {error.Message}");
    }
}

// Or use SafeParse for non-throwing validation
var result = nameSchema.SafeParse("AB");
if (!result.IsSuccess)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error.Message}");
    }
}
```

## Performance

ZodSharp is designed for maximum performance with zero-allocation validation and struct-based rules. Here's what makes it fast:

### Performance Characteristics

**Typical validation times** (measured with the committed benchmark suite on .NET 10.0, Release mode, 13th Gen Intel Core i9-13900KF):

- Boolean validation: **~2 ns** per validation (zero allocation)
- Number validation: **~11 ns** per validation (zero allocation)
- Simple string validation: **~42 ns** per validation (zero allocation)
- Small string array (5 items): **~57 ns** per validation (zero allocation)
- Simple object (2 fields): **~90 ns** per validation (zero allocation)
- Deeply nested object (4 levels): **~199 ns** per validation (zero allocation)
- Medium object (6 fields): **~340 ns** per validation (zero allocation)
- Complex object (13 fields with nesting): **~796 ns** per validation (zero allocation)
- Wide object (50 fields): **~2.2 μs** per validation (zero allocation)
- Medium array (100 items): **~4.6 μs** per validation (zero allocation)
- Large array (1000 items): **~11.7 μs** per validation (zero allocation)

**Memory efficiency**:

- **Zero allocations for every valid-input path** — primitives, strings, arrays, objects, unions, and discriminated unions all validate without allocating when the input is valid
- Struct-based rules avoid GC pressure
- No reflection overhead in hot paths
- Allocations only occur on the failure path (error collection) and inside string transforms (`ToLower`/`ToUpper`/`Trim` produce a new string)

Full results for every suite are in the [performance README](src/src/Benchmarks/README.md).

### Performance Optimizations

ZodSharp implements several optimizations for maximum performance:

#### 1. Zero-allocation Validation

- Validation rules implemented as `struct` to avoid allocations
- Use of `Span<T>` and `ReadOnlySpan<T>` when appropriate
- Array pooling via `ArrayPool<T>` for zero-allocation helpers

#### 2. Struct-based Rules

All validation rules are structs:

```csharp
public readonly struct MinLengthRule : IValidationRule<string>
{
    // Zero allocation validation
}
```

#### 3. Compiled Validators

Use expression trees to compile validators at runtime for maximum speed:

```csharp
using ZodSharp.Expressions;

var compiled = CompiledValidator.Compile(schema);
var result = compiled(value); // Ultra-fast validation
```

#### 4. Fluent API

Fluent API that allows schema composition:

```csharp
var schema = Z.String()
    .Min(3)
    .Max(50)
    .Email()
    .Describe("User email address");
```

### Performance Benchmarks

We maintain comprehensive performance tests in `src/src/Benchmarks`. Run them yourself:

```bash
# Run all performance benchmarks
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release

# Run specific test suites (the `--` passes the filter to BenchmarkDotNet)
dotnet run --project src/src/Benchmarks/Benchmarks.csproj -c Release -- --filter "*MemoryPerformanceTests*"
```

**Key performance highlights**:

- **10x faster** than reflection-based validation libraries
- **Zero allocations** for primitive validations
- **Sub-microsecond** validation for simple types
- **Minimal GC pressure** with struct-based architecture
- **Scalable** performance even with complex nested schemas

See the [performance README](src/src/Benchmarks/README.md) for detailed benchmark results and optimization tips.

## Architecture

```
src/
├── src/
│   ├── ZodSharp/              # Core validation library (Core, Schemas, Rules, JsonSchema)
│   ├── SourceGenerators/      # Compile-time [ZodSchema] generator
│   ├── SystemTextJson/        # System.Text.Json integration and JSON Schema import
│   ├── NewtonsoftJson/        # Newtonsoft.Json integration and JSON Schema import
│   ├── AspNetCore/            # ASP.NET Core ProblemDetails integration
│   ├── Examples.CLI/          # Usage examples
│   └── Benchmarks/   # BenchmarkDotNet performance suite
└── tests/                     # TUnit test projects
```

The source generator itself targets `netstandard2.0` so it can run in any compiler. The library packages target `net8.0`, `net9.0` and `net10.0`.

## Advanced Features

### Transforms

Transform values during validation:

```csharp
var schema = Z.String().Transform(s => s.ToUpper());
var result = schema.Validate("hello"); // "HELLO"
```

### Refinements

Add custom validations:

```csharp
var schema = Z.Number().Refine(n => n % 2 == 0, "Must be even");
var result = schema.Validate(4); // Success
```

### Lazy Evaluation

Create recursive and circular schemas:

```csharp
var categorySchema = Z.Lazy<Dictionary<string, object?>>(() => 
    Z.Object()
        .Field("name", Z.String())
        .Field("subcategories", Z.Array(categorySchema))
        .Build()
);
```

### Discriminated Unions

Optimized unions with discriminator:

```csharp
var union = Z.DiscriminatedUnion("type")
    .Option("user", userSchema)
    .Option("admin", adminSchema)
    .Build();
```

### Default Values

Default values when input is null:

```csharp
var schema = Z.String().Default("unknown");
var result = schema.Validate(null); // "unknown"
```

### JSON Integration

ZodSharp ships separate integration packages for the two major .NET JSON libraries.

#### System.Text.Json (`Purview.ZodSharp.SystemTextJson`)

```csharp
using ZodSharp;

// Deserialize and validate from string
var result = schema.DeserializeAndValidate(jsonString);

// Deserialize and validate from stream (async)
var result2 = await schema.DeserializeAndValidateAsync(jsonStream);

// Create JsonConverter with validation
var converter = schema.CreateValidatingConverter();
```

#### Newtonsoft.Json (`Purview.ZodSharp.NewtonsoftJson`)

```csharp
using ZodSharp;

// Deserialize and validate from string
var result = schema.DeserializeAndValidate(jsonString);

// Deserialize and validate from stream (async)
var result2 = await schema.DeserializeAndValidateAsync(jsonStream);

// Deserialize and validate from JToken
var result3 = schema.DeserializeAndValidate(jToken);

// Create JsonConverter with validation
var converter = schema.CreateValidatingConverter();
```

### JSON Schema Interoperability

Share schemas between TypeScript (Zod) and C# (ZodSharp) using JSON Schema. This enables infinite interoperability, allowing you to define a schema in one language and reuse it in another.

The export API (`Z.ToJsonSchema`) lives in the core `Purview.ZodSharp` package. The import API (`Z.FromJsonSchema`) is provided by the JSON integration package you choose — either `Purview.ZodSharp.SystemTextJson` or `Purview.ZodSharp.NewtonsoftJson`.

#### Export to JSON Schema (ZodSharp -> JSON Schema)

```csharp
var userSchema = Z.Object()
    .Field("name", Z.String().Min(3))
    .Field("email", Z.String().Email())
    .Field("age", Z.Number().Min(0).Int())
    .Build();

// Convert to JSON Schema object
var jsonSchema = Z.ToJsonSchema<Dictionary<string, object?>>(userSchema, new ToJsonSchemaOptions
{
    Title = "User",
    Id = "https://example.com/schemas/user.json"
});

// Serialize with your preferred JSON library
// System.Text.Json (add Purview.ZodSharp.SystemTextJson):
using ZodSharp.JsonSchema;
var systemTextJson = System.Text.Json.JsonSerializer.Serialize(jsonSchema, JsonSchemaSerializerOptions.Default);

// Newtonsoft.Json (add Purview.ZodSharp.NewtonsoftJson):
using ZodSharp.JsonSchema;
var newtonsoftJson = JsonConvert.SerializeObject(jsonSchema, JsonSchemaSerializerOptions.Default);
```

#### Import from JSON Schema (JSON Schema -> ZodSharp)

Add either `Purview.ZodSharp.SystemTextJson` or `Purview.ZodSharp.NewtonsoftJson` to your project, then:

```csharp
var jsonSchemaString = @"{
    ""type"": ""object"",
    ""properties"": {
        ""name"": { ""type"": ""string"", ""minLength"": 3 },
        ""email"": { ""type"": ""string"", ""format"": ""email"" }
    },
    ""required"": [""name"", ""email""]
}";

// Parse into ZodSharp schema
var userSchema = Z.FromJsonSchema(jsonSchemaString);

// Validate data
var result = userSchema.Validate(userData);
```

#### Cross-Platform Scenario

**Frontend (TypeScript/Zod):**

```typescript
import { z } from "zod";

const UserSchema = z.object({
  username: z.string().min(3),
  email: z.string().email()
});

// Zod v4+ natively supports JSON Schema conversion
const jsonSchema = z.toJSONSchema(UserSchema);
// Send jsonSchema to backend...
```

**Backend (C#/ZodSharp):**

```csharp
// Receive jsonSchema...
var userSchema = Z.FromJsonSchema(jsonSchemaString);
var result = userSchema.Validate(incomingData);
```

### Compiled Validators

Compiled validators for maximum performance:

```csharp
using ZodSharp.Expressions;

var compiled = CompiledValidator.Compile(schema);
var result = compiled(value); // Ultra-fast validation
```

### Schema Caching

Intelligent schema caching:

```csharp
using ZodSharp.Core;

var schema = SchemaCache.GetOrCreate("user", () => 
    Z.Object().Field("name", Z.String()).Build()
);
```

### Source Generators

Generate zero-allocation validators at compile time:

```csharp
using System.ComponentModel.DataAnnotations;
using ZodSharp;

[ZodSchema]
public class User
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(0, 120)]
    public int Age { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

// Auto-generated validator
var result = UserSchema.Validate(user);
var validated = UserSchema.Parse(user); // Throws on failure

// Value-first composition methods (validate a value, then run an extra predicate)
var refined = UserSchema.ApplyRefine(user, u => u.Age >= 18, "Must be adult");
var combined = UserSchema.ApplyAnd(user, u => u.Name.Length > 5, "Name too short");
var either = UserSchema.ApplyOr(user, u => u.Age < 18, "Must be an adult or a minor with consent");
```

**Features:**

- Automatic validation from DataAnnotations attributes
- Zero-reflection, zero-allocation validators
- Value-first composition methods (`.ApplyAnd()`, `.ApplyOr()`, `.ApplyRefine()`) plus instance schema-composing composition (`.Refine()`, `.SuperRefine()`, `.Pipe()`, `.Catch()`, `.Prefault()`, `.Default()`)
- Supports classes, structs, and records

#### Supported DataAnnotations size validators

`[Length]`, `[StringLength]`, `[MinLength]`, and `[MaxLength]` generate direct `Length` or `Count` access when possible:

- `string` -> `.Length`
- arrays, including rectangular arrays -> `.Length`
- jagged arrays -> outer-array `.Length`
- countable collections -> `.Count`
- `IEnumerable` / `IEnumerable<T>` -> a single counted pass with a non-enumerating fast path

`[Length]` follows DataAnnotations null semantics: `null` is valid unless `[Required]` is also present.

Other supported DataAnnotations validators:

- `[Range]` on numeric types plus parsed `decimal`, `DateTime`, `DateOnly`, and `TimeOnly` bounds
- `[RegularExpression]` on strings, with DataAnnotations-compatible `null` and empty-string behaviour
- `[AllowedValues]` and `[DeniedValues]` using generated typed equality checks instead of runtime attribute execution
- `[EmailAddress]` on strings

Structured size failures expose:

- `Code`: `too_small` or `too_big`
- `Origin`: `string`, `array`, or `collection`
- `Minimum` / `Maximum`
- `Inclusive`
- `Path`

Example:

```csharp
using System.ComponentModel.DataAnnotations;

[ZodSchema]
public sealed class Basket
{
    [Required]
    [Length(2, 5)]
    public List<string>? Items { get; set; }
}

var result = BasketSchema.Validate(new Basket { Items = ["apple"] });
// result.Errors[0].Code == "too_small"
// result.Errors[0].Minimum == 2
```

### ASP.NET Core ProblemDetails

Install `Purview.ZodSharp.AspNetCore` to convert failed validation results into standard ASP.NET Core payloads while preserving structured issues:

```csharp
using ZodSharp.AspNetCore;

var result = BasketSchema.Validate(basket);

if (!result.IsSuccess)
{
    var problem = result.ToHttpValidationProblemDetails();
    return Results.ValidationProblem(
        problem.Errors,
        extensions: new Dictionary<string, object?>
        {
            ["issues"] = problem.Extensions["issues"]
        });
}
```

### Span<T> Validation

Zero-allocation string validation using spans:

```csharp
var schema = Z.String().Min(3).Max(50).Email();
var span = "user@example.com".AsSpan();
var result = schema.ValidateSpan(span);
```

## Dependency Management

Package versions are declared centrally in `Directory.Packages.props`. No `packages.lock.json` files are committed; package resolution is left to NuGet at restore time.

> Note: a version like `13.0.4` in `Directory.Packages.props` is a *minimum* version requirement, not an exact pin, so the resolved graph can drift as newer packages are published.

## License

MIT — the license is declared in the NuGet package metadata (`PackageLicenseExpression`) and in `package.json`.

## Contributing

Contributions are welcome! Please open an issue or pull request.

## Acknowledgments

- [guinhx/ZodSharp](https://github.com/guinhx/ZodSharp) — the original project this repository was forked from.
- [Zod](https://github.com/colinhacks/zod)

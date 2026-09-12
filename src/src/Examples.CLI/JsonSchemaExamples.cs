using Newtonsoft.Json;
using ZodSharp.JsonSchema;

namespace ZodSharp.Examples.CLI;

/// <summary>
/// Examples demonstrating JSON Schema interoperability between ZodSharp and TypeScript Zod.
/// </summary>
public static class JsonSchemaExamples
{
	public static void RunAll()
	{
		Console.WriteLine("=== JSON Schema Interoperability Examples ===\n");

		DemoToJsonSchema();
		DemoFromJsonSchema();
		DemoRoundtrip();
		DemoCrossplatformScenario();
	}

	/// <summary>
	/// Example: Convert ZodSharp schema to JSON Schema
	/// </summary>
	static void DemoToJsonSchema()
	{
		Console.WriteLine("--- ToJsonSchema: ZodSharp -> JSON Schema ---");

		// Define a user schema in ZodSharp
		var userSchema = Z.Object()
			.Field("name", Z.String().Min(1).Max(100))
			.Field("email", Z.String().Email())
			.Field("age", Z.Number().Min(0).Max(150).Int())
			.Build();

		// Convert to JSON Schema
		var jsonSchema = Z.ToJsonSchema(
			userSchema,
			new ToJsonSchemaOptions { Title = "User", Id = "https://example.com/schemas/user.json" }
		);

		// Serialize to JSON string
		var json = JsonConvert.SerializeObject(jsonSchema, JsonSchemaSerializerOptions.Default);
		Console.WriteLine("Generated JSON Schema:");
		Console.WriteLine(json);
		Console.WriteLine();
	}

	/// <summary>
	/// Example: Parse JSON Schema into ZodSharp schema
	/// </summary>
	static void DemoFromJsonSchema()
	{
		Console.WriteLine("--- FromJsonSchema: JSON Schema -> ZodSharp ---");

		// This could come from a TypeScript Zod export or any JSON Schema source
		var jsonSchemaString = /*lang=json,strict*/
			@"{
            ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
            ""type"": ""object"",
            ""properties"": {
                ""title"": { ""type"": ""string"", ""minLength"": 1 },
                ""price"": { ""type"": ""number"", ""minimum"": 0 },
                ""inStock"": { ""type"": ""boolean"" }
            },
            ""required"": [""title"", ""price""]
        }";

		// Parse JSON Schema into ZodSharp schema
		var productSchema = Z.FromJsonSchema(jsonSchemaString);

		// Test validation with valid data
		Dictionary<string, object?> validProduct = new()
		{
			{ "title", "Laptop" },
			{ "price", 999.99 },
			{ "inStock", true },
		};

		var validResult = productSchema.Validate(validProduct);
		Console.WriteLine($"Valid product: {(validResult.IsSuccess ? "PASSED" : "FAILED")}");

		// Test validation with invalid data (missing required field)
		Dictionary<string, object?> invalidProduct = new()
		{
			{ "title", "Phone" },
			// Missing "price" field
		};

		var invalidResult = productSchema.Validate(invalidProduct);
		Console.WriteLine($"Invalid product (missing price): {(invalidResult.IsSuccess ? "PASSED" : "FAILED")}");
		if (!invalidResult.IsSuccess)
		{
			foreach (var error in invalidResult.Errors)
			{
				Console.WriteLine($"  Error: {error.Message}");
			}
		}
		Console.WriteLine();
	}

	/// <summary>
	/// Example: Roundtrip (ZodSharp -> JSON Schema -> ZodSharp)
	/// </summary>
	static void DemoRoundtrip()
	{
		Console.WriteLine("--- Roundtrip: ZodSharp -> JSON Schema -> ZodSharp ---");

		// Original ZodSharp schema
		var originalSchema = Z.Object().Field("id", Z.String().UUID()).Field("count", Z.Number().Min(0).Int()).Build();

		// Convert to JSON Schema
		var jsonSchema = Z.ToJsonSchema(originalSchema);
		var jsonString = JsonConvert.SerializeObject(jsonSchema, JsonSchemaSerializerOptions.Default);

		// Parse back to ZodSharp schema
		var parsedSchema = Z.FromJsonSchema(jsonString);

		// Test both schemas with same data
		Dictionary<string, object?> testData = new()
		{
			{ "id", "550e8400-e29b-41d4-a716-446655440000" },
			{ "count", 42.0 },
		};

		var originalResult = originalSchema.Validate(testData);
		var parsedResult = parsedSchema.Validate(testData);

		Console.WriteLine($"Original schema result: {(originalResult.IsSuccess ? "PASSED" : "FAILED")}");
		Console.WriteLine($"Parsed schema result: {(parsedResult.IsSuccess ? "PASSED" : "FAILED")}");
		Console.WriteLine($"Results match: {originalResult.IsSuccess == parsedResult.IsSuccess}");
		Console.WriteLine();
	}

	/// <summary>
	/// Example: Cross-platform scenario simulation
	/// </summary>
	static void DemoCrossplatformScenario()
	{
		Console.WriteLine("--- Cross-Platform Scenario ---");
		Console.WriteLine("Scenario: TypeScript frontend defines schema, C# backend validates");
		Console.WriteLine();

		// Simulating a JSON Schema that would come from TypeScript Zod
		// In real world, this would be loaded from a file or API
		var typeScriptGeneratedSchema =
			/*lang=json*/
			@"{
            ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
            ""$id"": ""CreateUserRequest"",
            ""type"": ""object"",
            ""properties"": {
                ""username"": { 
                    ""type"": ""string"", 
                    ""minLength"": 3, 
                    ""maxLength"": 20 
                },
                ""email"": { 
                    ""type"": ""string"", 
                    ""format"": ""email"" 
                },
                ""password"": { 
                    ""type"": ""string"", 
                    ""minLength"": 8 
                }
            },
            ""required"": [""username"", ""email"", ""password""],
            ""additionalProperties"": false
        }";

		// C# Backend: Parse the schema
		var createUserSchema = Z.FromJsonSchema(typeScriptGeneratedSchema);

		// Simulate incoming requests
		var requests = new[]
		{
			new Dictionary<string, object?>
			{
				{ "username", "john_doe" },
				{ "email", "john@example.com" },
				{ "password", "securePassword123" },
			},
			new Dictionary<string, object?>
			{
				{ "username", "ab" }, // Too short!
				{ "email", "invalid-email" }, // Invalid format
				{ "password", "123" }, // Too short!
			},
		};

		for (var i = 0; i < requests.Length; i++)
		{
			Console.WriteLine($"Request {i + 1}:");
			var result = createUserSchema.Validate(requests[i]);

			if (result.IsSuccess)
			{
				Console.WriteLine("  ✓ Validation passed");
			}
			else
			{
				Console.WriteLine("  ✗ Validation failed:");
				foreach (var error in result.Errors)
				{
					var path = error.Path.Length > 0 ? string.Join(".", error.Path) : "(root)";
					Console.WriteLine($"    - {path}: {error.Message}");
				}
			}
		}
		Console.WriteLine();
	}
}

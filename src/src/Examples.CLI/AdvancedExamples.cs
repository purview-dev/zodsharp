using ZodSharp.Core;
using ZodSharp.Expressions;
using ZodSharp.Schemas;

namespace ZodSharp.Examples.CLI;

static class AdvancedExamples
{
	public static void RunAll()
	{
		Console.WriteLine("=== Advanced ZodSharp Examples ===\n");

		StringAdvancedExamples();
		NumberAdvancedExamples();
		TransformExamples();
		RefinementExamples();
		DiscriminatedUnionExamples();
		LazyEvaluationExamples();
		SpanValidationExamples();
		CompiledValidatorExamples();
		JsonIntegrationExamples();
		DefaultValueExamples();
		SchemaCachingExamples();
		SourceGeneratorExamples();
	}

	static void StringAdvancedExamples()
	{
		Console.WriteLine("--- String Advanced Methods ---");

		var urlSchema = Z.String().Url();
		var urlResult = urlSchema.Validate("https://example.com");
		Console.WriteLine($"URL validation: {urlResult.IsSuccess}");

		var uuidSchema = Z.String().UUID();
		var uuidResult = uuidSchema.Validate("550e8400-e29b-41d4-a716-446655440000");
		Console.WriteLine($"UUID validation: {uuidResult.IsSuccess}");

		var prefixSchema = Z.String().StartsWith("https://");
		var prefixResult = prefixSchema.Validate("https://example.com");
		Console.WriteLine($"StartsWith validation: {prefixResult.IsSuccess}");

		var suffixSchema = Z.String().EndsWith(".com");
		var suffixResult = suffixSchema.Validate("example.com");
		Console.WriteLine($"EndsWith validation: {suffixResult.IsSuccess}");

		var trimmedSchema = Z.String().Trim();
		var trimmedResult = trimmedSchema.Validate("  hello  ");
		Console.WriteLine($"Trim result: '{trimmedResult.Value}'");

		var upperSchema = Z.String().ToUpper();
		var upperResult = upperSchema.Validate("hello");
		Console.WriteLine($"ToUpper result: '{upperResult.Value}'");

		var lowerSchema = Z.String().ToLower();
		var lowerResult = lowerSchema.Validate("HELLO");
		Console.WriteLine($"ToLower result: '{lowerResult.Value}'");

		var exactLengthSchema = Z.String().Length(10);
		var exactResult = exactLengthSchema.Validate("1234567890");
		Console.WriteLine($"Exact length validation: {exactResult.IsSuccess}");

		Console.WriteLine();
	}

	static void NumberAdvancedExamples()
	{
		Console.WriteLine("--- Number Advanced Methods ---");

		var positiveSchema = Z.Number().Positive();
		var positiveResult = positiveSchema.Validate(10.0);
		Console.WriteLine($"Positive validation: {positiveResult.IsSuccess}");

		var negativeSchema = Z.Number().Negative();
		var negativeResult = negativeSchema.Validate(-5.0);
		Console.WriteLine($"Negative validation: {negativeResult.IsSuccess}");

		var multipleOfSchema = Z.Number().MultipleOf(10);
		var multipleResult = multipleOfSchema.Validate(30.0);
		Console.WriteLine($"MultipleOf(10) validation: {multipleResult.IsSuccess}");

		var finiteSchema = Z.Number().Finite();
		var finiteResult = finiteSchema.Validate(42.0);
		Console.WriteLine($"Finite validation: {finiteResult.IsSuccess}");

		var safeSchema = Z.Number().Safe();
		var safeResult = safeSchema.Validate(2147483647.0);
		Console.WriteLine($"Safe integer validation: {safeResult.IsSuccess}");

		Console.WriteLine();
	}

	static void TransformExamples()
	{
		Console.WriteLine("--- Transform Examples ---");

		var upperSchema = Z.String().Transform(static s => s.ToUpperInvariant());
		var upperResult = upperSchema.Validate("hello");
		Console.WriteLine($"Transform to upper: '{upperResult.Value}'");

		var doubleSchema = Z.Number().Transform(static n => n * 2);
		var doubleResult = doubleSchema.Validate(5.0);
		Console.WriteLine($"Transform number (double): {doubleResult.Value}");

		var chainSchema = Z.String().Transform(static s => s.Trim()).Transform(static s => s.ToLowerInvariant());
		var chainResult = chainSchema.Validate("  HELLO  ");
		Console.WriteLine($"Chained transforms: '{chainResult.Value}'");

		Console.WriteLine();
	}

	static void RefinementExamples()
	{
		Console.WriteLine("--- Refinement Examples ---");

		var evenSchema = Z.Number().Refine(static n => n % 2 == 0, "Must be even");
		var evenResult = evenSchema.Validate(4.0);
		Console.WriteLine($"Even number validation: {evenResult.IsSuccess}");

		var passwordSchema = Z.String()
			.Min(8)
			.Refine(static s => s.Any(char.IsUpper), "Must contain uppercase")
			.Refine(static s => s.Any(char.IsLower), "Must contain lowercase")
			.Refine(static s => s.Any(char.IsDigit), "Must contain digit");

		var passwordResult = passwordSchema.Validate("Password123");
		Console.WriteLine($"Password validation: {passwordResult.IsSuccess}");

		Console.WriteLine();
	}

	static void DiscriminatedUnionExamples()
	{
		Console.WriteLine("--- Discriminated Union Examples ---");

		var userSchema = Z.Object().Field("type", Z.String()).Field("name", Z.String()).Build();

		var adminSchema = Z.Object()
			.Field("type", Z.String())
			.Field("name", Z.String())
			.Field("permissions", Z.Array(Z.String()))
			.Build();

		var union = Z.DiscriminatedUnion("type").Option("user", userSchema).Option("admin", adminSchema).Build();

		Dictionary<string, object?> userData = new() { { "type", "user" }, { "name", "John" } };

		var unionResult = union.Validate(userData);
		Console.WriteLine($"Discriminated union validation: {unionResult.IsSuccess}");

		Console.WriteLine();
	}

	static void LazyEvaluationExamples()
	{
		Console.WriteLine("--- Lazy Evaluation Examples ---");

		ZodLazy<Dictionary<string, object?>>? categorySchema = null;
		categorySchema = Z.Lazy(() =>
			Z.Object().Field("name", Z.String()).Field("subcategories", Z.Array(categorySchema!)).Build()
		);

		Dictionary<string, object?> categoryData = new()
		{
			{ "name", "Electronics" },
			{
				"subcategories",
				new[]
				{
					new Dictionary<string, object?>
					{
						{ "name", "Phones" },
						{ "subcategories", Array.Empty<Dictionary<string, object?>>() },
					},
				}
			},
		};

		var lazyResult = categorySchema.Validate(categoryData);
		Console.WriteLine($"Lazy evaluation validation: {lazyResult.IsSuccess}");

		Console.WriteLine();
	}

	static void SpanValidationExamples()
	{
		Console.WriteLine("--- Span<T> Validation Examples ---");

		var schema = Z.String().Min(3).Max(50).Email();

		var span = "user@example.com".AsSpan();
		var spanResult = schema.ValidateSpan(span);
		Console.WriteLine($"Span validation: {spanResult.IsSuccess}");

		var largeString = "verylongemail@example.com".AsSpan();
		var largeResult = schema.ValidateSpan(largeString);
		Console.WriteLine($"Large span validation: {largeResult.IsSuccess}");

		Console.WriteLine();
	}

	static void CompiledValidatorExamples()
	{
		Console.WriteLine("--- Compiled Validator Examples ---");

		var schema = Z.String().Min(3).Max(50).Email();

		var compiled = CompiledValidator.Compile(schema);
		var result = compiled("user@example.com");
		Console.WriteLine($"Compiled validator result: {result.IsSuccess}");

		var parser = CompiledValidator.CompileParser(schema);
		try
		{
			var value = parser("user@example.com");
			Console.WriteLine($"Compiled parser result: {value}");
		}
		catch (ZodException ex)
		{
			Console.WriteLine($"Compiled parser error: {ex.Message}");
		}

		Console.WriteLine();
	}

	static void JsonIntegrationExamples()
	{
		Console.WriteLine("--- JSON Integration Examples ---");

		var schema = Z.Object().Field("name", Z.String().Min(1)).Field("age", Z.Number().Min(0)).Build();

		var json = /*lang=json,strict*/
			"""{"name": "John", "age": 30}""";

		var result = schema.DeserializeAndValidate(json);
		Console.WriteLine($"JSON validation: {result.IsSuccess}");

		var converter = schema.CreateValidatingConverter();
		Newtonsoft.Json.JsonSerializerSettings settings = new() { Converters = { converter } };

		try
		{
			var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object?>>(
				json,
				settings
			);
			Console.WriteLine($"JSON converter result: {deserialized != null}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"JSON converter error: {ex.Message}");
		}

		Console.WriteLine();
	}

	static void DefaultValueExamples()
	{
		Console.WriteLine("--- Default Value Examples ---");

		var schema = Z.String().Default("unknown");
		var result = schema.Validate(null);
		Console.WriteLine($"Default value result: '{result.Value}'");

		var validatedDefault = Z.String().Min(3).Default("default");
		var validatedResult = validatedDefault.Validate(null);
		Console.WriteLine($"Validated default result: '{validatedResult.Value}'");

		Console.WriteLine();
	}

	static void SchemaCachingExamples()
	{
		Console.WriteLine("--- Schema Caching Examples ---");

		var schema = SchemaCache.GetOrCreate(
			"user",
			static () => Z.Object().Field("name", Z.String().Min(1)).Field("age", Z.Number().Min(0)).Build()
		);

		if (SchemaCache.TryGet("user", out ZodObject cachedSchema))
			Console.WriteLine($"Schema cached: {schema == cachedSchema}");
		else
			Console.WriteLine("Schema not found in cache.");

		Console.WriteLine();
	}

	static void SourceGeneratorExamples()
	{
		Console.WriteLine("--- Source Generator Examples ---");

		User user = new()
		{
			Name = "John Doe",
			Age = 30,
			Email = "john@example.com",
		};

		var result = UserSchema.Validate(user);
		Console.WriteLine($"Source generator validation: {result.IsSuccess}");

		if (result.IsSuccess)
		{
			Console.WriteLine($"Validated user: {result.Value.Name}, Age: {result.Value.Age}");
		}
		else
		{
			foreach (var error in result.Errors)
			{
				Console.WriteLine($"Error: {error.Message}");
			}
		}

		try
		{
			var validated = UserSchema.Parse(user);
			Console.WriteLine($"Parsed user: {validated.Name}");
		}
		catch (ZodException ex)
		{
			Console.WriteLine($"Parse error: {ex.Message}");
		}

		Console.WriteLine();
	}
}

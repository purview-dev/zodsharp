using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.AspNetCore;
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.JsonSchema.SystemTextJson;

namespace ZodSharp.TrimSafety;

/// <summary>The System.Text.Json, JSON Schema, Microsoft.Extensions.Validation, ProblemDetails and options integrations.</summary>
static class IntegrationChecks
{
	public static void AssemblyScanning(Harness h)
	{
		ServiceCollection services = new();
		services.AddZodSharp(static options => options.ScanAssemblies.Add(typeof(Order).Assembly));
		using var provider = services.BuildServiceProvider();
		h.Expect(
			provider.GetRequiredService<IZodSchemaFactory>().Resolve<Order>() is not null,
			"ScanAssemblies registers generated validators from [ZodSchemaGenerated] metadata"
		);

		ServiceCollection loaded = new();
		loaded.AddZodSharp(static options => options.ScanLoadedAssemblies = true);
		using var loadedProvider = loaded.BuildServiceProvider();
		h.Expect(
			loadedProvider.GetRequiredService<IZodSchemaFactory>().Resolve<Order>() is not null,
			"ScanLoadedAssemblies finds the generated validators"
		);
	}

	public static void SystemTextJson(Harness h)
	{
		CustomerSchemaValidator schema = new();
		var typeInfo = HarnessJsonContext.Default.Customer;

		var valid = schema.DeserializeAndValidate("""{"name":"Ada","age":36,"tags":["a"]}""", typeInfo);
		h.Expect(valid.IsSuccess && valid.Value.Name == "Ada", "a valid document deserializes and validates");

		var invalid = schema.DeserializeAndValidate("""{"name":"A","age":200,"tags":["a","b","c"]}""", typeInfo);
		h.ExpectPaths(invalid.Errors, ["Name", "Age", "Tags"], "an invalid document reports each member");

		var malformed = schema.DeserializeAndValidate("""{"name":""", typeInfo);
		h.Expect(
			!malformed.IsSuccess && malformed.Errors[0].Code == SystemTextJsonExtensions.InvalidJsonCode,
			"malformed JSON is reported as invalid_json"
		);

		var serialized = schema.ValidateAndSerialize(new Customer { Name = "Grace", Age = 85 }, typeInfo);
		h.Expect(
			serialized.IsSuccess && serialized.Value.Contains("\"Grace\"", StringComparison.Ordinal),
			"ValidateAndSerialize writes through the source-generated contract"
		);
	}

	public static void JsonSchemaRoundTrip(Harness h)
	{
		var typeInfo = JsonSchemaSerializerOptions.Default.GetTypeInfo<JsonSchemaDefinition>();

		var generated = OrderSchema.BuildJsonSchema();
		var json = JsonSerializer.Serialize(generated, typeInfo);
		h.Expect(json.Contains("\"minItems\": 1", StringComparison.Ordinal), "the generated schema exports minItems");
		h.Expect(
			json.Contains("\"maxProperties\": 2", StringComparison.Ordinal),
			"the generated schema exports maxProperties"
		);

		var runtime = Z.ToJsonSchema(
			Z.Object()
				.Field("name", Z.String().Min(2).Max(10))
				.Field("kind", Z.Enum("a", "b"))
				.Field("tags", Z.Array(Z.String()).Min(1))
				.Build()
		);
		var runtimeJson = JsonSerializer.Serialize(runtime, typeInfo);
		h.Expect(
			runtimeJson.Contains("\"minLength\": 2", StringComparison.Ordinal)
				&& runtimeJson.Contains("\"enum\"", StringComparison.Ordinal),
			"a runtime schema exports its constraints and enum values"
		);

		var imported = Z.FromJsonSchema(runtimeJson);
		h.Expect(
			imported
				.Validate(
					new Dictionary<string, object?>
					{
						["name"] = "Ada",
						["kind"] = "a",
						["tags"] = new object?[] { "x" },
					}
				)
				.IsSuccess,
			"an exported schema imports and validates"
		);
		h.Expect(
			!imported
				.Validate(
					new Dictionary<string, object?>
					{
						["name"] = "A",
						["kind"] = "z",
						["tags"] = new object?[] { "x" },
					}
				)
				.IsSuccess,
			"the imported schema enforces the exported constraints"
		);
	}

	public static async Task ValidationBridge(Harness h)
	{
		ServiceCollection services = new();
		services.AddZodSharpValidation();
		services.AddZodSharpZodSharpTrimSafetyVerificationSchemas();
		await using var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
		var report = await options.ValidateAsync(GeneratedChecks.InvalidOrder(), provider);
		h.Expect(
			report.Errors.Any(static e => e.FullPath.ToDottedString() == "Lines[0].Sku"),
			"ValidationOptions validates through the ZodSharp resolver with full-fidelity errors"
		);

		var clean = await options.ValidateAsync(GeneratedChecks.ValidOrder(), provider);
		h.Expect(clean.Errors.Count == 0, "a valid order produces no errors through the bridge");
	}

	public static async Task ProblemDetails(Harness h)
	{
		ZodProblemDetailsWriter writer = new(new ZodProblemDetailsOptions());
		var errors = OrderSchema.Validate(GeneratedChecks.InvalidOrder()).Errors;

		var problem = writer.Create(errors.AsSpan());
		h.Expect(problem.Status == StatusCodes.Status400BadRequest, "the problem is a 400");
		h.Expect(problem.Errors.ContainsKey("Lines[0].Sku"), "errors are keyed by path");

		DefaultHttpContext context = new();
		using MemoryStream body = new();
		context.Response.Body = body;
		await writer.WriteAsync(context, errors.AsSpan());

		var written = Encoding.UTF8.GetString(body.ToArray());
		using var document = JsonDocument.Parse(written);
		h.Expect(
			document.RootElement.TryGetProperty("issues", out var issues) && issues.GetArrayLength() > 0,
			"the response body is written through the source-generated ProblemDetails contract"
		);
		h.Expect(
			context.Response.ContentType?.StartsWith("application/problem+json", StringComparison.Ordinal) == true,
			"the response is application/problem+json"
		);
	}

	public static void OptionsValidation(Harness h)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Port"] = "70000" })
			.Build();

		ServiceCollection services = new();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddZodSharpZodSharpTrimSafetyVerificationSchemas();
		services.AddOptions<SmtpOptions>().BindConfiguration("Smtp").ValidateWithZod("Smtp", validateOnStart: false);
		using var provider = services.BuildServiceProvider();

		try
		{
			_ = provider.GetRequiredService<IOptions<SmtpOptions>>().Value;
			h.Fail("invalid options were accepted");
		}
		catch (OptionsValidationException ex)
		{
			h.Expect(
				ex.Failures.Any(static f => f.StartsWith("Smtp:Host:", StringComparison.Ordinal))
					&& ex.Failures.Any(static f => f.StartsWith("Smtp:Port:", StringComparison.Ordinal)),
				$"failures are named by configuration key (got: {string.Join(" | ", ex.Failures)})"
			);
		}
	}
}

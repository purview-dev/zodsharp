// Packed-consumer gate, consumer (a): a Native AOT console built from the packages alone. Every check records a
// failure rather than throwing, so one run reports every mismatch; any failure makes the process exit non-zero.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using PackedAot;
using ZodSharp;
using ZodSharp.AspNetCore;
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.JsonSchema.SystemTextJson;

var failures = new List<string>();
var passed = 0;

void Check(bool condition, string description)
{
	if (condition)
	{
		passed++;
		Console.WriteLine($"  pass  {description}");
	}
	else
	{
		failures.Add(description);
		Console.WriteLine($"  FAIL  {description}");
	}
}

void CheckPaths(ImmutableArray<ZodSharp.Core.ValidationError> errors, string[] expected, string description)
{
	var actual = errors.IsDefault ? [] : errors.Select(static e => e.FullPath.ToDottedString()).Distinct().ToArray();
	var missing = expected.Except(actual).ToArray();
	var unexpected = actual.Except(expected).ToArray();
	Check(
		missing.Length == 0 && unexpected.Length == 0,
		missing.Length == 0 && unexpected.Length == 0
			? description
			: $"{description} (missing: [{string.Join(", ", missing)}], unexpected: [{string.Join(", ", unexpected)}])"
	);
}

async Task Section(string name, Func<Task> body)
{
	Console.WriteLine(name);
	try
	{
		await body();
	}
	catch (Exception ex)
	{
		Check(false, $"{name} threw {ex.GetType().Name}: {ex.Message}");
	}
}

static Order ValidOrder() =>
	new()
	{
		CustomerEmail = "buyer@example.com",
		ShippingAddress = new() { Street = "1 Main St", City = "Springfield" },
		Lines = [new() { Sku = "ABC-1234", Quantity = 2 }],
		AdditionalAddresses = new()
		{
			["billing"] = new() { Street = "2 Side St", City = "Shelbyville" },
		},
	};

static Order InvalidOrder() =>
	new()
	{
		CustomerEmail = "not-an-email",
		ShippingAddress = new() { Street = "", City = "Springfield" },
		Lines = [new() { Sku = "abc", Quantity = 0 }],
		AdditionalAddresses = new()
		{
			["billing"] = new() { Street = "2 Side St", City = null },
		},
	};

await Section("native image", () =>
{
	// A native image compiles nothing at run time; `dotnet run` of the same project would JIT.
	Check(System.Runtime.JitInfo.GetCompiledMethodCount() == 0, "the process is a Native AOT image (no JIT)");
	return Task.CompletedTask;
});

await Section("generated validation", () =>
{
	Check(OrderSchema.Validate(ValidOrder()).IsSuccess, "a valid order passes");
	CheckPaths(
		OrderSchema.Validate(InvalidOrder()).Errors,
		[
			"CustomerEmail",
			"ShippingAddress.Street",
			"Lines[0].Sku",
			"Lines[0].Quantity",
			"AdditionalAddresses[billing].City",
		],
		"an invalid order reports nested, collection and dictionary errors"
	);
	return Task.CompletedTask;
});

await Section("count attributes", () =>
{
	var empty = ValidOrder();
	empty.Lines = [];
	var tooFew = OrderSchema.Validate(empty).Errors.Single();
	Check(
		tooFew.Code == "too_small" && tooFew.FullPath.ToDottedString() == "Lines" && tooFew.Minimum == 1,
		$"[MinCount] reports too_small at the member (got {tooFew.Code} at {tooFew.FullPath.ToDottedString()})"
	);

	var full = ValidOrder();
	full.Lines = [.. Enumerable.Range(0, 4).Select(static _ => new OrderLine { Sku = "ABC-1234" })];
	var tooMany = OrderSchema.Validate(full).Errors.Single();
	Check(tooMany.Code == "too_big" && tooMany.Maximum == 3, "[MaxCount] reports too_big with the maximum");

	var crowded = ValidOrder();
	for (var i = 0; i < 2; i++)
		crowded.AdditionalAddresses[$"extra{i}"] = new() { Street = "s", City = "c" };
	var range = OrderSchema.Validate(crowded).Errors.Single();
	Check(
		range.Code == "too_big" && range.Maximum == 2 && range.FullPath.ToDottedString() == "AdditionalAddresses",
		$"[CountRange] counts dictionary entries (got {range.Code} at {range.FullPath.ToDottedString()})"
	);
	return Task.CompletedTask;
});

await Section("member validation and resolution", () =>
{
	var order = InvalidOrder();
	CheckPaths(
		OrderSchema.ValidateMember(order, nameof(Order.CustomerEmail)).Errors,
		["CustomerEmail"],
		"ValidateMember reports only the member"
	);

	IZodUntypedValidator validator = new OrderSchemaValidator();
	var city = validator
		.Validate(order)
		.First(static e => e.FullPath.ToDottedString() == "AdditionalAddresses[billing].City");
	var resolved = validator.TryResolveMember(order, city.FullPath, out var container, out var memberName);
	Check(
		resolved && ReferenceEquals(container, order.AdditionalAddresses["billing"]) && memberName == "City",
		"TryResolveMember resolves the owning dictionary entry without reflection"
	);
	return Task.CompletedTask;
});

await Section("JSON Schema export", () =>
{
	var typeInfo = JsonSchemaSerializerOptions.Default.GetTypeInfo<JsonSchemaDefinition>();
	var json = JsonSerializer.Serialize(OrderSchema.BuildJsonSchema(), typeInfo);
	Check(json.Contains("\"minItems\": 1", StringComparison.Ordinal), "the generated schema exports minItems");
	Check(json.Contains("\"maxItems\": 3", StringComparison.Ordinal), "the generated schema exports maxItems");
	Check(
		json.Contains("\"maxProperties\": 2", StringComparison.Ordinal),
		"the generated schema exports maxProperties"
	);

	using var document = JsonDocument.Parse(json);
	var members = document.RootElement.TryGetProperty("properties", out var properties)
		? properties.EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal).ToArray()
		: [];
	Check(
		// Exported in JSON (camelCase) names.
		members.SequenceEqual(["additionalAddresses", "customerEmail", "lines", "shippingAddress"]),
		$"the generated schema describes every member (got [{string.Join(", ", members)}])"
	);

	var runtime = JsonSerializer.Serialize(
		Z.ToJsonSchema(Z.Object().Field("name", Z.String().Min(2)).Build()),
		typeInfo
	);
	Check(runtime.Contains("\"minLength\": 2", StringComparison.Ordinal), "a runtime schema exports its rules");
	return Task.CompletedTask;
});

await Section("System.Text.Json with a source-generated context", () =>
{
	CustomerSchemaValidator schema = new();
	var typeInfo = ConsumerJsonContext.Default.Customer;

	var valid = schema.DeserializeAndValidate("""{"name":"Ada","age":36,"tags":["a"]}""", typeInfo);
	Check(valid.IsSuccess && valid.Value.Name == "Ada", "a valid document deserializes and validates");

	var invalid = schema.DeserializeAndValidate("""{"name":"A","age":200,"tags":["a","b","c"]}""", typeInfo);
	CheckPaths(invalid.Errors, ["Name", "Age", "Tags"], "an invalid document reports each member");
	return Task.CompletedTask;
});

await Section("Microsoft.Extensions.Validation bridge", async () =>
{
	ServiceCollection services = new();
	services.AddZodSharpValidation();
	services.AddZodSharpPackedAotSchemas();
	await using var provider = services.BuildServiceProvider();

	var options = provider.GetRequiredService<IOptions<ValidationOptions>>().Value;
	var report = await options.ValidateAsync(InvalidOrder(), provider);
	Check(
		report.Errors.Any(static e => e.FullPath.ToDottedString() == "Lines[0].Sku"),
		"ValidationOptions validates through the ZodSharp resolver with full-fidelity errors"
	);
	Check(
		(await options.ValidateAsync(ValidOrder(), provider)).Errors.Count == 0,
		"a valid order produces no errors through the bridge"
	);
});

await Section("ZodProblemDetailsWriter without a host", async () =>
{
	ZodProblemDetailsWriter writer = new(new ZodProblemDetailsOptions());
	var errors = OrderSchema.Validate(InvalidOrder()).Errors;

	var problem = writer.Create(errors.AsSpan());
	Check(problem.Status == StatusCodes.Status400BadRequest, "the problem is a 400");
	Check(problem.Errors.ContainsKey("Lines[0].Sku"), "errors are keyed by path");

	DefaultHttpContext context = new();
	using MemoryStream body = new();
	context.Response.Body = body;
	await writer.WriteAsync(context, errors.AsSpan());

	using var document = JsonDocument.Parse(Encoding.UTF8.GetString(body.ToArray()));
	Check(
		document.RootElement.TryGetProperty("issues", out var issues) && issues.GetArrayLength() > 0,
		"the body carries the issues array"
	);
	Check(
		context.Response.ContentType?.StartsWith("application/problem+json", StringComparison.Ordinal) == true,
		"the response is application/problem+json"
	);
});

await Section("OpenAPI options", () =>
{
	OpenApiOptions options = new();
	Check(ReferenceEquals(options.AddZodSharp(), options), "AddZodSharp registers the ZodSharp transformers");
	return Task.CompletedTask;
});

Console.WriteLine();
if (failures.Count == 0)
{
	Console.WriteLine($"Packed Native AOT consumer passed: {passed} checks.");
	return 0;
}

Console.WriteLine($"Packed Native AOT consumer FAILED: {failures.Count} failure(s), {passed} passed.");
foreach (var failure in failures)
	Console.WriteLine($"  {failure}");

return 1;

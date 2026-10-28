// Packed-consumer gate, consumer (c): a Native AOT minimal API built from the packages alone. The script that publishes
// it starts the native binary, posts an invalid order and reads /openapi/v1.json.
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using PackedApi;
using ZodSharp;
using ZodSharp.Rules;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(static options =>
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default)
);

// The generated, trim-safe registration; ZodSharp as the Microsoft.Extensions.Validation resolver, so the built-in
// pipeline validates the endpoint parameter; the ZodSharp ProblemDetails contract; and the OpenAPI transformers.
builder.Services.AddZodSharp();
builder.Services.AddZodSharpPackedApiSchemas();
builder.Services.AddZodSharpValidation();
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddOpenApi(static options => options.AddZodSharp());

var app = builder.Build();
app.UseExceptionHandler();
app.MapOpenApi();
app.MapPost("/orders", static (CreateOrder order) => TypedResults.Ok(new OrderAccepted(order.Lines.Count)));

await app.RunAsync();

namespace PackedApi
{
	[ZodSchema]
	public sealed class CreateOrder
	{
		[Required]
		[EmailAddress]
		public string? CustomerEmail { get; set; }

		[MinCount(1)]
		[MaxCount(10)]
		public List<CreateOrderLine> Lines { get; set; } = [];
	}

	[ZodSchema]
	public sealed class CreateOrderLine
	{
		[Required]
		[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
		public string? Sku { get; set; }

		[Range(1, 100)]
		public int Quantity { get; set; }
	}

	public sealed record OrderAccepted(int Lines);

	[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(CreateOrder))]
	[JsonSerializable(typeof(OrderAccepted))]
	sealed partial class ApiJsonContext : JsonSerializerContext;
}

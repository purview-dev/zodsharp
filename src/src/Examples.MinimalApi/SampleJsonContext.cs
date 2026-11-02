using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// Source-generated JSON metadata for the request and response types, so the endpoints serialize under Native AOT.
/// The ProblemDetails types are supplied by ASP.NET Core and by <c>AddZodSharpProblemDetails()</c>.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(RegisterCustomerRequest))]
[JsonSerializable(typeof(OrderAccepted))]
[JsonSerializable(typeof(CustomerRegistered))]
sealed partial class SampleJsonContext : JsonSerializerContext;

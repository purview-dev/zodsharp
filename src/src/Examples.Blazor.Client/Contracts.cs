using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZodSharp.Examples.Blazor.Client;

/// <summary>
/// Whether an email address is already registered: the response of <c>GET /api/customers/registered</c>.
/// </summary>
/// <param name="Registered"><see langword="true"/> when the address is taken.</param>
sealed record EmailStatus(bool Registered);

/// <summary>
/// The response of a successful <c>POST /api/customers</c>.
/// </summary>
/// <param name="Email">The registered address.</param>
sealed record CustomerRegistered(string Email);

/// <summary>
/// Source-generated JSON metadata for the sample's HTTP contract, so the trimmed WebAssembly client serializes without
/// reflection. The server uses it too.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(RegisterCustomerRequest))]
[JsonSerializable(typeof(EmailStatus))]
[JsonSerializable(typeof(CustomerRegistered))]
sealed partial class ClientJsonContext : JsonSerializerContext;

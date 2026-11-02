using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ZodSharp;
using ZodSharp.Rules;

namespace PackedAot;

/// <summary>
/// An order with a nested object, a counted collection and a counted dictionary: the members the packed generator has
/// to emit nested, collection, dictionary and count-attribute rules for.
/// </summary>
[ZodSchema]
public sealed class Order
{
	[Required]
	[EmailAddress]
	public string? CustomerEmail { get; set; }

	[Required]
	public Address? ShippingAddress { get; set; }

	[MinCount(1)]
	[MaxCount(3)]
	public List<OrderLine> Lines { get; set; } = [];

	[CountRange(0, 2)]
	public Dictionary<string, Address> AdditionalAddresses { get; set; } = [];
}

[ZodSchema]
public sealed class OrderLine
{
	[Required]
	[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
	public string? Sku { get; set; }

	[Range(1, 100)]
	public int Quantity { get; set; } = 1;
}

[ZodSchema]
public sealed class Address
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string? Street { get; set; }

	[Required]
	public string? City { get; set; }
}

/// <summary>A JSON request model, deserialized through a source-generated <see cref="JsonSerializerContext"/>.</summary>
[ZodSchema]
public sealed class Customer
{
	[Required]
	[StringLength(50, MinimumLength = 2)]
	public string? Name { get; set; }

	[Range(0, 150)]
	public int Age { get; set; }

	[MaxCount(2)]
	public List<string> Tags { get; set; } = [];
}

[JsonSerializable(typeof(Customer))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class ConsumerJsonContext : JsonSerializerContext { }

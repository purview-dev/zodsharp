using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ZodSharp.Rules;

namespace ZodSharp.TrimSafety;

/// <summary>The order status; validated as a defined enum member.</summary>
enum OrderStatus
{
	Draft,
	Placed,
	Shipped,
}

/// <summary>Display names looked up through <see cref="DisplayAttribute.ResourceType"/>.</summary>
static class Labels
{
	public static string Street => "Street line";
}

/// <summary>
/// An order: a nested object, a counted collection, a dictionary, a union member, an enum, a display-name
/// resource and an <see cref="IValidatableObject"/> rule - the generated-code features a trimmed application uses.
/// </summary>
[ZodSchema]
sealed class Order : IValidatableObject
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

	public Pet Pet { get; set; }

	public OrderStatus Status { get; set; }

	public string? Notes { get; set; }

	public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
		ValidationContext validationContext
	)
	{
		if (string.Equals(Notes, "forbidden", StringComparison.Ordinal))
			yield return new("Notes may not be 'forbidden'.", [nameof(Notes)]);
	}
}

[ZodSchema]
sealed class OrderLine
{
	[Required]
	[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
	public string? Sku { get; set; }

	[Range(1, 100)]
	public int Quantity { get; set; } = 1;
}

[ZodSchema]
sealed class Address
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	[Display(Name = nameof(Labels.Street), ResourceType = typeof(Labels))]
	public string? Street { get; set; }

	[Required]
	public string? City { get; set; }
}

[ZodSchema]
sealed class Cat
{
	// [StringLength], not [MinLength]: MinLengthAttribute's constructor is [RequiresUnreferencedCode] on any member.
	[StringLength(50, MinimumLength = 2)]
	public string? Name { get; set; } = "Tom";
}

[ZodSchema]
sealed class Dog
{
	[Range(1, 100)]
	public int Weight { get; set; } = 1;
}

/// <summary>A C# union member, dispatched to the case's generated schema.</summary>
union Pet(Cat, Dog);

/// <summary>A JSON request model, deserialized through a source-generated <see cref="JsonSerializerContext"/>.</summary>
[ZodSchema]
sealed class Customer
{
	[Required]
	[StringLength(50, MinimumLength = 2)]
	public string? Name { get; set; }

	[Range(0, 150)]
	public int Age { get; set; }

	[MaxCount(2)]
	public List<string> Tags { get; set; } = [];
}

/// <summary>Options validated at startup with <c>ValidateWithZod</c>, bound by the configuration binder generator.</summary>
[ZodSchema]
sealed class SmtpOptions
{
	[Required]
	public string? Host { get; set; }

	[Range(1, 65535)]
	public int Port { get; set; } = 25;
}

/// <summary>A discriminated-union option over a CLR type, supplied through <c>Option&lt;T&gt;</c>.</summary>
[ZodSchema]
sealed class CircleShape
{
	public string Kind { get; set; } = "circle";

	[Range(1, 10)]
	public int Radius { get; set; } = 1;
}

[ZodSchema]
sealed class SquareShape
{
	public string Kind { get; set; } = "square";

	[Range(1, 10)]
	public int Side { get; set; } = 1;
}

/// <summary>A shape whose type the union is not told about, so reading its discriminator needs the reflection fallback.</summary>
sealed class UnregisteredShape
{
	public string Kind { get; set; } = "circle";
}

[JsonSerializable(typeof(Customer))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
sealed partial class HarnessJsonContext : JsonSerializerContext { }

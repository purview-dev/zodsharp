using System.ComponentModel.DataAnnotations;
using ZodSharp.Rules;

namespace ZodSharp.Examples;

/// <summary>
/// An order: a nested <see cref="Address"/>, a collection of <see cref="OrderLine"/> items and a dictionary of
/// extra addresses keyed by purpose (<c>billing</c>, <c>gift</c>). Errors are reported at
/// <c>ShippingAddress.City</c>, <c>Lines[0].Quantity</c> and <c>AdditionalAddresses[billing].PostCode</c>.
/// </summary>
[ZodSchema]
public sealed class CreateOrderRequest
{
	[Required]
	[EmailAddress]
	public string? CustomerEmail { get; set; }

	[Required]
	public Address? ShippingAddress { get; set; }

	// [MinCount]/[MaxCount] rather than [MinLength]/[MaxLength]: the BCL attributes' constructors are
	// [RequiresUnreferencedCode], so they raise IL2026 in a trimmed or Native AOT application (ZODSGEN053). The
	// generator validates both spellings identically, with the same error identity.
	[MinCount(1)]
	[MaxCount(20)]
	public IList<OrderLine> Lines { get; init; } = [];

	public IDictionary<string, Address> AdditionalAddresses { get; init; } = new Dictionary<string, Address>();
}

/// <summary>
/// One line of an order.
/// </summary>
[ZodSchema]
public sealed class OrderLine
{
	[Required]
	[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
	public string? Sku { get; set; }

	[Range(1, 100)]
	public int Quantity { get; set; }
}

/// <summary>
/// A postal address.
/// </summary>
[ZodSchema]
public sealed class Address
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string? Street { get; set; }

	[Required]
	[StringLength(60, MinimumLength = 1)]
	public string? City { get; set; }

	[Required]
	[StringLength(10, MinimumLength = 3)]
	public string? PostCode { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Validation;

namespace ZodSharp.Extensions.Validation.Fixtures;

public sealed class Order
{
	public string? Reference { get; set; }

	public Customer Customer { get; set; } = new();

	public List<OrderLine> Lines { get; set; } = [];

	public OrderLine[] LineArray { get; set; } = [];

	public IEnumerable<OrderLine> LineSequence { get; set; } = [];

	public HashSet<OrderLine> LineSet { get; set; } = [];

	public Dictionary<string, OrderLine> ByCode { get; set; } = [];

	public Dictionary<int, OrderLine> ById { get; set; } = [];

	public int? Quantity { get; set; }
}

public sealed class Customer
{
	public string? Email { get; set; }

	public Address Address { get; set; } = new();
}

public sealed class Address
{
	public string? Street { get; set; }
}

public sealed class OrderLine
{
	public string? Sku { get; set; }

	public int Quantity { get; set; }
}

public readonly record struct Point(int X, int Y);

/// <summary>
/// Known to both ZodSharp (registered in the tests) and the Microsoft.Extensions.Validation generator.
/// </summary>
[ValidatableType]
public sealed class DualModel
{
	[Required(ErrorMessage = "DataAnnotations: Name is required.")]
	public string? Name { get; set; }
}

/// <summary>
/// Known only to the Microsoft.Extensions.Validation generator; its <see cref="Order"/> is validated by ZodSharp.
/// </summary>
[ValidatableType]
public sealed class Envelope
{
	[Required]
	public string? Title { get; set; }

	public Order Order { get; set; } = new();
}

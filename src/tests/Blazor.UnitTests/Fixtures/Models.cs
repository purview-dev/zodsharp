namespace ZodSharp.Blazor.Fixtures;

public sealed class Order
{
	public string? Reference { get; set; }

	public Customer Customer { get; set; } = new();

	public List<OrderLine> Lines { get; set; } = [];

	public Dictionary<string, OrderLine> ByCode { get; set; } = [];
}

public sealed class Customer
{
	public string? Email { get; set; }

	public string? ConfirmEmail { get; set; }
}

public sealed class OrderLine
{
	public string? Sku { get; set; }

	public int Quantity { get; set; }
}

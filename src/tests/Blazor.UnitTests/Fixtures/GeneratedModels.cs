using System.ComponentModel.DataAnnotations;
using ZodSharp.Rules;

namespace ZodSharp.Blazor.Fixtures;

/// <summary>
/// A [ZodSchema] model with nested objects and a collection, and no [ValidatableType]: the shape a Blazor
/// WebAssembly client validates with no Microsoft.Extensions.Validation generator.
/// </summary>
[ZodSchema]
public class GeneratedPerson
{
	[MinLength(3)]
	public string Name { get; set; } = string.Empty;

	public GeneratedAddress Address { get; set; } = new();

	public List<GeneratedAddress> PreviousAddresses { get; set; } = [];
}

[ZodSchema]
public class GeneratedAddress
{
	[Required]
	public string? Street { get; set; }
}

/// <summary>
/// A [ZodSchema] order with a nested object, a collection and a dictionary of nested models.
/// </summary>
[ZodSchema]
public class GeneratedOrder
{
	[Required]
	[StartsWith("ORD-")]
	public string? Reference { get; set; }

	public GeneratedCustomer Customer { get; set; } = new();

	public List<GeneratedLine> Lines { get; set; } = [];

	// Nullable: static-SSR form binding leaves a dictionary that has no posted entries null.
	public Dictionary<string, GeneratedLine>? ByCode { get; set; } = [];
}

[ZodSchema]
public class GeneratedCustomer
{
	[Required]
	[EmailAddress]
	public string? Email { get; set; }
}

[ZodSchema]
public class GeneratedLine
{
	[Required]
	public string? Sku { get; set; }

	[Range(1, 100)]
	public int Quantity { get; set; } = 1;
}

using System.ComponentModel.DataAnnotations;
using ZodSharp.Rules;

namespace ZodSharp.Examples;

/// <summary>
/// A request to return items from an order. Besides the DataAnnotations attributes it carries ZodSharp-specific rules
/// - <c>[StartsWith]</c> and <c>[UUID]</c> - which the Blazor sample's static-SSR form also checks in the browser
/// (<c>AddZodSharpClientValidation</c>). Errors are reported at <c>Reference</c>, <c>ReturnAddress.City</c> and
/// <c>Items[0].Quantity</c>.
/// </summary>
[ZodSchema]
public sealed class ReturnRequest
{
	/// <summary>
	/// The prefix every return reference starts with.
	/// </summary>
	public const string ReferencePrefix = "RMA-";

	[Required]
	[StartsWith(ReferencePrefix)]
	public string? Reference { get; set; }

	[Required]
	[UUID]
	public string? OrderId { get; set; }

	[Required]
	public Address? ReturnAddress { get; set; } = new();

	[MinCount(1)]
	[MaxCount(10)]
	public List<ReturnItem> Items { get; set; } = [];
}

/// <summary>
/// One item being returned.
/// </summary>
[ZodSchema]
public sealed class ReturnItem
{
	[Required]
	[RegularExpression("^[A-Z]{3}-[0-9]{4}$")]
	public string? Sku { get; set; }

	[Range(1, 10)]
	public int Quantity { get; set; }
}

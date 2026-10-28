using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Rules;

namespace PackedWasm;

/// <summary>The form model: generated rules on a nested object and a counted collection.</summary>
[ZodSchema]
public sealed class Registration
{
	[Required]
	[StringLength(50, MinimumLength = 2)]
	public string? Name { get; set; }

	[Required]
	[EmailAddress]
	public string? Email { get; set; }

	[Required]
	public Address Address { get; set; } = new();

	[MaxCount(3)]
	public List<string> Tags { get; set; } = [];
}

[ZodSchema]
public sealed class Address
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string? City { get; set; }
}

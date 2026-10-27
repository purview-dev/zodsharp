using System.ComponentModel.DataAnnotations;

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

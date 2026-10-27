using System.ComponentModel.DataAnnotations;

namespace ZodSharp.Extensions.Validation.Fixtures;

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

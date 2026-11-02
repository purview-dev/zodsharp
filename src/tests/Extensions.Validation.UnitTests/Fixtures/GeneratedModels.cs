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

/// <summary>A struct member: its errors are located inside a value type, which cannot own a field.</summary>
[ZodSchema]
public readonly partial record struct GeneratedPostcode
{
	[Required]
	[RegularExpression("^[0-9]{5}$")]
	public string? Value { get; init; }
}

[ZodSchema]
public class GeneratedDelivery
{
	public GeneratedPostcode Postcode { get; set; }

	public List<GeneratedPostcode> Alternatives { get; set; } = [];
}

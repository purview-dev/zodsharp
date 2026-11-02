using System.ComponentModel.DataAnnotations;

namespace ZodSharp.AspNetCore.Fixtures;

[ZodSchema]
public class ValidatedRequest
{
	[Required]
	[MinLength(3)]
	public string? Name { get; set; }
}

/// <summary>Number ranges, whose bounds must reach ProblemDetails as JSON numbers.</summary>
[ZodSchema]
public class RangedRequest
{
	[Range(1, 10)]
	public int Quantity { get; set; } = 1;

	[Range(0.5, 2.5)]
	public double Ratio { get; set; } = 1;
}

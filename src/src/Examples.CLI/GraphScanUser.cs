using System.ComponentModel.DataAnnotations;

namespace ZodSharp.Examples.CLI;

[ZodSchema]
public sealed class GraphScanUser
{
	[Required]
	[StringLength(40, MinimumLength = 2)]
	public string Name { get; set; } = string.Empty;
}

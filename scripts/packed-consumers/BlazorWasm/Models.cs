using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Rules;

namespace PackedWasm;

/// <summary>The form model: generated rules on a nested object, a counted collection and a scalar value object.</summary>
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

	public PlanId Plan { get; set; } = new() { Value = Guid.NewGuid() };
}

[ZodSchema]
public sealed class Address
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string? City { get; set; }
}

/// <summary>
/// A scalar value object with a type-level rule, shaped like a Purview.ValueObjects scalar: the generator adapts the
/// Guid rule through <c>ScalarRuleAdapter</c> and copies the wrapped rule's identity. It once emitted that copy as
/// <c>(object)rule as IZodRule</c>, a runtime type test on a boxed struct that crashed the Mono AOT compiler (exit code
/// 0xC0000005) under RunAOTCompilation=true, so this type is what the WebAssembly AOT publish proves.
/// </summary>
[Purview.ValueObjects.Serialization.Scalar]
[NonSentinel(Message = "Choose a plan.")]
[ZodSchema]
public readonly partial record struct PlanId : Purview.ValueObjects.IScalarValueObject<PlanId, Guid>
{
	public Guid Value { get; init; }
}

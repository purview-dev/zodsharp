using System.ComponentModel.DataAnnotations;

// A stand-in for the Purview.ValueObjects [Scalar] attribute, which the generator recognizes by name and namespace, so
// these fixtures are shaped exactly like value objects without referencing that package.
#pragma warning disable IDE0130 // The stand-in must live in the namespace the generator recognizes.
namespace Purview.ValueObjects.Serialization
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class ScalarAttribute(string propertyName = "Value") : Attribute
	{
		public string PropertyName { get; } = propertyName;
	}
}

#pragma warning restore IDE0130

namespace ZodSharp.Blazor.Fixtures
{
	/// <summary>A scalar value object: a struct whose own member (<c>Value</c>) carries the rules.</summary>
	[Purview.ValueObjects.Serialization.Scalar]
	[ZodSchema]
	public readonly partial record struct ContactEmail
	{
		[Required]
		[EmailAddress]
		public string? Value { get; init; }
	}

	/// <summary>A struct that is not a scalar: its members keep their own names in messages.</summary>
	[ZodSchema]
	public partial record struct Coordinates
	{
		[Range(-90, 90)]
		public double Latitude { get; set; }
	}

	/// <summary>A form model whose members are structs, so their errors are located inside a value type.</summary>
	[ZodSchema]
	public sealed class SignUp
	{
		[Required]
		public string? Name { get; set; } = "Ada";

		public ContactEmail Email { get; set; } = new() { Value = "ada@example.com" };

		[Display(Name = "Backup email")]
		public ContactEmail? BackupEmail { get; set; }

		public Coordinates Location { get; set; }
	}
}

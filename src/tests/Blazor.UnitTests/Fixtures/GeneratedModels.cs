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

/// <summary>Rejects the value <c>taken</c> only asynchronously.</summary>
public sealed class AsyncTakenAttribute : AsyncValidationAttribute
{
	/// <inheritdoc/>
	protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(
		object? value,
		ValidationContext validationContext
	) => throw new InvalidOperationException("Validated asynchronously.");

	/// <inheritdoc/>
	protected override async Task<System.ComponentModel.DataAnnotations.ValidationResult?> IsValidAsync(
		object? value,
		ValidationContext validationContext,
		CancellationToken cancellationToken
	)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();

		return value is "taken"
			? new System.ComponentModel.DataAnnotations.ValidationResult("Name is taken")
			: System.ComponentModel.DataAnnotations.ValidationResult.Success;
	}
}

/// <summary>A union case whose rule runs only asynchronously.</summary>
#pragma warning disable ZODSGEN048 // The form validates asynchronously; the diagnostic is expected for the fixture.
[ZodSchema]
public class GeneratedCat
{
	[AsyncTaken]
	public string? Name { get; set; }
}
#pragma warning restore ZODSGEN048

/// <summary>A synchronous union case.</summary>
[ZodSchema]
public class GeneratedDog
{
	[Range(1, 100)]
	public int Weight { get; set; } = 1;
}

/// <summary>A union target: validation dispatches to the active case's schema.</summary>
[ZodSchema]
public readonly union GeneratedPet(GeneratedCat, GeneratedDog);

/// <summary>A form model holding a union target member.</summary>
[ZodSchema]
public class GeneratedOwner
{
	public GeneratedPet Pet { get; set; }
}

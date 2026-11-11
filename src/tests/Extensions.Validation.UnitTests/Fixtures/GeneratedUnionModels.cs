#if NET11_0_OR_GREATER
using System.ComponentModel.DataAnnotations;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ZodSharp.Extensions.Validation.Fixtures;

/// <summary>Rejects the value <c>taken</c> only asynchronously, so the sync path must not silently pass it.</summary>
public sealed class AsyncTakenAttribute : AsyncValidationAttribute
{
	/// <inheritdoc/>
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
		throw new InvalidOperationException("Validated asynchronously.");

	/// <inheritdoc/>
	protected override async Task<ValidationResult?> IsValidAsync(
		object? value,
		ValidationContext validationContext,
		CancellationToken cancellationToken
	)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();

		return value is "taken" ? new ValidationResult("Name is taken") : ValidationResult.Success;
	}
}

/// <summary>A union case with an asynchronous rule.</summary>
#pragma warning disable ZODSGEN048 // The fixture is validated asynchronously; the diagnostic is the point of the test.
[ZodSchema]
public class UnionCat
{
	[AsyncTaken]
	public string? Name { get; set; }
}
#pragma warning restore ZODSGEN048

/// <summary>A union case with only synchronous rules.</summary>
[ZodSchema]
public class UnionDog
{
	public int Weight { get; set; } = 1;
}

/// <summary>A union target whose first case validates asynchronously.</summary>
[ZodSchema]
public readonly union GeneratedPet(UnionCat, UnionDog);
#endif

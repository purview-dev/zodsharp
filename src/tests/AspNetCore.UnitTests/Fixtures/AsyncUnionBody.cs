#if NET11_0_OR_GREATER
using System.ComponentModel.DataAnnotations;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ZodSharp.AspNetCore.Fixtures;

/// <summary>Rejects the value <c>taken</c> only asynchronously.</summary>
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
#pragma warning disable ZODSGEN048 // The endpoint validates asynchronously; the diagnostic is expected for the fixture.
[ZodSchema]
public class AsyncUnionCat
{
	[AsyncTaken]
	public string? Name { get; set; }
}
#pragma warning restore ZODSGEN048

/// <summary>
/// A union target used as a minimal-API request body. The cases have different JSON value kinds (object and
/// string) so System.Text.Json can pick one; the string case has no validator, which is why ZODSGEN055 is off.
/// </summary>
#pragma warning disable ZODSGEN055 // The string case is not validated; only the object case is exercised.
[ZodSchema]
public readonly union AsyncPet(AsyncUnionCat, string);
#pragma warning restore ZODSGEN055
#endif

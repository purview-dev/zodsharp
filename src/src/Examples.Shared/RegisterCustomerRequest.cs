using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;

namespace ZodSharp.Examples;

/// <summary>
/// A customer registration. Besides its member rules it has a service-aware asynchronous rule: the email address
/// must not already be registered, which is checked against <see cref="ICustomerEmailRegistry"/> resolved from the
/// caller's services (the request services in a minimal API, the circuit's services in Blazor).
/// </summary>
[ZodSchema]
public sealed class RegisterCustomerRequest
{
	/// <summary>
	/// The error code reported when the email address is already registered.
	/// </summary>
	public const string EmailTakenCode = "email_taken";

	[Required]
	[StringLength(100, MinimumLength = 2)]
	public string? Name { get; set; }

	[Required]
	[EmailAddress]
	public string? Email { get; set; }

	[Range(13, 130)]
	public int Age { get; set; }

	/// <summary>
	/// Runs after the member rules pass. <see cref="ZodAsyncMemberAttribute"/> ties it to <see cref="Email"/>, so
	/// field-level validation (a Blazor field change) re-runs it only when the email changes.
	/// </summary>
	[ZodAsyncMember(nameof(Email))]
	internal static async ValueTask<ValidationResult<RegisterCustomerRequest>> CustomValidationAsync(
		RegisterCustomerRequest value,
		IServiceProvider services,
		CancellationToken cancellationToken
	)
	{
		var registry = services.GetRequiredService<ICustomerEmailRegistry>();
		if (value.Email is null || !await registry.IsRegisteredAsync(value.Email, cancellationToken))
			return ValidationResult<RegisterCustomerRequest>.Success(value);

		return ValidationResult<RegisterCustomerRequest>.Failure(
			ValidationError.Create(
				EmailTakenCode,
				"Email is already registered",
				RegisterCustomerRequestSchema.Paths.Email
			)
		);
	}
}

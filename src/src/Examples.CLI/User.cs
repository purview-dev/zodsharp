using System.ComponentModel.DataAnnotations;
using ZodSharp.Core;

namespace ZodSharp.Examples.CLI;

[ZodSchema]
sealed class User
{
	[Required]
	[StringLength(50, MinimumLength = 3)]
	public string Name { get; set; } = string.Empty;

	[Required]
	[Range(0, 120)]
	public int Age { get; set; }

	[EmailAddress]
	public string? Email { get; set; }
}

partial class UserSchemaValidator
{
	public bool WasCalled;

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0390:Make method synchronous",
		Justification = "This is an example of an asynchronous custom validation method"
	)]
	public async ValueTask<ValidationResult<User>> CustomValidationAsync(User user, CancellationToken cancellationToken)
	{
		WasCalled = true;

		cancellationToken.ThrowIfCancellationRequested();

		return ValidationResult<User>.Success(user);
	}
}

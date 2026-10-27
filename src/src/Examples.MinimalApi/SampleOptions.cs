using System.ComponentModel.DataAnnotations;
using ZodSharp.Core;

namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// The sample's settings, bound from the <c>Sample</c> configuration section and validated with the generated
/// schema when the host starts. A failure names the configuration key (<c>Sample:SupportEmail: ...</c>), so it can
/// be found in <c>appsettings.json</c>.
/// </summary>
[ZodSchema]
sealed class SampleOptions
{
	/// <summary>
	/// The configuration section the options are bound from.
	/// </summary>
	public const string SectionName = "Sample";

	[Required]
	[StringLength(50, MinimumLength = 1)]
	public string? ApiName { get; set; }

	[Required]
	[EmailAddress]
	public string? SupportEmail { get; set; }

	[Range(1, 20)]
	public int MaxLinesPerOrder { get; set; } = 20;

	/// <summary>
	/// An asynchronous, service-aware rule. On .NET 11 the options validator implements
	/// <c>IAsyncValidateOptions&lt;T&gt;</c>, so <c>ValidateOnStart()</c> awaits it before the host starts: the support
	/// mailbox must not be one of the customer addresses.
	/// </summary>
	internal static async ValueTask<ValidationResult<SampleOptions>> CustomValidationAsync(
		SampleOptions value,
		IServiceProvider services,
		CancellationToken cancellationToken
	)
	{
		var registry = services.GetRequiredService<ICustomerEmailRegistry>();
		if (value.SupportEmail is null || !await registry.IsRegisteredAsync(value.SupportEmail, cancellationToken))
			return ValidationResult<SampleOptions>.Success(value);

		return ValidationResult<SampleOptions>.Failure(
			ValidationError.Create(
				"support_email_is_customer",
				"The support mailbox must not be a customer address",
				SampleOptionsSchema.Paths.SupportEmail
			)
		);
	}
}

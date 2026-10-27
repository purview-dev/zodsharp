#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZodSharp.AspNetCore.ClientValidation;

/// <summary>
/// In Development, warns at startup when the resolved <see cref="ClientValidationProvider"/> is not the ZodSharp
/// provider - something registered after <c>AddZodSharpClientValidation</c> replaced it, so ZodSharp rules are
/// silently missing from static-SSR forms.
/// </summary>
sealed partial class ZodClientValidationStartupFilter(
	IServiceProvider services,
	IHostEnvironment environment,
	ILogger<ZodClientValidationStartupFilter> logger
) : IStartupFilter
{
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		ArgumentNullException.ThrowIfNull(next);

		if (environment.IsDevelopment())
		{
			try
			{
				using var scope = services.CreateScope();
				var provider = scope.ServiceProvider.GetService<ClientValidationProvider>();
				if (provider is not ZodClientValidationProvider)
					LogProviderReplaced(logger, provider?.GetType().FullName ?? "(none)");
			}
			catch (InvalidOperationException ex)
			{
				LogProviderUnresolvable(logger, ex);
			}
		}

		return next;
	}

	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Warning,
		Message = "AddZodSharpClientValidation was called, but the resolved ClientValidationProvider is '{ProviderType}'. "
			+ "A registration made after AddZodSharpClientValidation replaced the ZodSharp provider, so static-SSR forms "
			+ "carry no ZodSharp client rules. Call AddZodSharpClientValidation after that registration."
	)]
	static partial void LogProviderReplaced(ILogger logger, string providerType);

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Warning,
		Message = "The ClientValidationProvider could not be resolved to check the ZodSharp client validation registration."
	)]
	static partial void LogProviderUnresolvable(ILogger logger, Exception exception);
}
#endif

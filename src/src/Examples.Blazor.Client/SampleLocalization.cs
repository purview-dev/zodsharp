using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.Examples.Blazor.Client;

/// <summary>
/// The sample's localization, shared by the server and the client <c>Program.cs</c>, so both render modes show the
/// same text.
/// </summary>
static class SampleLocalization
{
	/// <summary>
	/// The cultures the sample offers.
	/// </summary>
	public static readonly string[] SupportedCultures = ["en", "fr"];

	/// <summary>
	/// Registers localization and routes every ZodSharp message through <see cref="SampleMessages"/>. Messages are
	/// keyed by error code (or by the rule's English template), so one entry localizes a code wherever it is reported;
	/// codes and paths never change.
	/// </summary>
	/// <param name="services">The services.</param>
	public static void AddSampleLocalization(IServiceCollection services)
	{
		services.AddLocalization();

		// One formatter for every surface: the ProblemDetails the server API writes resolve IZodMessageFormatter, and
		// both Blazor routes (DataAnnotationsValidator through the bridge, and ZodValidator) use the bridge's.
		services.AddSingleton<IZodMessageFormatter>(static sp => new StringLocalizerZodMessageFormatter(
			sp.GetRequiredService<IStringLocalizerFactory>(),
			localizerProvider: static (_, factory) => factory.Create(typeof(SampleMessages)),
			sharedResourceType: typeof(SampleMessages)
		));
		services
			.AddOptions<ZodValidationOptions>()
			.Configure<IZodMessageFormatter>(static (options, formatter) => options.MessageFormatter = formatter);
	}

	/// <summary>
	/// Adopts the culture the server rendered the page in (<c>&lt;html lang&gt;</c>), so the WebAssembly runtime matches
	/// the server-rendered parts of the page.
	/// </summary>
	/// <param name="services">The client's services.</param>
	/// <returns>A task that completes when the culture is set.</returns>
	public static async Task ApplyDocumentCultureAsync(IServiceProvider services)
	{
		var js = services.GetRequiredService<IJSRuntime>();
		string? name;
		try
		{
			name = await js.InvokeAsync<string?>("zodSharpSample.culture");
		}
		catch (JSException)
		{
			return;
		}

		if (name is null || Array.IndexOf(SupportedCultures, name) < 0)
			return;

		CultureInfo culture = new(name);
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
	}
}

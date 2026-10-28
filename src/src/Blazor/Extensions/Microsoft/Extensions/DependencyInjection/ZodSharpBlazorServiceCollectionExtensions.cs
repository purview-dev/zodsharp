using Microsoft.Extensions.DependencyInjection.Extensions;
using ZodSharp.Blazor;
using ZodSharp.Extensions.Validation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers ZodSharp for Blazor forms.
/// </summary>
public static class ZodSharpBlazorServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers ZodSharp for Blazor forms on Blazor Server, Blazor WebAssembly and Auto render mode. Call it in
		/// the server and the client <c>Program.cs</c>.
		/// </summary>
		/// <param name="configureValidation">
		/// Optional configuration of the Microsoft.Extensions.Validation bridge - for example registering generated,
		/// trim-safe validator sources.
		/// </param>
		/// <param name="configure">Optional configuration of the Blazor components.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		/// <remarks>
		/// <para>
		/// Registers the Microsoft.Extensions.Validation bridge, so the standard <c>&lt;DataAnnotationsValidator /&gt;</c> validates every
		/// type that has a ZodSharp validator with ZodSharp - nested members, collections and dictionaries included -
		/// with no <c>[ValidatableType]</c> and no Microsoft.Extensions.Validation generator. Also registers the
		/// options <see cref="ZodValidator"/> reads and <see cref="TimeProvider.System"/> when no
		/// <see cref="TimeProvider"/> is registered.
		/// </para>
		/// <para>
		/// The bridge is registered for <b>type validation only</b>: it does not claim minimal-API parameters, so a host
		/// that also maps endpoints keeps its endpoints exactly as they were (no ZodSharp validation runs before their
		/// handlers). To validate ZodSharp-typed endpoint parameters as well, also call <c>AddZodSharpValidation()</c>
		/// (in either order), or set <see cref="ZodValidationOptions.ValidateParameters"/> through
		/// <paramref name="configureValidation"/>.
		/// </para>
		/// <para>
		/// It deliberately does not register an <see cref="IZodValidationReportSink"/>: on Blazor Server the scope
		/// is the circuit, so a scoped sink would accumulate the errors of every validation on the page.
		/// </para>
		/// </remarks>
		public IServiceCollection AddZodSharpBlazor(
			Action<ZodValidationOptions>? configureValidation = null,
			Action<ZodBlazorOptions>? configure = null
		)
		{
			ArgumentNullException.ThrowIfNull(services);

			// Type validation only: forms need it, the host's minimal APIs should not change because forms are used.
			services.AddZodSharpValidation(configureValidation, validateParameters: false);
			services.AddOptions<ZodBlazorOptions>();
			if (configure is not null)
				services.Configure(configure);

			services.TryAddSingleton(TimeProvider.System);
			return services;
		}
	}
}

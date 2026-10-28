#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZodSharp.AspNetCore.ClientValidation;
using ZodSharp.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers ZodSharp client-side validation rules for statically rendered Blazor forms.
/// </summary>
public static class ZodSharpClientValidationServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Emits client-side validation rules for ZodSharp rule attributes (<c>[StartsWith]</c>, <c>[UUID]</c>,
		/// <c>[MinValue]</c>, …) and registered runtime schemas in statically rendered (static SSR) Blazor forms,
		/// alongside ASP.NET Core's DataAnnotations rules. Reference Purview.ZodSharp.Blazor so the browser has the
		/// matching validators (<c>zodsharp-validation.js</c>, loaded automatically as a JavaScript initializer).
		/// </summary>
		/// <param name="configure">Optional configuration, for example runtime schemas to derive rules from.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		/// <remarks>
		/// <para>
		/// Safe to call before or after <c>AddRazorComponents</c>: the registered <see cref="ClientValidationProvider"/>
		/// is replaced by a decorator around it (or around ASP.NET Core's default when none is registered yet, which
		/// <c>AddRazorComponents</c>' <c>TryAdd</c> then leaves alone). A provider registered later still replaces it;
		/// in Development a startup check logs a warning when that happens.
		/// </para>
		/// <para>Client rules only improve the user experience; the server always validates the posted form.</para>
		/// </remarks>
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(ZodClientValidationTrimming.Message)]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ZodClientValidationTrimming.Message)]
		public IServiceCollection AddZodSharpClientValidation(Action<ZodClientValidationOptions>? configure = null)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddOptions<ZodClientValidationOptions>();
			if (configure is not null)
				services.Configure(configure);

			services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, ZodClientValidationStartupFilter>());

			var existing = services.LastOrDefault(static d =>
				d.ServiceType == typeof(ClientValidationProvider) && !d.IsKeyedService
			);
			if (existing?.ImplementationFactory is { } factory && factory.Target is ZodClientValidationRegistration)
				return services; // Already decorated.

			var inner = existing ?? FindDefaultProvider();
			services.RemoveAll<ClientValidationProvider>();

			ZodClientValidationRegistration registration = new(inner);
			services.Add(ServiceDescriptor.Scoped<ClientValidationProvider>(registration.Create));
			return services;
		}
	}

	/// <summary>
	/// Finds the descriptor <c>AddRazorComponents</c> registers for <see cref="ClientValidationProvider"/> by calling it
	/// on a scratch collection, so the decorator wraps the real default even when it is registered first.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(ZodClientValidationTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ZodClientValidationTrimming.Message)]
	static ServiceDescriptor? FindDefaultProvider()
	{
		ServiceCollection scratch = [];
		scratch.AddRazorComponents();
		return scratch.LastOrDefault(static d =>
			d.ServiceType == typeof(ClientValidationProvider) && !d.IsKeyedService
		);
	}

	/// <summary>
	/// The factory behind the decorated registration; also how repeated calls recognise it.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(ZodClientValidationTrimming.Message)]
	sealed class ZodClientValidationRegistration(ServiceDescriptor? inner)
	{
		public ZodClientValidationProvider Create(IServiceProvider provider) =>
			new(
				CreateInner(provider),
				provider.GetRequiredService<IOptions<ZodClientValidationOptions>>().Value,
				provider.GetService<IZodSchemaFactory>()
			);

		ClientValidationProvider? CreateInner(IServiceProvider provider)
		{
			if (inner is null)
				return null;

			try
			{
				return inner switch
				{
					{ ImplementationInstance: ClientValidationProvider instance } => instance,
					{ ImplementationFactory: { } factory } => factory(provider) as ClientValidationProvider,
					{ ImplementationType: { } type } => ActivatorUtilities.CreateInstance(provider, type)
						as ClientValidationProvider,
					_ => null,
				};
			}
			catch (InvalidOperationException)
			{
				// The default's dependencies are registered by AddRazorComponents; without it there is nothing to keep.
				return null;
			}
		}
	}
}
#endif

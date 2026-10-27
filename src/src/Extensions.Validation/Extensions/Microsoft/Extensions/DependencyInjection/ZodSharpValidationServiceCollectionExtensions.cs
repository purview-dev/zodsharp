using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers ZodSharp with Microsoft.Extensions.Validation.
/// </summary>
public static class ZodSharpValidationServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers ZodSharp as the first Microsoft.Extensions.Validation resolver, so minimal APIs, Blazor Server
		/// and Blazor WebAssembly validate every type that has a ZodSharp validator with ZodSharp.
		/// </summary>
		/// <param name="configure">Optional configuration of the bridge.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		/// <remarks>
		/// <para>
		/// Registers <see cref="IZodSchemaFactory"/> when none is registered yet (see <c>AddZodSharpFactory</c>), calls
		/// <c>AddValidation()</c>, and registers <see cref="ZodValidatableInfoResolver"/> as a singleton. The resolver
		/// is placed by a post-configure step, so it ends up first whether the application calls
		/// <c>AddValidation()</c> itself before this, after it, or not at all. Call <c>AddValidation()</c> in the
		/// application too when DataAnnotations types should be validated: the Microsoft.Extensions.Validation
		/// generator only intercepts calls in the application's own code.
		/// </para>
		/// <para>Calling this more than once registers the resolver once; each <paramref name="configure"/> applies.</para>
		/// </remarks>
		public IServiceCollection AddZodSharpValidation(Action<ZodValidationOptions>? configure = null)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.AddZodSharpFactory();
			services.AddValidation();
			services.AddOptions<ZodValidationOptions>();
			if (configure is not null)
				services.Configure(configure);

			services.TryAddSingleton(static sp => new ZodValidatableInfoResolver(
				sp.GetService<IZodSchemaFactory>(),
				sp.GetRequiredService<IOptions<ZodValidationOptions>>().Value
			));
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IPostConfigureOptions<ValidationOptions>, ZodValidationOptionsSetup>()
			);

			return services;
		}
	}
}

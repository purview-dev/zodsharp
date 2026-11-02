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
		/// <para>
		/// Microsoft.Extensions.Validation's endpoint filter validates every parameter a registered resolver claims, so
		/// this turns on minimal-API validation for ZodSharp-typed parameters: an invalid request is answered with a
		/// 400 before the handler runs. Opt an endpoint or group out with <c>DisableValidation()</c>, or every
		/// parameter with <see cref="ZodValidationOptions.ValidateParameters"/> = <see langword="false"/>.
		/// </para>
		/// <para>Calling this more than once registers the resolver once; each <paramref name="configure"/> applies.</para>
		/// </remarks>
		public IServiceCollection AddZodSharpValidation(Action<ZodValidationOptions>? configure = null) =>
			services.AddZodSharpValidation(configure, validateParameters: true);

		/// <summary>
		/// Registers ZodSharp as the first Microsoft.Extensions.Validation resolver, choosing whether it claims
		/// parameters (minimal-API endpoint validation); see <c>AddZodSharpValidation(configure)</c>.
		/// </summary>
		/// <param name="configure">Optional configuration of the bridge.</param>
		/// <param name="validateParameters">
		/// <see langword="true"/> to claim ZodSharp-typed parameters, so the endpoint filter validates them before the
		/// handler runs. <see langword="false"/> registers type validation only (Blazor forms,
		/// <c>ValidationOptions.ValidateAsync</c>) and leaves the host's endpoints as they were. Any registration that
		/// asks for parameters wins, and <see cref="ZodValidationOptions.ValidateParameters"/>, when set, overrides
		/// both. <c>AddZodSharpBlazor()</c> registers the bridge with <see langword="false"/>.
		/// </param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		public IServiceCollection AddZodSharpValidation(
			Action<ZodValidationOptions>? configure,
			bool validateParameters
		)
		{
			ArgumentNullException.ThrowIfNull(services);

			if (validateParameters)
				services.TryAddSingleton(new ZodParameterValidationRequest());

			services.AddZodSharpFactory();
			services.AddValidation();
			services.AddOptions<ZodValidationOptions>();
			if (configure is not null)
				services.Configure(configure);

			services.TryAddSingleton(static sp => new ZodValidatableInfoResolver(
				sp.GetService<IZodSchemaFactory>(),
				sp.GetRequiredService<IOptions<ZodValidationOptions>>().Value,
				parametersRequested: sp.GetService<ZodParameterValidationRequest>() is not null
			));
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IPostConfigureOptions<ValidationOptions>, ZodValidationOptionsSetup>()
			);

			return services;
		}
	}
}

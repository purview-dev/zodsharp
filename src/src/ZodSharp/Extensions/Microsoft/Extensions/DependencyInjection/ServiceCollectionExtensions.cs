using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using ZodSharp.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering ZodSharp services with <see cref="IServiceCollection"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Adds the <see cref="IZodSchemaFactory"/> to the dependency injection container.
		/// </summary>
		/// <param name="configure">An optional callback to configure the factory.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		/// <remarks>
		/// The factory is registered only if one is not already present — repeated calls (with any
		/// <paramref name="configure"/> callback) are ignored so earlier registrations are never
		/// overwritten. Every <see cref="ZodSchemaRegistration"/> in the container (added by the generated
		/// <c>AddZodSharp{Assembly}Schemas()</c> extensions) is applied before <paramref name="configure"/>.
		/// </remarks>
		public IServiceCollection AddZodSharpFactory(Action<IZodSchemaFactory>? configure = null)
		{
			services.TryAddSingleton<IZodSchemaFactory>(sp =>
			{
				ZodSchemaFactory factory = new();

				foreach (var registration in sp.GetServices<ZodSchemaRegistration>())
					registration.Register(factory);

				configure?.Invoke(factory);

				return factory;
			});

			return services;
		}

		/// <summary>
		/// Adds a deferred validator registration, applied when the <see cref="IZodSchemaFactory"/> is first
		/// resolved, and ensures a factory is registered. Generated <c>AddZodSharp{Assembly}Schemas()</c>
		/// extensions call this with the assembly's generated registration method.
		/// </summary>
		/// <param name="register">Registers validators into the factory.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		public IServiceCollection AddZodSchemaRegistration(Action<IZodSchemaFactory> register)
		{
			ArgumentNullException.ThrowIfNull(register);

			services.AddSingleton(new ZodSchemaRegistration(register));
			services.AddZodSharpFactory();

			return services;
		}

		/// <summary>
		/// Registers a generic <see cref="IValidateOptions{T}"/> singleton that resolves the
		/// source-generated schema validator for <typeparamref name="T"/> from the registered
		/// <see cref="IZodSchemaFactory"/>.
		/// </summary>
		/// <typeparam name="T">The options type to validate.</typeparam>
		/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="T"/>.</param>
		/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
		/// <remarks>
		/// Requires an <see cref="IZodSchemaFactory"/> to already be registered, for example via
		/// <see cref="AddZodSharpFactory(IServiceCollection, Action{IZodSchemaFactory}?)"/>
		/// or <c>AddZodSharp</c> from the <c>Purview.ZodSharp.AspNetCore</c> package.
		/// </remarks>
		public IServiceCollection AddZodSchemaOptionsValidator<T>(
			MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
		)
			where T : class
		{
			services.AddSingleton<IValidateOptions<T>>(sp => new ZodSchemaOptionsValidator<T>(
				sp.GetRequiredService<IZodSchemaFactory>(),
				missingValidatorBehavior
			));

			return services;
		}
	}
}

using System.ComponentModel;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using ZodSharp.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="ZodSchemaOptionsValidator{T}"/> instances with
/// <see cref="IServiceCollection"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class OptionsServiceCollectionExtensions
{
	/// <summary>
	/// Registers a generic <see cref="IValidateOptions{T}"/> singleton that resolves the
	/// source-generated schema validator for <typeparamref name="T"/> from the registered
	/// <see cref="IZodSchemaFactory"/>.
	/// </summary>
	/// <typeparam name="T">The options type to validate.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddZodSchemaOptionsValidator<T>(this IServiceCollection services)
		where T : class
	{
		if (services is null)
			throw new ArgumentNullException(nameof(services));

		services.AddSingleton<IValidateOptions<T>>(sp => new ZodSchemaOptionsValidator<T>(
			sp.GetRequiredService<IZodSchemaFactory>()
		));

		return services;
	}
}

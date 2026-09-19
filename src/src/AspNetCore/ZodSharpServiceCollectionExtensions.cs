using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// DI integration for ZodSharp schema validation.
/// </summary>
#if !NETSTANDARD2_1_OR_GREATER
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public static class ZodSharpServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="IZodSchemaFactory"/> as a singleton, applies configuration,
	/// and auto-registers source-generated validators from the configured assemblies.
	/// </summary>
	public static IServiceCollection AddZodSharp(
		this IServiceCollection services,
		Action<ZodSchemaFactoryOptions>? configure = null
	)
	{
		ZodSchemaFactoryOptions options = new();
		configure?.Invoke(options);

		services.AddSingleton<IZodSchemaFactory>(sp =>
		{
			ZodSchemaFactory factory = new();
			options.ConfigureFactory?.Invoke(factory);

			foreach (var assembly in options.ScanAssemblies)
				factory.RegisterFromAssembly(assembly);

			return factory;
		});

		return services;
	}

	/// <summary>
	/// Registers <see cref="ZodExceptionHandler"/> as an <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
	/// and configures <see cref="ZodProblemDetailsOptions"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Requires <c>app.UseExceptionHandler()</c> in the request pipeline for the handler to be invoked.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddZodSharpProblemDetails(
		this IServiceCollection services,
		Action<ZodProblemDetailsOptions>? configure = null
	)
	{
		if (configure is not null)
			services.Configure(configure);

		services.AddExceptionHandler<ZodExceptionHandler>();

		return services;
	}
}

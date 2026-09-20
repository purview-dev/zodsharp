using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Registers <see cref="IZodSchemaFactory"/> as a singleton, applies configuration,
		/// and auto-registers source-generated validators from the configured assemblies.
		/// </summary>
		public IServiceCollection AddZodSharp(Action<ZodSchemaFactoryOptions>? configure = null)
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
		/// Registers <see cref="ZodExceptionHandler"/> as an <see cref="AspNetCore.Diagnostics.IExceptionHandler"/>
		/// and configures <see cref="ZodProblemDetailsOptions"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Requires <c>app.UseExceptionHandler()</c> in the request pipeline for the handler to be invoked.
		/// </para>
		/// </remarks>
		public IServiceCollection AddZodSharpProblemDetails(
			Action<ZodProblemDetailsOptions>? configure = null,
			Action<ProblemDetailsOptions>? problemDetails = null
		)
		{
			services.AddProblemDetails(problemDetails);

			if (configure is not null)
				services.Configure(configure);

			services.AddExceptionHandler<ZodExceptionHandler>();

			return services;
		}
	}
}

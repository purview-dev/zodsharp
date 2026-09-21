using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
		/// <remarks>
		/// The factory is registered only if one is not already present — repeated calls (with any
		/// <c>configure</c> callback) are ignored so earlier registrations are never overwritten.
		/// </remarks>
		public IServiceCollection AddZodSharp(Action<ZodSchemaFactoryOptions>? configure = null)
		{
			ZodSchemaFactoryOptions options = new();
			configure?.Invoke(options);

			services.TryAddSingleton(
				new ZodSchemaFactoryConfiguration
				{
					ConfigureFactory = options.ConfigureFactory,
					ScanAssemblies = [.. options.ScanAssemblies],
					ScanAssemblyGraphs = [.. options.ScanAssemblyGraphs],
					ScanLoadedAssemblies = options.ScanLoadedAssemblies,
				}
			);
			services.TryAddSingleton<IZodSchemaFactory>(CreateFactory);

			return services;
		}

		/// <summary>
		/// Registers the assembly containing <typeparamref name="T"/> for scanning for generated validators.
		/// </summary>
		/// <typeparam name="T">The type whose assembly is scanned for generated validators.</typeparam>
		/// <returns>The <see cref="IServiceCollection"/> instance for chaining.</returns>
		public IServiceCollection AddZodSharpAssembly<T>() => AddZodSharpAssembly(services, typeof(T).Assembly);

		/// <summary>
		/// Adds the ZodSharp assembly containing the specified type to the service collection.
		/// </summary>
		/// <param name="type">The type whose assembly is added.</param>
		/// <returns>The same service collection for chaining additional registrations.</returns>
		public IServiceCollection AddZodSharpAssembly(Type type)
		{
			ArgumentNullException.ThrowIfNull(type);

			return AddZodSharpAssembly(services, type.Assembly);
		}

		/// <summary>
		/// Registers a specific assembly to scan for generated validators.
		/// </summary>
		public IServiceCollection AddZodSharpAssembly(Assembly assembly)
		{
			ArgumentNullException.ThrowIfNull(assembly);

			services.AddSingleton(new ZodSchemaAssemblyRegistration(assembly, ZodSchemaAssemblyRegistrationKind.Exact));
			services.TryAddSingleton<IZodSchemaFactory>(CreateFactory);

			return services;
		}

		/// <summary>
		/// Registers the assembly containing <typeparamref name="T"/> and its referenced assemblies for scanning for
		/// generated validators.
		/// </summary>
		/// <remarks>The root assembly and all directly or indirectly referenced assemblies are scanned for generated
		/// validators.</remarks>
		/// <typeparam name="T">The type whose assembly graph is scanned for generated validators.</typeparam>
		/// <returns>The <see cref="IServiceCollection"/> instance for chaining.</returns>
		public IServiceCollection AddZodSharpAssemblyGraph<T>() =>
			AddZodSharpAssemblyGraph(services, typeof(T).Assembly);

		/// <summary>
		/// Registers the ZodSharp assembly graph for the assembly containing the specified type.
		/// </summary>
		/// <param name="type">The type whose assembly contains the ZodSharp definitions to register.</param>
		/// <returns>The service collection for chaining additional registrations.</returns>
		public IServiceCollection AddZodSharpAssemblyGraph(Type type)
		{
			ArgumentNullException.ThrowIfNull(type);

			return AddZodSharpAssemblyGraph(services, type.Assembly);
		}

		/// <summary>
		/// Registers a root assembly whose referenced assembly graph should be scanned for generated validators.
		/// </summary>
		public IServiceCollection AddZodSharpAssemblyGraph(Assembly rootAssembly)
		{
			ArgumentNullException.ThrowIfNull(rootAssembly);

			services.AddSingleton(
				new ZodSchemaAssemblyRegistration(rootAssembly, ZodSchemaAssemblyRegistrationKind.Graph)
			);
			services.TryAddSingleton<IZodSchemaFactory>(CreateFactory);

			return services;
		}

		/// <summary>
		/// Registers a scan across all assemblies currently loaded into the application domain for generated validators.
		/// </summary>
		public IServiceCollection AddZodSharpLoadedAssemblies()
		{
			services.TryAddSingleton<ZodSchemaLoadedAssemblyRegistration>();
			services.TryAddSingleton<IZodSchemaFactory>(CreateFactory);

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

	static ZodSchemaFactory CreateFactory(IServiceProvider serviceProvider)
	{
		ZodSchemaFactory factory = new();
		var configuration = serviceProvider.GetService<ZodSchemaFactoryConfiguration>();

		configuration?.ConfigureFactory?.Invoke(factory);

		foreach (var assembly in GetAssembliesToScan(serviceProvider, configuration))
			factory.RegisterFromAssembly(assembly);

		return factory;
	}

	static IEnumerable<Assembly> GetAssembliesToScan(
		IServiceProvider serviceProvider,
		ZodSchemaFactoryConfiguration? configuration
	)
	{
		HashSet<string> seenAssemblyNames = new(StringComparer.Ordinal);

		foreach (var assembly in configuration?.ScanAssemblies ?? [])
			if (TryMarkAssemblySeen(assembly, seenAssemblyNames))
				yield return assembly;

		foreach (var registration in serviceProvider.GetServices<ZodSchemaAssemblyRegistration>())
			if (
				registration.Kind == ZodSchemaAssemblyRegistrationKind.Exact
				&& TryMarkAssemblySeen(registration.Assembly, seenAssemblyNames)
			)
				yield return registration.Assembly;

		foreach (var assembly in GetAssembliesFromGraphs(configuration?.ScanAssemblyGraphs ?? [], seenAssemblyNames))
			yield return assembly;

		foreach (
			var assembly in GetAssembliesFromGraphs(
				serviceProvider
					.GetServices<ZodSchemaAssemblyRegistration>()
					.Where(static registration => registration.Kind == ZodSchemaAssemblyRegistrationKind.Graph)
					.Select(static registration => registration.Assembly),
				seenAssemblyNames
			)
		)
			yield return assembly;

		if (
			(configuration?.ScanLoadedAssemblies ?? false)
			|| serviceProvider.GetService<ZodSchemaLoadedAssemblyRegistration>() is not null
		)
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				if (TryMarkAssemblySeen(assembly, seenAssemblyNames))
					yield return assembly;
	}

	static IEnumerable<Assembly> GetAssembliesFromGraphs(
		IEnumerable<Assembly> rootAssemblies,
		HashSet<string> seenAssemblyNames
	)
	{
		Queue<Assembly> pendingAssemblies = new(rootAssemblies);
		HashSet<string> queuedAssemblies = new(StringComparer.Ordinal);

		foreach (var assembly in pendingAssemblies)
			queuedAssemblies.Add(GetAssemblyIdentity(assembly));

		while (pendingAssemblies.TryDequeue(out var assembly))
		{
			if (TryMarkAssemblySeen(assembly, seenAssemblyNames))
				yield return assembly;

			foreach (var reference in assembly.GetReferencedAssemblies())
			{
				var referenceIdentity = GetAssemblyIdentity(reference);
				if (!queuedAssemblies.Add(referenceIdentity))
					continue;

				if (TryLoadAssembly(reference) is { } referencedAssembly)
					pendingAssemblies.Enqueue(referencedAssembly);
			}
		}
	}

	static Assembly? TryLoadAssembly(AssemblyName assemblyName)
	{
		try
		{
			return Assembly.Load(assemblyName);
		}
		catch (FileLoadException)
		{
			return null;
		}
		catch (FileNotFoundException)
		{
			return null;
		}
		catch (BadImageFormatException)
		{
			return null;
		}
	}

	static bool TryMarkAssemblySeen(Assembly assembly, HashSet<string> seenAssemblyNames) =>
		seenAssemblyNames.Add(GetAssemblyIdentity(assembly));

	static string GetAssemblyIdentity(Assembly assembly) =>
		assembly.FullName ?? assembly.GetName().Name ?? string.Empty;

	static string GetAssemblyIdentity(AssemblyName assemblyName) =>
		assemblyName.FullName ?? assemblyName.Name ?? string.Empty;
}

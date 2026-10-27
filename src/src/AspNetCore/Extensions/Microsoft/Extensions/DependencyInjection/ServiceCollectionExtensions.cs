using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	const string GraphScanRequirement =
		"Walks the assembly reference graph, which a trimmer can remove (Assembly.GetReferencedAssemblies). Register "
		+ "validators with the generated AddZodSharp{AssemblyId}Schemas() extension (or AddZodSharpAssembly) instead.";

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
					ScanAssemblyGraphs = ReadGraphRoots(options),
					ScanLoadedAssemblies = options.ScanLoadedAssemblies,
				}
			);
			services.TryAddSingleton<IZodSchemaFactory>(CreateFactory);
			AddProblemDetailsWriter(services);

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
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(GraphScanRequirement)]
		public IServiceCollection AddZodSharpAssemblyGraph<T>() =>
			AddZodSharpAssemblyGraph(services, typeof(T).Assembly);

		/// <summary>
		/// Registers the ZodSharp assembly graph for the assembly containing the specified type.
		/// </summary>
		/// <param name="type">The type whose assembly contains the ZodSharp definitions to register.</param>
		/// <returns>The service collection for chaining additional registrations.</returns>
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(GraphScanRequirement)]
		public IServiceCollection AddZodSharpAssemblyGraph(Type type)
		{
			ArgumentNullException.ThrowIfNull(type);

			return AddZodSharpAssemblyGraph(services, type.Assembly);
		}

		/// <summary>
		/// Registers a root assembly whose referenced assembly graph should be scanned for generated validators.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(GraphScanRequirement)]
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
		/// Registers <see cref="ZodExceptionHandler"/> as an <see cref="AspNetCore.Diagnostics.IExceptionHandler"/>,
		/// the shared <see cref="ZodProblemDetailsWriter"/> and ASP.NET Core's ProblemDetails service, and configures
		/// <see cref="ZodProblemDetailsOptions"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Requires <c>app.UseExceptionHandler()</c> in the request pipeline for the handler to be invoked.
		/// </para>
		/// <para>
		/// Also adds the ZodSharp ProblemDetails JSON metadata to the minimal-API <c>JsonOptions</c> resolver chain,
		/// so the payload serializes under Native AOT.
		/// </para>
		/// <para>
		/// On .NET 11 it also enriches the built-in Microsoft.Extensions.Validation pipeline (<c>AddZodSharpValidation()</c>):
		/// it registers a report sink that collects the bridge's full errors on the current request (<c>ZodRequestValidationReport</c>, in <c>HttpContext.Features</c>) and composes a
		/// <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/> callback that rewrites the pipeline's
		/// <see cref="HttpValidationProblemDetails"/> with <see cref="ZodProblemDetailsWriter.Apply"/> and mirrors the
		/// status to the response. The ZodSharp callback runs first and any callback the application configures - before
		/// or after this call - runs after it, so the application keeps the last word.
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

			AddProblemDetailsWriter(services);
			services.AddExceptionHandler<ZodExceptionHandler>();
#if NET11_0_OR_GREATER
			AddValidationPipelineEnrichment(services);
#endif

			return services;
		}
	}

#if NET11_0_OR_GREATER
	/// <summary>
	/// Enriches the built-in Microsoft.Extensions.Validation pipeline's ProblemDetails with the ZodSharp contract: a
	/// per-request <see cref="ZodRequestValidationReport"/> (in <c>HttpContext.Features</c>) collects the bridge's full errors, and a
	/// composed <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/> callback rewrites the response from it.
	/// </summary>
	static void AddValidationPipelineEnrichment(IServiceCollection services)
	{
		// The sink stores errors on the current HTTP request, not in the DI scope: a Blazor Server circuit is one
		// long-lived scope, and a scoped collector there would only ever grow.
		services.AddHttpContextAccessor();
		services.TryAddSingleton<
			ZodSharp.Extensions.Validation.IZodValidationReportSink,
			HttpRequestValidationReportSink
		>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<
				IPostConfigureOptions<ProblemDetailsOptions>,
				ZodValidationProblemDetailsCustomizer
			>()
		);
	}
#endif

	static void AddProblemDetailsWriter(IServiceCollection services)
	{
		services.AddOptions<ZodProblemDetailsOptions>();
		services.TryAddSingleton(static sp => new ZodProblemDetailsWriter(
			sp.GetRequiredService<IOptions<ZodProblemDetailsOptions>>(),
			sp.GetService<IZodMessageFormatter>(),
			sp.GetService<ILogger<ZodProblemDetailsWriter>>()
		));

		if (!services.Any(static descriptor => descriptor.ServiceType == typeof(ZodProblemDetailsJsonMarker)))
		{
			services.AddSingleton<ZodProblemDetailsJsonMarker>();
			services.ConfigureHttpJsonOptions(static options =>
				options.SerializerOptions.TypeInfoResolverChain.Add(ZodProblemDetailsJsonContext.Default)
			);
		}
	}

	static ZodSchemaFactory CreateFactory(IServiceProvider serviceProvider)
	{
		ZodSchemaFactory factory = new();
		var configuration = serviceProvider.GetService<ZodSchemaFactoryConfiguration>();

		// Trim-safe registrations added by the generated AddZodSharp{Assembly}Schemas() extensions.
		foreach (var registration in serviceProvider.GetServices<ZodSchemaRegistration>())
			registration.Register(factory);

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
		HashSet<string> seenAssemblyNames = [with(StringComparer.Ordinal)];

		foreach (var assembly in configuration?.ScanAssemblies ?? [])
			if (TryMarkAssemblySeen(assembly, seenAssemblyNames))
				yield return assembly;

		foreach (var registration in serviceProvider.GetServices<ZodSchemaAssemblyRegistration>())
			if (
				registration.Kind == ZodSchemaAssemblyRegistrationKind.Exact
				&& TryMarkAssemblySeen(registration.Assembly, seenAssemblyNames)
			)
				yield return registration.Assembly;

		foreach (var assembly in GetGraphAssemblies(serviceProvider, configuration, seenAssemblyNames))
			yield return assembly;

		if (
			(configuration?.ScanLoadedAssemblies ?? false)
			|| serviceProvider.GetService<ZodSchemaLoadedAssemblyRegistration>() is not null
		)
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				if (TryMarkAssemblySeen(assembly, seenAssemblyNames))
					yield return assembly;
	}

	// Graph scanning needs Assembly.GetReferencedAssemblies, which trimming can invalidate. It is opted into only through
	// [RequiresUnreferencedCode] APIs (AddZodSharpAssemblyGraph, ScanAssemblyGraphs), so the requirement is stated where
	// the application opts in. On .NET 9+ the walk is also removed when trimming (ZodReflectionFeature is a feature guard);
	// .NET 8's trimming tools ignore feature guards, so there the internal reads carry a suppression instead.
#if !NET9_0_OR_GREATER
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "net8.0 only: graph roots exist only when the application called a [RequiresUnreferencedCode] "
			+ "opt-in (AddZodSharpAssemblyGraph or ScanAssemblyGraphs), which already warned it."
	)]
#endif
	static IReadOnlyList<Assembly> ReadGraphRoots(ZodSchemaFactoryOptions options) =>
		ZodReflectionFeature.IsSupported ? [.. options.ScanAssemblyGraphs] : [];

#if !NET9_0_OR_GREATER
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "net8.0 only: graph roots exist only when the application called a [RequiresUnreferencedCode] "
			+ "opt-in (AddZodSharpAssemblyGraph or ScanAssemblyGraphs), which already warned it."
	)]
#endif
	static IEnumerable<Assembly> GetGraphAssemblies(
		IServiceProvider serviceProvider,
		ZodSchemaFactoryConfiguration? configuration,
		HashSet<string> seenAssemblyNames
	) =>
		ZodReflectionFeature.IsSupported
			? GetAssembliesFromGraphs(
				[
					.. configuration?.ScanAssemblyGraphs ?? [],
					.. serviceProvider
						.GetServices<ZodSchemaAssemblyRegistration>()
						.Where(static registration => registration.Kind == ZodSchemaAssemblyRegistrationKind.Graph)
						.Select(static registration => registration.Assembly),
				],
				seenAssemblyNames
			)
			: [];

	/// <remarks>
	/// Walking an assembly's reference graph cannot be followed by a trimmer, which may also have removed
	/// the references entirely. Prefer <c>ScanAssemblies</c> with an explicit assembly, or register
	/// validators directly, in a trimmed or Native AOT host — the graph walk also loads the whole
	/// transitive closure at container-build time, which is a measurable startup cost in a large solution.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(GraphScanRequirement)]
	static IEnumerable<Assembly> GetAssembliesFromGraphs(
		IEnumerable<Assembly> rootAssemblies,
		HashSet<string> seenAssemblyNames
	)
	{
		Queue<Assembly> pendingAssemblies = new(rootAssemblies);
		HashSet<string> queuedAssemblies = [with(StringComparer.Ordinal)];

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

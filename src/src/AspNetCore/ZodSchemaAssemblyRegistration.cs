using System.Reflection;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

enum ZodSchemaAssemblyRegistrationKind
{
	Exact,
	Graph,
}

sealed record ZodSchemaAssemblyRegistration(Assembly Assembly, ZodSchemaAssemblyRegistrationKind Kind);

sealed class ZodSchemaLoadedAssemblyRegistration;

sealed class ZodSchemaFactoryConfiguration
{
	public required IReadOnlyList<Assembly> ScanAssemblies { get; init; }

	public required IReadOnlyList<Assembly> ScanAssemblyGraphs { get; init; }

	public required bool ScanLoadedAssemblies { get; init; }

	public required Action<IZodSchemaFactory>? ConfigureFactory { get; init; }
}

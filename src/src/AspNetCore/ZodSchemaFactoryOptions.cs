using System.Reflection;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Configuration options for <c>AddZodSharp</c>.
/// </summary>
public sealed class ZodSchemaFactoryOptions
{
	/// <summary>
	/// Assemblies to scan for <see cref="ZodSchemaGeneratedAttribute"/> and auto-register generated validators.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Assembly> ScanAssemblies { get; } = [];

	/// <summary>
	/// Root assemblies whose referenced assembly graphs should be scanned for
	/// <see cref="ZodSchemaGeneratedAttribute"/> and auto-register generated validators.
	/// </summary>
	/// <remarks>
	/// The reference-graph walk needs <see cref="Assembly.GetReferencedAssemblies"/>, which trimming can invalidate, so it
	/// is removed when trimming and this property is annotated <c>[RequiresUnreferencedCode]</c>. Register validators with
	/// the generated <c>AddZodSharp{AssemblyId}Schemas()</c> extension, or list assemblies in
	/// <see cref="ScanAssemblies"/>, instead.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
	public List<Assembly> ScanAssemblyGraphs
	{
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
			"Walks the assembly reference graph, which a trimmer can remove. Register validators with the generated "
				+ "AddZodSharp{AssemblyId}Schemas() extension, or list assemblies in ScanAssemblies, instead."
		)]
		get;
	} = [];

	/// <summary>
	/// Whether to scan all currently loaded <see cref="AppDomain"/> assemblies for
	/// <see cref="ZodSchemaGeneratedAttribute"/> and auto-register generated validators.
	/// </summary>
	public bool ScanLoadedAssemblies { get; set; }

	/// <summary>
	/// Action applied to the factory before the scan, for registering hand-built validators.
	/// </summary>
	public Action<IZodSchemaFactory>? ConfigureFactory { get; set; }
}

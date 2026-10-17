namespace ZodSharp;

/// <summary>
/// Declares assembly-wide defaults for the <c>[ZodSchema]</c> source generator.
/// </summary>
/// <remarks>
/// <para>
/// A single attribute per assembly carries the defaults a generated schema falls back to when neither the
/// property nor the containing type states a value. It is the source-level counterpart to the
/// <c>ZodSharp*</c> MSBuild properties and overrides them, so a default can be committed with the code
/// rather than a build file.
/// </para>
/// <para>
/// <code>
/// [assembly: ZodSharpSchemaDefaults(AllowNulls = true)]
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class ZodSharpSchemaDefaultsAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the assembly-wide default for whether collection properties allow <see langword="null"/>
	/// elements.
	/// </summary>
	/// <remarks>
	/// The setting only takes effect when it is explicitly assigned; an attribute that leaves it out defers to
	/// the <c>ZodSharpAllowNulls</c> MSBuild property, whose own default is to reject null elements. A
	/// nullable <c>bool?</c> cannot express that, because <see cref="Nullable{T}"/> is not a legal attribute
	/// parameter type, so the unset state is the absence of the named argument rather than a null value.
	/// </remarks>
	public bool AllowNulls { get; set; }
}

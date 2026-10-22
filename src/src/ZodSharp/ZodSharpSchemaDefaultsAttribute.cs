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

	/// <summary>
	/// Gets or sets the assembly-wide default for the JSON wire form assumed for
	/// <see cref="DateTimeOffset"/> values.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the assembly-level default for schema generation. It overrides the
	/// <c>ZodSharpDateTimeOffsetSerialization</c> MSBuild property, and a type or property can override it
	/// with <see cref="DateTimeOffsetBehaviorAttribute"/>. When neither this property nor the MSBuild
	/// property is set, <see cref="DateTimeOffsetSerialization.PreserveOffset"/> applies.
	/// </para>
	/// <para>
	/// The setting only shapes the exported JSON Schema. <see cref="DateTimeOffsetSerialization.PreserveOffset"/>
	/// exports an offset-aware sentinel pattern that matches the serializer's normal behavior.
	/// <see cref="DateTimeOffsetSerialization.Utc"/> is narrower and must be paired with the integration
	/// package's UTC converter so the serializer and the schema agree.
	/// </para>
	/// </remarks>
	public DateTimeOffsetSerialization DateTimeOffsetSerialization { get; set; }
}

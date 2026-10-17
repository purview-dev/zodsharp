namespace ZodSharp;

/// <summary>
/// Overrides the collection validation behaviour for a single property (or positional record parameter).
/// </summary>
/// <remarks>
/// <para>
/// The only setting today is <see cref="AllowNulls"/>, which controls whether a collection may contain
/// <see langword="null"/> elements. Null elements are rejected by default. Set <c>AllowNulls = true</c> to
/// accept them, or <c>AllowNulls = false</c> to reject them even when the element type is explicitly
/// nullable (<c>T?</c>) or a wider default allows them.
/// </para>
/// <para>
/// Apply to a collection property. The attribute is merged in from a positional record parameter, so
/// <c>[CollectionBehavior(AllowNulls = true)] RepositoryItem[] Items</c> works on a positional record.
/// </para>
/// <para>
/// Precedence, highest first: this attribute, an explicitly nullable element type (<c>T?</c>), the
/// containing type's <c>[ZodSchema(AllowNulls = ...)]</c> option, the assembly-level
/// <c>[assembly: ZodSharpSchemaDefaults(AllowNulls = ...)]</c> attribute, and the <c>ZodSharpAllowNulls</c>
/// MSBuild property. The built-in default is to reject null elements.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class CollectionBehaviorAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether the collection may contain <see langword="null"/> elements.
	/// </summary>
	/// <remarks>
	/// The setting only takes effect when it is explicitly assigned; an attribute that leaves it out defers to
	/// the next level in the precedence chain. A nullable <c>bool?</c> cannot express that, because
	/// <see cref="Nullable{T}"/> is not a legal attribute parameter type, so the unset state is the absence of
	/// the named argument rather than a null value.
	/// </remarks>
	public bool AllowNulls { get; set; }
}

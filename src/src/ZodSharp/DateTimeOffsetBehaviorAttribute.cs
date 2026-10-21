namespace ZodSharp;

/// <summary>
/// Overrides the <see cref="DateTimeOffsetSerialization"/> assumed for a type or a single
/// <see cref="DateTimeOffset"/> property (or positional record parameter).
/// </summary>
/// <remarks>
/// <para>
/// The setting only shapes the JSON Schema the <c>[ZodSchema]</c> generator exports; it does not change how
/// values are serialized. <see cref="DateTimeOffsetSerialization.PreserveOffset"/> matches the serializer's
/// normal behavior and exports an exact offset-aware sentinel pattern.
/// <see cref="DateTimeOffsetSerialization.Utc"/> is a narrower contract; pair it with the integration
/// package's UTC converter, which writes UTC and rejects a non-UTC offset on read.
/// </para>
/// <para>
/// Precedence, highest first: this attribute on the property (or positional parameter), this attribute on
/// the containing type, <c>[assembly: ZodSharpSchemaDefaults(DateTimeOffsetSerialization = ...)]</c>, the
/// <c>ZodSharpDateTimeOffsetSerialization</c> MSBuild property (for example in <c>Directory.Build.props</c>),
/// and finally <see cref="DateTimeOffsetSerialization.PreserveOffset"/>.
/// </para>
/// <para>
/// A <c>[Scalar&lt;DateTimeOffset&gt;]</c> owns its schema, so apply the attribute to the scalar type rather
/// than to a property whose type is that scalar.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="DateTimeOffsetBehaviorAttribute"/> class.
/// </remarks>
/// <param name="serialization">The DateTimeOffset wire form assumed for JSON Schema generation.</param>
[AttributeUsage(
	AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Parameter,
	AllowMultiple = false,
	Inherited = false
)]
public sealed class DateTimeOffsetBehaviorAttribute(DateTimeOffsetSerialization serialization) : Attribute
{
	/// <summary>
	/// Gets the DateTimeOffset wire form assumed for JSON Schema generation.
	/// </summary>
	public DateTimeOffsetSerialization Serialization { get; } = serialization;
}

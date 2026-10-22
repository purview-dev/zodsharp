namespace ZodSharp.JsonSchema;

/// <summary>
/// Options for converting ZodSharp schemas to JSON Schema.
/// </summary>
public sealed class ToJsonSchemaOptions
{
	/// <summary>
	/// Whether to include the $schema property in the output.
	/// Default: true
	/// </summary>
	public bool IncludeSchema { get; set; } = true;

	/// <summary>
	/// Custom $id for the schema.
	/// </summary>
	public string? Id { get; set; }

	/// <summary>
	/// Custom title for the schema.
	/// </summary>
	public string? Title { get; set; }

	/// <summary>
	/// Gets or sets the wire form assumed for a <see cref="DateTimeOffset"/> sentinel mapping.
	/// </summary>
	/// <remarks>
	/// The generated <c>BuildJsonSchema</c> path resolves this from
	/// <see cref="DateTimeOffsetBehaviorAttribute"/> and the assembly/MSBuild defaults. This option is the
	/// runtime equivalent for a hand-built fluent schema, which has no compile-time metadata to read.
	/// </remarks>
	public DateTimeOffsetSerialization DateTimeOffsetSerialization { get; set; } =
		DateTimeOffsetSerialization.PreserveOffset;
}

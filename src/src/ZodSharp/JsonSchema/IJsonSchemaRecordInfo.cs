namespace ZodSharp.JsonSchema;

/// <summary>
/// A non-generic view of a record (dictionary) schema, for the JSON Schema exporter.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ToJsonSchemaConverter"/> walks schemas through a non-generic <see cref="object"/>, so it cannot
/// pattern-match <c>ZodRecord&lt;TValue&gt;</c> without knowing <c>TValue</c>. This seam exposes the value
/// schema the export needs; without it a record falls through to the generic fallback and exports as the
/// empty schema.
/// </para>
/// <para>
/// It is deliberately <c>internal</c> — it is a cross-type detail of this assembly, not public API.
/// </para>
/// </remarks>
interface IJsonSchemaRecordInfo
{
	/// <summary>
	/// Gets the value schema, boxed so the member is reachable without naming the value type.
	/// </summary>
	object ValueSchema { get; }
}

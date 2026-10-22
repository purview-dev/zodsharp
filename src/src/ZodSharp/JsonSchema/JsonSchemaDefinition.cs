using System.Text.Json.Serialization;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Represents a JSON Schema definition.
/// Based on Draft 2020-12 specification.
/// </summary>
/// <remarks>
/// The four JSON Schema keyword members carry an explicit <see cref="JsonPropertyNameAttribute"/> rather
/// than relying on a custom <c>JsonNamingPolicy</c>. A naming policy is a runtime object, so the
/// System.Text.Json source generator cannot reproduce it, and a context-based (trim- and AOT-safe)
/// contract would have emitted <c>schema</c>/<c>id</c>/<c>ref</c>/<c>defs</c> instead of the specification's
/// <c>$</c>-prefixed names. Declaring the names here keeps the wire format identical on both the
/// reflection and source-generated paths. Newtonsoft.Json ignores these attributes and keeps using its own
/// resolver.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
public class JsonSchemaDefinition
{
	/// <summary>$schema - The JSON Schema version URI</summary>
	[JsonPropertyName("$schema")]
	public string? Schema { get; set; }

	/// <summary>$id - Schema identifier</summary>
	[JsonPropertyName("$id")]
	public string? Id { get; set; }

	/// <summary>$ref - Reference to another schema</summary>
	[JsonPropertyName("$ref")]
	public string? Ref { get; set; }

	/// <summary>type - The data type (string, number, integer, boolean, object, array, null)</summary>
	public string? Type { get; set; }

	/// <summary>title - Schema title</summary>
	public string? Title { get; set; }

	/// <summary>description - Schema description</summary>
	public string? Description { get; set; }

	/// <summary>default - Default value</summary>
	public object? Default { get; set; }

	/// <summary>format - String format (email, uri, uuid, date-time, etc.)</summary>
	public string? Format { get; set; }

	// ========== String Constraints ==========

	/// <summary>minLength - Minimum string length</summary>
	public int? MinLength { get; set; }

	/// <summary>maxLength - Maximum string length</summary>
	public int? MaxLength { get; set; }

	/// <summary>pattern - Regex pattern for string validation</summary>
	public string? Pattern { get; set; }

	// ========== Number Constraints ==========

	/// <summary>minimum - Minimum value (inclusive)</summary>
	public double? Minimum { get; set; }

	/// <summary>maximum - Maximum value (inclusive)</summary>
	public double? Maximum { get; set; }

	/// <summary>exclusiveMinimum - Exclusive minimum value</summary>
	public double? ExclusiveMinimum { get; set; }

	/// <summary>exclusiveMaximum - Exclusive maximum value</summary>
	public double? ExclusiveMaximum { get; set; }

	/// <summary>multipleOf - Value must be a multiple of this</summary>
	public double? MultipleOf { get; set; }

	// ========== Array Constraints ==========

	/// <summary>items - Schema for array items</summary>
	public JsonSchemaDefinition? Items { get; set; }

	/// <summary>minItems - Minimum array length</summary>
	public int? MinItems { get; set; }

	/// <summary>maxItems - Maximum array length</summary>
	public int? MaxItems { get; set; }

	/// <summary>uniqueItems - Whether array items must be unique</summary>
	public bool? UniqueItems { get; set; }

	// ========== Object Constraints ==========

	/// <summary>properties - Object property schemas</summary>
	public Dictionary<string, JsonSchemaDefinition>? Properties { get; set; }

	/// <summary>required - Required property names</summary>
	public List<string>? Required { get; set; }

	/// <summary>additionalProperties - Schema for additional properties, or false to disallow</summary>
	public object? AdditionalProperties { get; set; }

	/// <summary>minProperties - Minimum number of members in an object</summary>
	public int? MinProperties { get; set; }

	/// <summary>maxProperties - Maximum number of members in an object</summary>
	public int? MaxProperties { get; set; }

	// ========== Negation ==========

	/// <summary>not - The data must not match this schema (for example a sentinel value that is rejected).</summary>
	public JsonSchemaDefinition? Not { get; set; }

	/// <summary>contentEncoding - The encoding of a string (for example <c>base64</c>).</summary>
	public string? ContentEncoding { get; set; }

	// ========== Composition ==========

	/// <summary>anyOf - Match any of the schemas</summary>
	public List<JsonSchemaDefinition>? AnyOf { get; set; }

	/// <summary>oneOf - Match exactly one of the schemas</summary>
	public List<JsonSchemaDefinition>? OneOf { get; set; }

	/// <summary>allOf - Match all of the schemas</summary>
	public List<JsonSchemaDefinition>? AllOf { get; set; }

	// ========== Enum & Const ==========

	/// <summary>enum - List of allowed values</summary>
	public List<object?>? Enum { get; set; }

	/// <summary>const - Exact value required</summary>
	public object? Const { get; set; }

	// ========== Definitions ==========

	/// <summary>$defs - Schema definitions (Draft 2020-12)</summary>
	[JsonPropertyName("$defs")]
	public Dictionary<string, JsonSchemaDefinition>? Defs { get; set; }

	/// <summary>definitions - Schema definitions (Draft 07 and earlier)</summary>
	public Dictionary<string, JsonSchemaDefinition>? Definitions { get; set; }

	// ========== Metadata ==========

	/// <summary>deprecated - Whether this schema is deprecated</summary>
	public bool? Deprecated { get; set; }

	/// <summary>readOnly - Whether this value is read-only</summary>
	public bool? ReadOnly { get; set; }

	/// <summary>writeOnly - Whether this value is write-only</summary>
	public bool? WriteOnly { get; set; }

	/// <summary>examples - Example values</summary>
	public List<object?>? Examples { get; set; }

	/// <summary>nullable - OpenAPI 3.0 nullable flag</summary>
	public bool? Nullable { get; set; }

	// ========== Extensions ==========

	/// <summary>
	/// Names of validation rules that could not be represented as JSON Schema keywords. An exported schema
	/// that lists a rule here is <b>incomplete</b> — a consumer cannot rely on it to reproduce that rule's
	/// validation — so the omission is surfaced in the schema rather than dropped silently.
	/// </summary>
	/// <remarks>
	/// Emitted as the vendor extension <c>x-zodsharp-unsupported</c>. The name is declared explicitly so both
	/// serializers write it verbatim (Newtonsoft.Json maps it through
	/// <c>ZodSharp.JsonSchema.NewtonsoftJson.JsonSchemaNamingStrategy</c>).
	/// </remarks>
	[JsonPropertyName("x-zodsharp-unsupported")]
	public List<string>? UnsupportedRules { get; set; }
}

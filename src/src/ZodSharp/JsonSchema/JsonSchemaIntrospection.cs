namespace ZodSharp.JsonSchema;

/// <summary>
/// A non-generic view of a nullable schema, for the JSON Schema exporter.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="IJsonSchemaInnerSchema"/> because the two mean different things to the
/// exporter. An optional schema exports as its inner schema — optionality lives in the parent's
/// <c>required</c> list — so it is simply unwrapped. A nullable schema changes the exported shape to
/// <c>anyOf: [inner, {"type": "null"}]</c>, so it must be recognised rather than unwrapped.
/// </remarks>
interface IJsonSchemaNullableInfo
{
	/// <summary>
	/// Gets the schema the null is permitted alongside, boxed so it is reachable without naming its type.
	/// </summary>
	object NullableInnerSchema { get; }
}

/// <summary>
/// A non-generic view of a literal schema, for the JSON Schema exporter.
/// </summary>
/// <remarks>
/// <c>ZodLiteral&lt;T&gt;</c> is generic, so the exporter cannot pattern-match it without naming <c>T</c>.
/// This replaces matching the type's name and reading its value by private field name.
/// </remarks>
interface IJsonSchemaLiteralInfo
{
	/// <summary>
	/// Gets the literal value, boxed so it is reachable without naming its type.
	/// </summary>
	object? LiteralValue { get; }
}

/// <summary>
/// A non-generic view of an enum schema, for the JSON Schema exporter.
/// </summary>
/// <remarks>
/// <c>ZodNativeEnum&lt;TEnum&gt;</c> is generic, so the exporter cannot pattern-match it without naming
/// <c>TEnum</c>. The values are boxed and expressed in their JSON wire form (the numeric value of a native
/// enum, matching System.Text.Json's default enum handling).
/// </remarks>
interface IJsonSchemaEnumInfo
{
	/// <summary>
	/// Gets the allowed values, boxed so they are reachable without naming the enum type.
	/// </summary>
	IReadOnlyList<object?> EnumValues { get; }
}

using ZodSharp.Core;

namespace ZodSharp.JsonSchema.NewtonsoftJson;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible")]
#if !NETSTANDARD2_1_OR_GREATER
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
public static class ZExtensions
{
	extension(Z)
	{
		/// <summary>
		/// Creates a ZodSharp schema from a JSON Schema definition.
		/// Enables consuming schemas defined in TypeScript Zod.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
		public static IZodSchema<object, object> FromJsonSchema(JsonSchemaDefinition schema) =>
			FromJsonSchemaParser.Parse(schema);

		/// <summary>
		/// Creates a ZodSharp schema from a JSON Schema string.
		/// Enables consuming schemas defined in TypeScript Zod via JSON files or APIs.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
		[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
		public static IZodSchema<object, object> FromJsonSchema(string jsonSchema) =>
			FromJsonSchemaParser.Parse(jsonSchema);
	}
}

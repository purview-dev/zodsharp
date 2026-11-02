#if NET11_0_OR_GREATER
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.Schemas;

namespace ZodSharp.AspNetCore.ClientValidation;

/// <summary>
/// Configures the ZodSharp client-side validation rules emitted for statically rendered (static SSR) Blazor forms.
/// </summary>
public sealed class ZodClientValidationOptions
{
	readonly Dictionary<Type, JsonSchemaDefinition> _schemas = [];

	/// <summary>
	/// Gets the JSON Schemas client rules are derived from for model types validated by a runtime schema (which has no
	/// attributes to read), keyed by model type.
	/// </summary>
	public IReadOnlyDictionary<Type, JsonSchemaDefinition> Schemas => _schemas;

	/// <summary>
	/// Derives client rules for <typeparamref name="TModel"/> from a runtime object schema whose field names are the
	/// model's property names (see <c>ZodModelSchema.For&lt;TModel&gt;(ZodObject)</c> in Purview.ZodSharp.Blazor).
	/// </summary>
	/// <typeparam name="TModel">The form model type.</typeparam>
	/// <param name="schema">The schema.</param>
	/// <returns>The options, for chaining.</returns>
	public ZodClientValidationOptions AddSchema<TModel>(ZodObject schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		_schemas[typeof(TModel)] = Z.ToJsonSchema(schema);
		return this;
	}

	/// <summary>
	/// Derives client rules for <typeparamref name="TModel"/> from a schema of the model's own type.
	/// </summary>
	/// <typeparam name="TModel">The form model type.</typeparam>
	/// <param name="schema">The schema.</param>
	/// <returns>The options, for chaining.</returns>
	public ZodClientValidationOptions AddSchema<TModel>(IZodSchema<TModel, TModel> schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		_schemas[typeof(TModel)] = Z.ToJsonSchema(schema);
		return this;
	}
}
#endif

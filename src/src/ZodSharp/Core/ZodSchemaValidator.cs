namespace ZodSharp.Core;

/// <summary>
/// Wraps an <see cref="IZodSchema{TOutput, TInput}"/> as an <see cref="IZodSchemaValidator{T}"/> for DI registration.
/// </summary>
public sealed class ZodSchemaValidator<T>(IZodSchema<T, T> schema)
	: IZodSchemaValidator<T>,
		JsonSchema.IJsonSchemaInnerSchema
{
	readonly IZodSchema<T, T> _schema = schema;

	// Lets the JSON Schema exporter see the schema this adapter wraps.
	object JsonSchema.IJsonSchemaInnerSchema.InnerSchema => _schema;

	/// <inheritdoc/>
	public ValidationResult<T> Validate(T value) => _schema.Validate(value);

	/// <inheritdoc/>
	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		_schema.ValidateAsync(value, cancellationToken);
}

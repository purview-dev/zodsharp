using System.Diagnostics.CodeAnalysis;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Wraps a typed <see cref="IZodSchema{T, T}"/> into an <see cref="IZodSchema{Object, Object}"/>
/// by coercing the untyped <see cref="object"/> input via <see cref="SchemaValueCoercion"/>.
/// Used by <see cref="ZodObject.Extend{T}(string, IZodSchema{T, T})"/> and the object/union builders.
/// </summary>
/// <typeparam name="T">The inner schema's type.</typeparam>
public sealed class FieldSchemaWrapper<T>(IZodSchema<T, T> inner) : IZodSchema<object, object>, IOptionalSchema
{
	/// <inheritdoc/>
	public bool IsOptional => inner is IOptionalSchema o && o.IsOptional;

	/// <inheritdoc/>
	public bool ProvidesValueOnMissing => inner is IOptionalSchema o && o.ProvidesValueOnMissing;

	/// <summary>Wraps a typed schema into an untyped schema.</summary>
	[SuppressMessage(
		"Design",
		"CA1000",
		Justification = "Factory method is intentionally placed on the generic wrapper type."
	)]
	public static IZodSchema<object, object> Wrap(IZodSchema<T, T> schema) => new FieldSchemaWrapper<T>(schema);

	/// <inheritdoc/>
	public ValidationResult<object> Validate(object value)
	{
		if (value is null && inner is IAcceptsNull acceptsNull)
			return acceptsNull.ValidateNull();

		if (SchemaValueCoercion.TryCoerce<T>(value, out var typedValue))
		{
			var result = inner.Validate(typedValue);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		return ValidationResult<object>.Failure(
			new ValidationError(
				"invalid_type",
				$"Expected {SchemaValueCoercion.GetTypeDisplayName(typeof(T))}, but got {value?.GetType().Name ?? "null"}",
				[]
			)
		);
	}

	/// <inheritdoc/>
	public ValueTask<ValidationResult<object>> ValidateAsync(
		object value,
		CancellationToken cancellationToken = default
	) => new(Validate(value));
}

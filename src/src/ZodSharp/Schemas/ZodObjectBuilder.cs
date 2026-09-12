using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Builder for creating object schemas with a fluent API.
/// </summary>
public sealed class ZodObjectBuilder
{
	readonly Dictionary<string, IZodSchema<object, object>> _shape = [];

	/// <summary>
	/// Adds a field to the object schema.
	/// </summary>
	/// <typeparam name="T">The field type</typeparam>
	/// <param name="name">The field name</param>
	/// <param name="schema">The field schema</param>
	/// <returns>This builder for method chaining</returns>
	public ZodObjectBuilder Field<T>(string name, IZodSchema<T, T> schema)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentNullException(nameof(name));
		if (schema is null)
			throw new ArgumentNullException(nameof(schema));

		_shape[name] = new SchemaWrapper<T>(schema);
		return this;
	}

	/// <summary>
	/// Builds the object schema.
	/// </summary>
	/// <returns>The built object schema</returns>
	public ZodObject Build() => new(_shape.ToImmutableDictionary());

	/// <typeparam name="T">The inner type</typeparam>
	sealed class SchemaWrapper<T>(IZodSchema<T, T> inner) : IZodSchema<object, object>, IOptionalSchema
	{
		public bool IsOptional => inner is IOptionalSchema o && o.IsOptional;

		public bool ProvidesValueOnMissing => inner is IOptionalSchema o && o.ProvidesValueOnMissing;

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

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

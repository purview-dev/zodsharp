using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Builder for creating discriminated unions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ZodDiscriminatedUnionBuilder class.
/// </remarks>
/// <param name="discriminator">The discriminator field name</param>
public class ZodDiscriminatedUnionBuilder(string discriminator)
{
	readonly Dictionary<string, IZodSchema<object, object>> _options = [];

	/// <summary>
	/// Adds an object-typed option to the discriminated union.
	/// </summary>
	/// <param name="value">The discriminator value</param>
	/// <param name="schema">The schema for this option</param>
	/// <returns>This builder for method chaining</returns>
	public ZodDiscriminatedUnionBuilder Option(string value, IZodSchema<object, object> schema)
	{
		if (schema is null)
			throw new ArgumentNullException(nameof(schema));

		_options[value] = schema;
		return this;
	}

	/// <summary>
	/// Adds a typed option to the discriminated union.
	/// </summary>
	/// <typeparam name="T">The schema input and output type</typeparam>
	/// <param name="value">The discriminator value</param>
	/// <param name="schema">The schema for this option</param>
	/// <returns>This builder for method chaining</returns>
	public ZodDiscriminatedUnionBuilder Option<T>(string value, IZodSchema<T, T> schema)
	{
		if (schema is null)
			throw new ArgumentNullException(nameof(schema));

		_options[value] = new SchemaWrapper<T>(schema);
		return this;
	}

	/// <summary>
	/// Builds the discriminated union schema.
	/// </summary>
	public ZodDiscriminatedUnion Build() => new(discriminator, _options.ToImmutableDictionary());

	sealed class SchemaWrapper<T>(IZodSchema<T, T> inner) : IZodSchema<object, object>, IOptionalSchema
	{
		public bool IsOptional => inner is IOptionalSchema o && o.IsOptional;

		public bool ProvidesValueOnMissing => inner is IOptionalSchema o && o.ProvidesValueOnMissing;

		public ValidationResult<object> Validate(object value)
		{
			if (value is null && inner is IAcceptsNull acceptsNull)
				return acceptsNull.ValidateNull();

			if (!SchemaValueCoercion.TryCoerce<T>(value, out var typedValue))
			{
				return ValidationResult<object>.Failure(
					new ValidationError(
						"invalid_type",
						$"Expected {SchemaValueCoercion.GetTypeDisplayName(typeof(T))}, but got {value?.GetType().Name ?? "null"}",
						[]
					)
				);
			}

			var result = inner.Validate(typedValue);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

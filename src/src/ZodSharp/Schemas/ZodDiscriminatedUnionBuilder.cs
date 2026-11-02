using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
	readonly List<string> _order = [];
	readonly List<ZodDiscriminatedUnion.DiscriminatorReader> _readers = [];

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

		AddOption(value, schema);
		return this;
	}

	/// <summary>
	/// Adds a typed option to the discriminated union.
	/// </summary>
	/// <typeparam name="T">The schema input and output type</typeparam>
	/// <param name="value">The discriminator value</param>
	/// <param name="schema">The schema for this option</param>
	/// <returns>This builder for method chaining</returns>
	/// <remarks>
	/// A CLR value of type <typeparamref name="T"/> (or a type derived from it) has its discriminator read through an
	/// accessor built from <typeparamref name="T"/>'s public properties, which the
	/// <see cref="DynamicallyAccessedMembersAttribute"/> preserves - so a POCO union is trim- and Native AOT-safe.
	/// </remarks>
	public ZodDiscriminatedUnionBuilder Option<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(string value, IZodSchema<T, T> schema)
	{
		if (schema is null)
			throw new ArgumentNullException(nameof(schema));

		AddOption(value, new SchemaWrapper<T>(schema));
		AddReader(typeof(T));
		return this;
	}

	void AddReader([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
	{
		if (type == typeof(object) || _readers.Exists(reader => reader.Type == type))
			return;

		_readers.Add(new(type, ZodDiscriminatedUnion.BuildAccessor(type, discriminator)));
	}

	void AddOption(string value, IZodSchema<object, object> schema)
	{
		if (!_options.ContainsKey(value))
			_order.Add(value);

		_options[value] = schema;
	}

	/// <summary>
	/// Builds the discriminated union schema.
	/// </summary>
	public ZodDiscriminatedUnion Build() =>
		new(discriminator, _options.ToImmutableDictionary(), [.. _order], [.. _readers]);

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
				return ValidationResult<object>.Failure(ZodIssues.InvalidType(ZodTypeNames.Of(typeof(T)), value));
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

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for discriminated unions.
/// More efficient than regular unions when a discriminator field is present.
/// Equivalent to Zod's discriminatedUnion method.
/// </summary>
/// <remarks>
/// Initializes a new instance of the ZodDiscriminatedUnion class.
/// </remarks>
/// <param name="discriminator">The discriminator field name</param>
/// <param name="options">The union options</param>
public class ZodDiscriminatedUnion(
	string discriminator,
	ImmutableDictionary<string, IZodSchema<object, object>> options
) : ZodType<object, object>
{
	// Reflection on the discriminator is unavoidable for POCO inputs, but it only has to happen once per
	// (type, discriminator) pair: the accessor is compiled and cached, so validation hot paths run direct
	// property access with no reflection and no boxing for string discriminators.
	static readonly ConditionalWeakTable<
		Type,
		ConcurrentDictionary<string, Func<object, string?>>
	> DiscriminatorAccessors = new();

	static readonly Func<object, string?> MissingDiscriminatorAccessor = static _ => null;

	/// <summary>
	/// Parses and validates the value using the discriminated union.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<object> ParseInternal(object value)
	{
		if (value == null)
		{
			return ValidationResult<object>.Failure(
				new ValidationError("invalid_type", "Expected object, but got null", [])
			);
		}

		var discriminatorValue = GetDiscriminatorValue(value);

		if (discriminatorValue is null)
		{
			return ValidationResult<object>.Failure(
				new ValidationError("missing_discriminator", $"Discriminator field '{discriminator}' not found", [])
			);
		}

		if (!options.TryGetValue(discriminatorValue, out var schema))
		{
			return ValidationResult<object>.Failure(
				new ValidationError(
					"invalid_discriminator",
					$"Invalid discriminator value '{discriminatorValue}'. Expected one of: {string.Join(", ", options.Keys)}",
					[]
				)
			);
		}

		// Success!
		return schema.Validate(value);
	}

	string? GetDiscriminatorValue(object value)
	{
		if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
		{
			return TryGetDictionaryDiscriminatorValue(readOnlyDictionary);
		}

		if (value is IDictionary<string, object?> dictionary)
		{
			return TryGetDictionaryDiscriminatorValue(dictionary);
		}

		var type = value.GetType();
		var accessors = DiscriminatorAccessors.GetValue(
			type,
			static _ => new ConcurrentDictionary<string, Func<object, string?>>(StringComparer.OrdinalIgnoreCase)
		);
		var accessor = accessors.GetOrAdd(
			discriminator,
			static (name, candidateType) =>
				BuildDiscriminatorAccessor(candidateType, name) ?? MissingDiscriminatorAccessor,
			type
		);

		return accessor(value);
	}

	/// <summary>
	/// Compiles a <see cref="Func{T, TResult}"/> that reads the discriminator property from an instance,
	/// so subsequent validations avoid reflection and, for <see cref="string"/> discriminators, boxing.
	/// </summary>
	/// <param name="type">The runtime type of the value being validated.</param>
	/// <param name="discriminator">The discriminator property name.</param>
	/// <returns>The compiled accessor, or <see langword="null"/> when no readable property exists.</returns>
	static Func<object, string?>? BuildDiscriminatorAccessor(Type type, string discriminator)
	{
		var property = type.GetProperty(
			discriminator,
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
		);

		if (property is null || !property.CanRead || property.GetMethod is not { IsStatic: false })
			return null;

		var parameter = Expression.Parameter(typeof(object), "value");
		Expression access = Expression.Property(Expression.Convert(parameter, type), property);
		Expression boxed = Expression.Convert(access, typeof(object));

		var body = Expression.Condition(
			Expression.NotEqual(boxed, Expression.Constant(null, typeof(object))),
			Expression.Call(boxed, typeof(object).GetMethod(nameof(ToString))!),
			Expression.Constant(null, typeof(string))
		);

		return Expression.Lambda<Func<object, string?>>(body, parameter).Compile();
	}

	string? TryGetDictionaryDiscriminatorValue(IEnumerable<KeyValuePair<string, object?>> dictionary)
	{
		foreach (var (key, dictionaryValue) in dictionary)
		{
			if (string.Equals(key, discriminator, StringComparison.OrdinalIgnoreCase))
			{
				return dictionaryValue?.ToString();
			}
		}

		return null;
	}
}

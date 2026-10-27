using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
	> DiscriminatorAccessors = [];

	static readonly Func<object, string?> MissingDiscriminatorAccessor = static _ => null;

	// The option keys in a deterministic order for messages and parameters: declaration order when built by
	// ZodDiscriminatedUnionBuilder, otherwise ordinal order. The dictionary's own enumeration order is
	// hash-based, so it could differ between processes.
	readonly ImmutableArray<string> _optionKeys = [.. options.Keys.Order(StringComparer.Ordinal)];

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodDiscriminatedUnion"/> class, listing the options in
	/// <paramref name="optionOrder"/> in messages and parameters.
	/// </summary>
	/// <param name="discriminator">The discriminator field name.</param>
	/// <param name="options">The union options.</param>
	/// <param name="optionOrder">The option keys in declaration order.</param>
	internal ZodDiscriminatedUnion(
		string discriminator,
		ImmutableDictionary<string, IZodSchema<object, object>> options,
		ImmutableArray<string> optionOrder
	)
		: this(discriminator, options)
	{
		_optionKeys = optionOrder;
	}

	/// <summary>
	/// Parses and validates the value using the discriminated union.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>A validation result</returns>
	protected override ValidationResult<object> ParseInternal(object value)
	{
		if (value == null)
		{
			return ValidationResult<object>.Failure(ZodIssues.NullValue(ZodTypeNames.Object));
		}

		var discriminatorValue = GetDiscriminatorValue(value);

		if (discriminatorValue is null)
		{
			return ValidationResult<object>.Failure(
				DiscriminatorError(ZodIssueCodes.MissingDiscriminator, ZodMessages.MissingDiscriminator)
			);
		}

		if (!options.TryGetValue(discriminatorValue, out var schema))
		{
			return ValidationResult<object>.Failure(
				DiscriminatorError(ZodIssueCodes.InvalidDiscriminator, ZodMessages.InvalidDiscriminator)
			);
		}

		// Success!
		return schema.Validate(value);
	}

	// Discriminator failures are reported at the discriminator's own path, as Zod does. The received value is
	// not echoed: it is caller input.
	ValidationError DiscriminatorError(string code, string template) =>
		ZodIssues.Formatted(
			code,
			ZodIssueCategories.InvalidUnion,
			template,
			ZodPath.Member(discriminator),
			null,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[ZodIssueParameters.Discriminator] = discriminator,
				[ZodIssueParameters.Options] = _optionKeys,
			}
		);

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
	/// <remarks>
	/// The expression-compiled fast path needs runtime code generation, which Native AOT does not provide.
	/// When it is unavailable this falls back to plain reflection: slower per call, but correct, and still
	/// built once per type because the result is cached by the caller. The discriminator property belongs to
	/// the consumer's own type, so a trimmed application has to preserve it — which it already does if that
	/// type is serialized.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:UnrecognizedReflectionPattern",
		Justification = "The discriminator is a property of the consumer's own validated type, resolved from "
			+ "its runtime type. A trimmed application must preserve that type's properties; it is the "
			+ "consumer's model, not library-internal state."
	)]
	static Func<object, string?>? BuildDiscriminatorAccessor(Type type, string discriminator)
	{
		var property = type.GetProperty(
			discriminator,
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
		);

		if (property is null || !property.CanRead || property.GetMethod is not { IsStatic: false })
			return null;

		// Native AOT has no runtime code generation, so do not build an expression tree there.
		if (!RuntimeFeature.IsDynamicCodeSupported)
			return value => property.GetValue(value)?.ToString();

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

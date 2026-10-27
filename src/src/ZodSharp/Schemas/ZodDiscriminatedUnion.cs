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
	// A POCO's discriminator is read through an accessor built once per runtime type and cached on this union (the
	// readers depend on the options this union was built with): a compiled property read where dynamic code is
	// available, so validation hot paths do no reflection and no boxing for string discriminators.
	readonly ConcurrentDictionary<Type, Func<object, string?>> _accessors = new();

	static readonly Func<object, string?> MissingDiscriminatorAccessor = static _ => null;

	// The option keys in a deterministic order for messages and parameters: declaration order when built by
	// ZodDiscriminatedUnionBuilder, otherwise ordinal order. The dictionary's own enumeration order is
	// hash-based, so it could differ between processes.
	readonly ImmutableArray<string> _optionKeys = [.. options.Keys.Order(StringComparer.Ordinal)];

	readonly ImmutableArray<DiscriminatorReader> _readers = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodDiscriminatedUnion"/> class, listing the options in
	/// <paramref name="optionOrder"/> in messages and parameters.
	/// </summary>
	/// <param name="discriminator">The discriminator field name.</param>
	/// <param name="options">The union options.</param>
	/// <param name="optionOrder">The option keys in declaration order.</param>
	/// <param name="readers">The discriminator readers built for the options' CLR types.</param>
	internal ZodDiscriminatedUnion(
		string discriminator,
		ImmutableDictionary<string, IZodSchema<object, object>> options,
		ImmutableArray<string> optionOrder,
		ImmutableArray<DiscriminatorReader> readers
	)
		: this(discriminator, options)
	{
		_optionKeys = optionOrder;
		_readers = readers;
	}

	/// <summary>
	/// A discriminator reader for one option's CLR type, built from the type the builder was given through
	/// <c>Option&lt;T&gt;</c>, so it needs no reflection over an unknown type.
	/// </summary>
	/// <param name="Type">The option type.</param>
	/// <param name="Read">The accessor, or <see langword="null"/> when the type has no discriminator property.</param>
	internal readonly record struct DiscriminatorReader(Type Type, Func<object, string?>? Read);

	// Exposed to the JSON Schema exporter, which exports the union as oneOf with the discriminator recorded.
	internal string Discriminator => discriminator;

	internal ImmutableDictionary<string, IZodSchema<object, object>> Options => options;

	internal ImmutableArray<string> OptionKeys => _optionKeys;

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

		var accessor = _accessors.GetOrAdd(
			value.GetType(),
			static (type, state) =>
				ResolveAccessor(type, state.Discriminator, state.Readers) ?? MissingDiscriminatorAccessor,
			(Discriminator: discriminator, Readers: _readers)
		);

		return accessor(value);
	}

	/// <summary>
	/// Resolves the discriminator accessor for a POCO: the reader built for the option type it is (or derives from),
	/// otherwise the reflection fallback when <see cref="ZodReflectionFeature.IsSupported"/>.
	/// </summary>
	static Func<object, string?>? ResolveAccessor(
		Type type,
		string discriminator,
		ImmutableArray<DiscriminatorReader> readers
	)
	{
		foreach (var reader in readers)
		{
			if (reader.Type == type && reader.Read is not null)
				return reader.Read;
		}

		foreach (var reader in readers)
		{
			if (reader.Type.IsAssignableFrom(type) && reader.Read is not null)
				return reader.Read;
		}

		return ZodReflectionFeature.IsSupported ? BuildReflectiveAccessor(type, discriminator) : null;
	}

	/// <summary>
	/// Builds the accessor for a type the builder was given through <c>Option&lt;T&gt;</c>. The type's public
	/// properties are preserved by the <see cref="DynamicallyAccessedMembersAttribute"/> on <c>T</c>, so this is
	/// safe under trimming and Native AOT.
	/// </summary>
	/// <param name="type">The option type.</param>
	/// <param name="discriminator">The discriminator property name.</param>
	/// <returns>The accessor, or <see langword="null"/> when the type has no readable discriminator property.</returns>
	internal static Func<object, string?>? BuildAccessor(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
		string discriminator
	) =>
		CreateAccessor(
			type,
			FindProperty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), discriminator)
		);

	/// <summary>
	/// The fallback for a POCO whose type was not supplied through <c>Option&lt;T&gt;</c>: reads the discriminator from
	/// the runtime type by name, which a trimmer cannot follow.
	/// </summary>
#if NET9_0_OR_GREATER
	[RequiresUnreferencedCode(
		"Reads the discriminator from a type that was not supplied through Option<T>. Add each option with Option<T> "
			+ "so its properties are preserved, or validate dictionaries/JSON."
	)]
#else
	// .NET 8's trimming tools ignore feature guards, so on net8.0 this cannot be expressed as a guarded requirement
	// without surfacing a warning for every use of the type. The fallback is reached only for a POCO whose type was
	// not supplied through Option<T>; under trimming its property may be gone, in which case the value is reported as
	// missing its discriminator (the union fails closed). Add options with Option<T> in a trimmed net8.0 application.
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:UnrecognizedReflectionPattern",
		Justification = "net8.0 only: reached for a POCO of a type not supplied through Option<T>; a trimmed property "
			+ "makes the union report a missing discriminator rather than accept the value. Option<T> is the trim-safe path."
	)]
#endif
	static Func<object, string?>? BuildReflectiveAccessor(Type type, string discriminator) =>
		CreateAccessor(
			type,
			FindProperty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), discriminator)
		);

	// The discriminator matches case-insensitively, as Type.GetProperty with BindingFlags.IgnoreCase did; the first
	// declared match wins.
	static PropertyInfo? FindProperty(PropertyInfo[] properties, string discriminator)
	{
		foreach (var property in properties)
		{
			if (string.Equals(property.Name, discriminator, StringComparison.OrdinalIgnoreCase))
				return property;
		}

		return null;
	}

	/// <summary>
	/// Compiles a <see cref="Func{T, TResult}"/> that reads the discriminator property from an instance, so
	/// subsequent validations avoid reflection and, for <see cref="string"/> discriminators, boxing.
	/// </summary>
	/// <remarks>
	/// The expression-compiled fast path needs runtime code generation, which Native AOT does not provide; there the
	/// accessor reads the property through <see cref="PropertyInfo.GetValue(object)"/> instead. Either way it is built
	/// once per type, because the result is cached by the caller.
	/// </remarks>
	static Func<object, string?>? CreateAccessor(Type type, PropertyInfo? property)
	{
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

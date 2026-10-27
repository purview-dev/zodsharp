using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// The Microsoft.Extensions.Validation resolver for ZodSharp: supplies type and parameter validation for every
/// type that has a ZodSharp validator, so minimal APIs, Blazor Server and Blazor WebAssembly validate it with
/// ZodSharp.
/// </summary>
/// <remarks>
/// <para>
/// A type is looked up in <see cref="ZodValidationOptions.Sources"/> first, then in
/// <see cref="IZodSchemaFactory"/>. A type found in neither is left to the next resolver (usually the
/// generated DataAnnotations resolver), except a C# union whose active case a ZodSharp or other validator knows.
/// </para>
/// <para>
/// Results are cached per type, including negative results. Register every validator before the first
/// validation: a validator registered with the factory after a type has been looked up is not seen.
/// </para>
/// </remarks>
public sealed class ZodValidatableInfoResolver : IValidatableInfoResolver
{
	readonly IZodSchemaFactory? _factory;
	readonly ZodValidationOptions _options;
	readonly ConcurrentDictionary<Type, IValidatableTypeInfo?> _types = new();
	readonly ConditionalWeakTable<ParameterInfo, IValidatableParameterInfo?> _parameters = new();
	ValidationOptions? _attachedTo;

	/// <summary>
	/// Creates the resolver.
	/// </summary>
	/// <param name="factory">The schema factory; <see langword="null"/> to use only <see cref="ZodValidationOptions.Sources"/>.</param>
	/// <param name="options">The bridge options; defaults when <see langword="null"/>.</param>
	public ZodValidatableInfoResolver(IZodSchemaFactory? factory, ZodValidationOptions? options = null)
	{
		_factory = factory;
		_options = options ?? new();
	}

	/// <summary>
	/// Gets the bridge options.
	/// </summary>
	public ZodValidationOptions Options => _options;

	/// <summary>
	/// Gets the ZodSharp validator for <paramref name="type"/>, from <see cref="ZodValidationOptions.Sources"/> or
	/// the factory.
	/// </summary>
	/// <param name="type">The validated type.</param>
	/// <param name="validator">The validator, when one is registered.</param>
	/// <returns><see langword="true"/> when a validator is registered.</returns>
	public bool TryGetValidator(Type type, [NotNullWhen(true)] out IZodUntypedValidator? validator)
	{
		ArgumentNullException.ThrowIfNull(type);

		foreach (var source in _options.Sources)
		{
			if (source.TryGetValidator(type, out validator) && validator is not null)
				return true;
		}

		if (_factory is not null && _factory.TryGetUntypedValidator(type, out validator) && validator is not null)
			return true;

		validator = null;
		return false;
	}

	/// <inheritdoc/>
	public bool TryGetValidatableTypeInfo(Type type, [NotNullWhen(true)] out IValidatableTypeInfo? validatableTypeInfo)
	{
		ArgumentNullException.ThrowIfNull(type);

		validatableTypeInfo = _types.GetOrAdd(type, static (key, self) => self.Create(key), this);
		return validatableTypeInfo is not null;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Claims a parameter whose type (a <see cref="Nullable{T}"/> unwrapped) has a ZodSharp validator, or a
	/// collection parameter whose element type has one. <c>[SkipValidation]</c> and <c>[FromServices]</c>
	/// parameters, and primitive types, are never claimed.
	/// </remarks>
	public bool TryGetValidatableParameterInfo(
		ParameterInfo parameterInfo,
		[NotNullWhen(true)] out IValidatableParameterInfo? validatableParameterInfo
	)
	{
		ArgumentNullException.ThrowIfNull(parameterInfo);

		validatableParameterInfo = _parameters.GetValue(parameterInfo, CreateParameter);
		return validatableParameterInfo is not null;
	}

	internal void AttachTo(ValidationOptions options) => _attachedTo = options;

	/// <summary>
	/// Finds the type info the resolvers after this one would supply for <paramref name="type"/>.
	/// </summary>
	internal IValidatableTypeInfo? FindNextTypeInfo(Type type, ValidationOptions options)
	{
		var seen = false;
		foreach (var resolver in options.Resolvers)
		{
			if (ReferenceEquals(resolver, this))
			{
				seen = true;
				continue;
			}

			if (seen && resolver.TryGetValidatableTypeInfo(type, out var info))
				return info;
		}

		return null;
	}

	IValidatableTypeInfo? Create(Type type)
	{
		if (IsNeverValidated(type))
			return null;

		var lookup = Nullable.GetUnderlyingType(type) ?? type;
		if (TryGetValidator(lookup, out var validator) || (lookup != type && TryGetValidator(type, out validator)))
		{
			ZodValidatableTypeInfo zod = new(validator, _options);
			return _options.AlsoRunDataAnnotations ? new ZodCompositeValidatableTypeInfo(zod, this) : zod;
		}

		// A union with no validator of its own: follow the active case, unless a later resolver (the
		// Microsoft.Extensions.Validation generator) already knows the union.
		if (typeof(IUnion).IsAssignableFrom(lookup))
		{
			var attached = _attachedTo;
			if (attached is null || FindNextTypeInfo(lookup, attached) is null)
				return new ZodUnionValidatableTypeInfo(lookup);
		}

		return null;
	}

	IValidatableParameterInfo? CreateParameter(ParameterInfo parameter)
	{
		if (parameter.Name is null || IsExcluded(parameter))
			return null;

		var declared = parameter.ParameterType;
		var type = Nullable.GetUnderlyingType(declared) ?? declared;
		if (IsNeverValidated(type))
			return null;

		var allowsNull = AllowsNull(parameter);
		var isCollection = type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

		if (
			TryGetValidatableTypeInfo(type, out var info)
			&& info is ZodValidatableTypeInfo or ZodCompositeValidatableTypeInfo
		)
			return new ZodValidatableParameterInfo(parameter.Name, declared, allowsNull, isCollection, Unwrap(info));

		// A C# union with no validator of its own: claim it here so the active case is validated even when the
		// application never calls AddValidation() (which is what adds the runtime parameter resolver).
		if (info is ZodUnionValidatableTypeInfo union)
			return new ZodValidatableParameterInfo(parameter.Name, declared, allowsNull, false, null, union);

		if (isCollection && GetElementType(type) is { } elementType && TryGetValidator(elementType, out _))
			return new ZodValidatableParameterInfo(parameter.Name, declared, allowsNull, true, null);

		return null;
	}

	static ZodValidatableTypeInfo Unwrap(IValidatableTypeInfo info) =>
		info as ZodValidatableTypeInfo ?? ((ZodCompositeValidatableTypeInfo)info).Zod;

	static bool IsNeverValidated(Type type) =>
		type.IsPrimitive
		|| type.IsEnum
		|| type.IsPointer
		|| type == typeof(string)
		|| type == typeof(decimal)
		|| type == typeof(object)
		|| type == typeof(DateTime)
		|| type == typeof(DateTimeOffset)
		|| type == typeof(DateOnly)
		|| type == typeof(TimeOnly)
		|| type == typeof(TimeSpan)
		|| type == typeof(Guid)
		|| type == typeof(Uri)
		|| type == typeof(CancellationToken);

	static bool IsExcluded(ParameterInfo parameter)
	{
		if (
			parameter.IsDefined(typeof(SkipValidationAttribute), inherit: false)
			|| parameter.ParameterType.IsDefined(typeof(SkipValidationAttribute), inherit: false)
		)
			return true;

		// [FromServices] / [FromKeyedServices] live in ASP.NET Core, which this package must not reference.
		foreach (var attribute in parameter.GetCustomAttributesData())
		{
			var name = attribute.AttributeType.Name;
			if (name is "FromServicesAttribute" or "FromKeyedServicesAttribute")
				return true;
		}

		return false;
	}

	static bool AllowsNull(ParameterInfo parameter)
	{
		if (parameter.HasDefaultValue || Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
			return true;

		if (parameter.ParameterType.IsValueType)
			return false;

		try
		{
			NullabilityInfoContext nullability = new();
			return nullability.Create(parameter).WriteState == NullabilityState.Nullable;
		}
		catch (InvalidOperationException)
		{
			// Nullability metadata is unavailable (trimmed, or the feature switch is off): treat it as required.
			return false;
		}
	}

	static Type? GetElementType(Type collectionType)
	{
		if (collectionType.IsArray)
			return collectionType.GetElementType();

		if (collectionType.IsGenericType)
		{
			var arguments = collectionType.GetGenericArguments();
			if (arguments.Length == 1)
				return arguments[0];
		}

		return null;
	}
}

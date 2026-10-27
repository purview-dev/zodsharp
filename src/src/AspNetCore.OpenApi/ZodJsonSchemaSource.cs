using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;
using ZodSharp.JsonSchema;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Resolves ZodSharp's own JSON Schema view of a CLR type: the source of truth the transformers describe.
/// </summary>
/// <remarks>
/// <para>
/// A validator registered in <see cref="IZodSchemaFactory"/> wins, because it is what the host actually runs.
/// Otherwise the generated validator recorded by <see cref="ZodSchemaGeneratedAttribute"/> on the type's
/// assembly is used; that path is trim- and AOT-safe for the same reasons as
/// <see cref="ZodSchemaFactoryExtensions.RegisterFromAssembly(IZodSchemaFactory, Assembly)"/>.
/// </para>
/// <para>
/// A type neither source knows, or whose schema exports as the empty (<c>any</c>) schema, resolves to
/// <see langword="null"/> and is left untouched. Results are cached per type.
/// </para>
/// </remarks>
sealed class ZodJsonSchemaSource
{
	readonly ConcurrentDictionary<Type, JsonSchemaDefinition?> _definitions = new();
	readonly ConcurrentDictionary<Assembly, Dictionary<Type, ZodSchemaGeneratedAttribute>> _generated = new();

	public bool TryGet(Type type, IServiceProvider services, out JsonSchemaDefinition definition)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(services);

		var resolved = _definitions.GetOrAdd(type, Resolve, services);
		definition = resolved!;
		return resolved is not null;
	}

	public bool IsKnown(Type type, IServiceProvider services) => TryGet(type, services, out _);

	JsonSchemaDefinition? Resolve(Type type, IServiceProvider services)
	{
		object? source = null;

		if (
			services.GetService<IZodSchemaFactory>() is { } factory
			&& factory.TryGetUntypedValidator(type, out var validator)
			&& validator is not null
		)
		{
			source = validator;
		}

		source ??= CreateGeneratedValidator(type);
		if (source is null)
			return null;

		JsonSchemaDefinition definition;
		try
		{
			definition = ToJsonSchemaConverter.ConvertUntyped(source, new ToJsonSchemaOptions { IncludeSchema = false });
		}
		catch (NotSupportedException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}

		return IsEmpty(definition) ? null : definition;
	}

	object? CreateGeneratedValidator(Type type)
	{
		var generated = _generated.GetOrAdd(type.Assembly, ReadGeneratedValidators);
		return generated.TryGetValue(type, out var attribute) ? Activator.CreateInstance(attribute.ValidatorType) : null;
	}

	// Activator.CreateInstance over ValidatorType is trim- and AOT-safe: the property is annotated
	// PublicParameterlessConstructor and the generator records the type as a typeof, which roots it.
	static Dictionary<Type, ZodSchemaGeneratedAttribute> ReadGeneratedValidators(Assembly assembly)
	{
		Dictionary<Type, ZodSchemaGeneratedAttribute> validators = [];
		foreach (var attribute in assembly.GetCustomAttributes<ZodSchemaGeneratedAttribute>())
			validators.TryAdd(attribute.TargetType, attribute);

		return validators;
	}

	static bool IsEmpty(JsonSchemaDefinition definition) =>
		definition.Type is null
		&& definition.Ref is null
		&& definition.Properties is null
		&& definition.Items is null
		&& definition.AnyOf is null
		&& definition.OneOf is null
		&& definition.AllOf is null
		&& definition.Enum is null
		&& definition.Const is null
		&& definition.Format is null
		&& definition.Pattern is null;
}

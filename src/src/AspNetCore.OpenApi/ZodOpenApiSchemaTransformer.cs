using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ZodSharp.JsonSchema;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Describes the constraints ZodSharp enforces on the schemas ASP.NET Core generates: lengths, bounds,
/// patterns, formats, allowed and denied values, required members, nullability and discriminated unions.
/// </summary>
/// <remarks>
/// <para>
/// The source of truth is ZodSharp's own JSON Schema export for the type — the generated
/// <c>BuildJsonSchema()</c>, or a runtime schema registered in <c>IZodSchemaFactory</c>. A type ZodSharp does
/// not know is left exactly as the platform produced it.
/// </para>
/// <para>
/// When ZodSharp and the platform (DataAnnotations) disagree on a keyword, ZodSharp's value is written,
/// because it is the validator that enforces the request. In Development each override is logged at
/// <see cref="LogLevel.Debug"/>.
/// </para>
/// </remarks>
/// <param name="options">The transformer options.</param>
public sealed class ZodOpenApiSchemaTransformer(ZodOpenApiOptions options) : IOpenApiSchemaTransformer
{
	readonly ZodOpenApiOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	readonly ZodJsonSchemaSource _source = new();

	internal ZodJsonSchemaSource Source => _source;

	/// <inheritdoc/>
	public Task TransformAsync(
		OpenApiSchema schema,
		OpenApiSchemaTransformerContext context,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentNullException.ThrowIfNull(context);

		var services = context.ApplicationServices;

		if (context.JsonPropertyInfo is { } property)
		{
			if (property.DeclaringType is { } declaringType && _source.TryGet(declaringType, services, out var owner))
				TransformProperty(schema, property, context.JsonTypeInfo, owner, CreateContext(services, property));
		}
		else if (TryGetParameterMember(context, services, out var member, out var location))
		{
			ZodOpenApiSchemaMapper.ApplyValue(schema, member, CreateContext(services, location));
		}
		else if (_source.TryGet(context.JsonTypeInfo.Type, services, out var definition))
		{
			TransformType(schema, context.JsonTypeInfo, definition, CreateContext(services, context.JsonTypeInfo.Type));
		}

		return Task.CompletedTask;
	}

	void TransformProperty(
		OpenApiSchema schema,
		JsonPropertyInfo property,
		JsonTypeInfo propertyType,
		JsonSchemaDefinition owner,
		MapperContext context
	)
	{
		if (!TryGetMember(owner, property.Name, out var definition))
			return;

		// An object-typed member is described by its own type schema (and usually becomes a component
		// reference), so only value members are constrained here.
		if (propertyType.Kind == JsonTypeInfoKind.Object || ZodOpenApiSchemaMapper.IsObject(definition))
			return;

		ZodOpenApiSchemaMapper.ApplyValue(schema, definition, context);

		// [Display(Description)] is the one description attribute the platform does not map.
		if (
			schema.Description is null
			&& property.AttributeProvider?.GetCustomAttributes(typeof(DisplayAttribute), inherit: true)
				is [DisplayAttribute { Description: { Length: > 0 } description }, ..]
		)
		{
			schema.Description = description;
		}
	}

	void TransformType(OpenApiSchema schema, JsonTypeInfo typeInfo, JsonSchemaDefinition definition, MapperContext context)
	{
		var inner = ZodOpenApiSchemaMapper.UnwrapNullable(definition, out _);

		if (inner.Properties is null)
		{
			// A scalar (a value object, or a registered schema for a primitive-shaped type) or a union
			// described by ZodSharp at the type level.
			if (inner.OneOf is { Count: > 0 } && schema.AnyOf is { Count: > 0 })
				ApplyDiscriminatedUnion(schema, inner);
			else if (inner.OneOf is null && inner.AnyOf is null)
				ZodOpenApiSchemaMapper.ApplyValue(schema, definition, context);

			return;
		}

		ApplyRequired(schema, typeInfo, inner);

		// System.Text.Json polymorphism ([JsonPolymorphic]/[JsonDerivedType], or a closed hierarchy) already
		// dispatches on a discriminator, so exactly one case matches.
		if (_options.DiscriminatedUnionsAsOneOf && schema.AnyOf is { Count: > 0 } && schema.Discriminator is not null)
		{
			schema.OneOf = schema.AnyOf;
			schema.AnyOf = null;
		}
	}

	void ApplyDiscriminatedUnion(OpenApiSchema schema, JsonSchemaDefinition definition)
	{
		if (!_options.DiscriminatedUnionsAsOneOf)
			return;

		schema.OneOf = schema.AnyOf;
		schema.AnyOf = null;

		if (
			schema.Discriminator is null
			&& definition.GetVendorExtensions().TryGetValue(ZodOpenApiSchemaMapper.DiscriminatorExtensionName, out var name)
			&& name is string propertyName
		)
		{
			schema.Discriminator = new OpenApiDiscriminator { PropertyName = propertyName };
		}
	}

	/// <summary>
	/// Adds the members ZodSharp rejects when absent. A non-nullable value-type member is excluded: an absent
	/// value deserializes to its default, which ZodSharp sees and may accept, so listing it would claim more
	/// than is enforced. Members the platform already lists (C# <c>required</c>, <c>[JsonRequired]</c>) are
	/// kept, because System.Text.Json enforces them.
	/// </summary>
	static void ApplyRequired(OpenApiSchema schema, JsonTypeInfo typeInfo, JsonSchemaDefinition definition)
	{
		if (definition.Required is not { Count: > 0 } required || schema.Properties is null)
			return;

		foreach (var property in typeInfo.Properties)
		{
			if (!schema.Properties.ContainsKey(property.Name) || !ContainsMember(required, property.Name))
				continue;

			var type = property.PropertyType;
			if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
				continue;

			schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
			schema.Required.Add(property.Name);
		}
	}

	/// <summary>
	/// Finds ZodSharp's schema for a member by its wire name. The generator records the camelCase name (the
	/// ASP.NET Core default) unless <c>[JsonPropertyName]</c> says otherwise, so a host with a different naming
	/// policy is matched through the camelCase form as well.
	/// </summary>
	static bool TryGetMember(JsonSchemaDefinition owner, string name, out JsonSchemaDefinition definition)
	{
		definition = null!;
		return owner.Properties is { } properties
			&& (
				properties.TryGetValue(name, out definition!)
				|| properties.TryGetValue(JsonNamingPolicy.CamelCase.ConvertName(name), out definition!)
			);
	}

	static bool ContainsMember(List<string> names, string name) =>
		names.Contains(name, StringComparer.Ordinal)
		|| names.Contains(JsonNamingPolicy.CamelCase.ConvertName(name), StringComparer.Ordinal);
	/// <summary>
	/// Resolves the ZodSharp schema for an <c>[AsParameters]</c> member bound from the route, query or a header:
	/// the member's container is the ZodSharp-known type.
	/// </summary>
	bool TryGetParameterMember(
		OpenApiSchemaTransformerContext context,
		IServiceProvider services,
		out JsonSchemaDefinition member,
		out string location
	)
	{
		member = null!;
		location = string.Empty;

		if (
			context.ParameterDescription?.ModelMetadata is not { ContainerType: { } container, PropertyName: { } name }
			|| !_source.TryGet(container, services, out var owner)
			|| owner.Properties is null
		)
		{
			return false;
		}

		if (!TryGetMember(owner, name, out var definition))
			return false;

		if (ZodOpenApiSchemaMapper.IsObject(definition))
			return false;

		member = definition;
		location = $"parameter '{context.ParameterDescription.Name}' ({container.Name}.{name})";
		return true;
	}

	MapperContext CreateContext(IServiceProvider services, JsonPropertyInfo property) =>
		CreateContext(services, $"property '{property.DeclaringType.Name}.{property.Name}'");

	MapperContext CreateContext(IServiceProvider services, Type type) =>
		CreateContext(services, $"type '{type.Name}'");

	MapperContext CreateContext(IServiceProvider services, string location)
	{
		ILogger? logger = null;
		if (services.GetService<IHostEnvironment>() is { } environment && environment.IsDevelopment())
			logger = services.GetService<ILogger<ZodOpenApiSchemaTransformer>>();

		return new MapperContext(_options, logger, location);
	}
}

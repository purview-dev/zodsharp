using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ZodSharp.JsonSchema;
using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;

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
		else if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Union)
		{
			TransformUnionCases(schema, context.JsonTypeInfo, services);
		}

		return Task.CompletedTask;
	}

	static void TransformProperty(
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

		var clrType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

		// An enum member's schema becomes the enum's shared component, so anything written here would constrain
		// every member of that enum type. Only the enum's own value set — a fact about the type — is written.
		if (clrType.IsEnum)
		{
			if (Enum.GetValuesAsUnderlyingType(clrType).Length == ZodOpenApiSchemaMapper.EnumValueCount(definition))
			{
				var wire = TryGetTypeInfo(propertyType.Options, clrType);
				ZodOpenApiSchemaMapper.ApplyEnum(
					schema,
					definition,
					context,
					wire is null ? null : value => ToWireValue(value, clrType, wire)
				);
			}

			return;
		}

		// System.Text.Json writes a TimeSpan in its constant ("c") format, which the platform already describes
		// with a pattern; it is not an ISO 8601 duration, so ZodSharp's base format for the type is not applied.
		if (clrType == typeof(TimeSpan))
			context.IgnoredFormat = "duration";

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

	static JsonTypeInfo? TryGetTypeInfo(JsonSerializerOptions options, Type type)
	{
		try
		{
			return options.GetTypeInfo(type);
		}
		catch (NotSupportedException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	/// <summary>
	/// Converts one of ZodSharp's enum values (the member's number, or its name when the generator was told enums
	/// are strings) to what System.Text.Json writes for that member with the application's options: a number by
	/// default, or the member name — after the converter's naming policy and any <c>[JsonStringEnumMemberName]</c>
	/// — when a <c>JsonStringEnumConverter</c> applies globally or to the type. The generator cannot see the
	/// application's options, so the document is corrected here. A value that is not a member is left as it is.
	/// </summary>
	static JsonNode? ToWireValue(object? value, Type enumType, JsonTypeInfo typeInfo)
	{
		var member = value switch
		{
			string name when Enum.TryParse(enumType, name, ignoreCase: true, out var parsed) => parsed,
			byte or sbyte or short or ushort or int or uint or long or ulong => Enum.ToObject(enumType, value),
			double number when number == Math.Floor(number) => Enum.ToObject(enumType, (long)number),
			_ => null,
		};

		if (member is null)
			return ZodOpenApiSchemaMapper.ToNode(value);

		try
		{
			return JsonSerializer.SerializeToNode(member, typeInfo) ?? ZodOpenApiSchemaMapper.ToNode(value);
		}
		catch (Exception exception) when (exception is JsonException or NotSupportedException)
		{
			return ZodOpenApiSchemaMapper.ToNode(value);
		}
	}

	/// <summary>
	/// A C# union keeps the platform's <c>anyOf</c> over its case schemas; ZodSharp only constrains each case
	/// it knows, and never restructures the union. The platform builds the case schemas inline with the union,
	/// so they are constrained here rather than through their own transformer invocations.
	/// </summary>
	void TransformUnionCases(OpenApiSchema schema, JsonTypeInfo union, IServiceProvider services)
	{
		if (schema.AnyOf is not { } cases || cases.Count != union.UnionCases.Count)
			return;

		for (var i = 0; i < cases.Count; i++)
		{
			var caseType = union.UnionCases[i].CaseType;
			if (cases[i] is not OpenApiSchema caseSchema || !_source.TryGet(caseType, services, out var definition))
				continue;

			var caseInfo = union.Options.GetTypeInfo(caseType);
			TransformType(caseSchema, caseInfo, definition, CreateContext(services, caseType));

			if (caseSchema.Properties is null)
				continue;

			foreach (var property in caseInfo.Properties)
			{
				if (
					caseSchema.Properties.TryGetValue(property.Name, out var member)
					&& member is OpenApiSchema memberSchema
				)
				{
					TransformProperty(
						memberSchema,
						property,
						union.Options.GetTypeInfo(property.PropertyType),
						definition,
						CreateContext(services, property)
					);
				}
			}
		}
	}

	void TransformType(
		OpenApiSchema schema,
		JsonTypeInfo typeInfo,
		JsonSchemaDefinition definition,
		MapperContext context
	)
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
		if (schema.AnyOf is { Count: > 0 } && schema.Discriminator is not null)
			MarkOneOf(schema);
	}

	void ApplyDiscriminatedUnion(OpenApiSchema schema, JsonSchemaDefinition definition)
	{
		MarkOneOf(schema);

		if (
			_options.DiscriminatedUnionsAsOneOf
			&& schema.Discriminator is null
			&& definition
				.GetVendorExtensions()
				.TryGetValue(ZodOpenApiSchemaMapper.DiscriminatorExtensionName, out var name)
			&& name is string propertyName
		)
		{
			schema.Discriminator = new OpenApiDiscriminator { PropertyName = propertyName };
		}
	}

	/// <summary>
	/// Marks a union for <c>oneOf</c>. The swap itself happens in <see cref="ZodOpenApiDocumentTransformer"/>:
	/// the platform resolves the case references of a polymorphic <c>anyOf</c> after the schema transformers
	/// run, and a case moved out of <c>anyOf</c> here would lose its polymorphic component name.
	/// </summary>
	void MarkOneOf(OpenApiSchema schema)
	{
		if (!_options.DiscriminatedUnionsAsOneOf)
			return;

		schema.Metadata ??= new Dictionary<string, object>(StringComparer.Ordinal);
		schema.Metadata[ZodOpenApiSchemaMapper.OneOfMetadataKey] = true;
	}

	/// <summary>
	/// Adds the members ZodSharp rejects when absent: an absent member deserializes to <see langword="null"/>,
	/// so a reference (or <see cref="Nullable{T}"/>) member ZodSharp does not allow to be null — a non-nullable
	/// reference, <c>[Required]</c>, or a required rule such as <c>[RequiredZod]</c> — is required.
	/// </summary>
	/// <remarks>
	/// A non-nullable value-type member is excluded: an absent value deserializes to its default, which ZodSharp
	/// sees and may accept, so listing it would claim more than is enforced. Members the platform already lists
	/// (C# <c>required</c>, <c>[JsonRequired]</c>) are kept, because System.Text.Json enforces them.
	/// </remarks>
	static void ApplyRequired(OpenApiSchema schema, JsonTypeInfo typeInfo, JsonSchemaDefinition definition)
	{
		if (schema.Properties is null)
			return;

		foreach (var property in typeInfo.Properties)
		{
			if (
				!schema.Properties.ContainsKey(property.Name)
				|| !TryGetMember(definition, property.Name, out var member)
			)
				continue;

			var type = property.PropertyType;
			if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
				continue;

			ZodOpenApiSchemaMapper.UnwrapNullable(member, out var allowsNull);
			if (allowsNull)
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

	MapperContext CreateContext(IServiceProvider services, Type type) => CreateContext(services, $"type '{type.Name}'");

	MapperContext CreateContext(IServiceProvider services, string location)
	{
		ILogger? logger = null;
		if (services.GetService<IHostEnvironment>() is { } environment && environment.IsDevelopment())
			logger = services.GetService<ILogger<ZodOpenApiSchemaTransformer>>();

		return new MapperContext(_options, logger, location);
	}
}

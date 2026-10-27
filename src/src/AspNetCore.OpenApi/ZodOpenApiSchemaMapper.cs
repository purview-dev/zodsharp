using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using ZodSharp.JsonSchema;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Applies ZodSharp's JSON Schema view of a value onto the schema ASP.NET Core generated for it.
/// </summary>
/// <remarks>
/// <para>
/// ZodSharp's exporter already turns every rule into JSON Schema keywords, so this is a keyword-by-keyword
/// merge rather than a second rule interpreter. The conflict policy is the same for every keyword: a keyword
/// ZodSharp has no opinion on is left alone, an identical value is left alone, and a different value is
/// replaced, because ZodSharp is the validator that enforces it.
/// </para>
/// <para>
/// The output is the OpenAPI 3.1/3.2 (JSON Schema 2020-12) dialect: numeric exclusive bounds and
/// <c>type: [x, "null"]</c> for nullability. ZodSharp's OpenAPI 3.0 <c>nullable</c> flag is ignored.
/// </para>
/// </remarks>
static class ZodOpenApiSchemaMapper
{
	public const string UnsupportedExtensionName = "x-zodsharp-unsupported";
	public const string DiscriminatorExtensionName = "x-zod-discriminator";
	public const string DiscriminatorValueExtensionName = "x-zod-discriminatorValue";

	/// <summary>
	/// Applies a value schema (a property, parameter, item or scalar type), including its nullability.
	/// </summary>
	public static void ApplyValue(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		var inner = UnwrapNullable(source, out var nullable);

		if (target.Type is { } type && HasOwnType(inner))
		{
			var updated = nullable ? type | JsonSchemaType.Null : type & ~JsonSchemaType.Null;
			if (updated != type && updated != 0)
			{
				context.Conflict("type", type, updated);
				target.Type = updated;
			}
		}

		ApplyKeywords(target, inner, context);
	}

	/// <summary>
	/// Applies the keywords of <paramref name="source"/> that describe a value (not the object members, which
	/// are applied by their own property schemas).
	/// </summary>
	static void ApplyKeywords(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		ApplyType(target, source, context);
		ApplyFormat(target, source, context);
		ApplyLengths(target, source, context);
		ApplyBounds(target, source, context);
		ApplyPatterns(target, source, context);
		ApplyValues(target, source, context);

		if (source.ContentEncoding is { } encoding)
			target.ContentEncoding = Set("contentEncoding", target.ContentEncoding, encoding, context);

		if (source.UniqueItems is { } unique)
			target.UniqueItems = Set("uniqueItems", target.UniqueItems, unique, context);

		if (source.Description is { } description)
			target.Description = Set("description", target.Description, description, context);

		if (source.Items is { } items && target.Items is OpenApiSchema targetItems && !IsObject(items))
			ApplyValue(targetItems, items, context);

		if (
			source.AdditionalProperties is JsonSchemaDefinition values
			&& target.AdditionalProperties is OpenApiSchema targetValues
			&& !IsObject(values)
		)
		{
			ApplyValue(targetValues, values, context);
		}

		ApplyVendorExtensions(target, source, context);
	}

	static void ApplyType(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		// The only type change ZodSharp makes is narrowing a number to an integer (the Int and SafeInteger
		// rules on a double). Every other type is the CLR type's wire form, which the platform already knows.
		if (
			string.Equals(source.Type, "integer", StringComparison.Ordinal)
			&& target.Type is { } type
			&& type.HasFlag(JsonSchemaType.Number)
		)
		{
			var updated = (type & ~JsonSchemaType.Number) | JsonSchemaType.Integer;
			context.Conflict("type", type, updated);
			target.Type = updated;
		}
	}

	static void ApplyFormat(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		if (source.Format is { } format)
			target.Format = Set("format", target.Format, format, context);
	}

	static void ApplyLengths(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		if (source.MinLength is { } minLength)
			target.MinLength = Set("minLength", target.MinLength, minLength, context);

		if (source.MaxLength is { } maxLength)
			target.MaxLength = Set("maxLength", target.MaxLength, maxLength, context);

		if (source.MinItems is { } minItems)
			target.MinItems = Set("minItems", target.MinItems, minItems, context);

		if (source.MaxItems is { } maxItems)
			target.MaxItems = Set("maxItems", target.MaxItems, maxItems, context);

		if (source.MinProperties is { } minProperties)
			target.MinProperties = Set("minProperties", target.MinProperties, minProperties, context);

		if (source.MaxProperties is { } maxProperties)
			target.MaxProperties = Set("maxProperties", target.MaxProperties, maxProperties, context);
	}

	static void ApplyBounds(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		// A bound is applied as a pair: when ZodSharp states a lower bound it states the whole lower bound, so
		// an inclusive minimum the platform derived from [Range] is cleared when ZodSharp's bound is exclusive
		// (and the other way round).
		if (source.Minimum is not null || source.ExclusiveMinimum is not null)
		{
			target.Minimum = SetNumber("minimum", target.Minimum, source.Minimum, context);
			target.ExclusiveMinimum = SetNumber("exclusiveMinimum",
				target.ExclusiveMinimum,
				source.ExclusiveMinimum,
				context
			);
		}

		if (source.Maximum is not null || source.ExclusiveMaximum is not null)
		{
			target.Maximum = SetNumber("maximum", target.Maximum, source.Maximum, context);
			target.ExclusiveMaximum = SetNumber("exclusiveMaximum",
				target.ExclusiveMaximum,
				source.ExclusiveMaximum,
				context
			);
		}

		if (source.MultipleOf is { } multipleOf)
		{
			var value = (decimal)multipleOf;
			if (target.MultipleOf != value)
			{
				context.Conflict("multipleOf", target.MultipleOf, value);
				target.MultipleOf = value;
			}
		}
	}

	static void ApplyPatterns(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		if (source.Pattern is { } pattern)
		{
			// A Guid's wire form is already described by the platform's format: uuid, and ZodSharp's base Guid
			// pattern is the same constraint spelled differently. Keep the platform's description.
			if (
				!(
					string.Equals(target.Format, "uuid", StringComparison.Ordinal)
					&& string.Equals(pattern, Rules.GuidRule.JsonSchemaPattern, StringComparison.Ordinal)
				)
			)
			{
				target.Pattern = Set("pattern", target.Pattern, pattern, context);
			}
		}

		// Several patterns cannot share the single pattern keyword, so ZodSharp promotes them to allOf
		// branches. The branches are written without a type, so a nullable value still accepts null.
		var branches = source.AllOf?.Where(IsPatternBranch).Select(static branch => branch.Pattern).ToList();
		if (branches is not { Count: > 0 })
			return;

		if (target.Pattern is { } existing)
		{
			context.Conflict("pattern", existing, null);
			target.Pattern = null;
		}

		target.AllOf ??= [];
		foreach (var branch in branches)
		{
			if (
				target.AllOf.Any(schema =>
					schema is OpenApiSchema { } current && string.Equals(current.Pattern, branch, StringComparison.Ordinal)
				)
			)
			{
				continue;
			}

			target.AllOf.Add(new OpenApiSchema { Pattern = branch });
		}
	}

	static void ApplyValues(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		if (source.Enum is { Count: > 0 } values)
		{
			var nodes = ToNodes(values);
			if (!SameNodes(target.Enum, nodes))
			{
				context.Conflict("enum", Render(target.Enum), Render(nodes));
				target.Enum = nodes;
			}
		}

		if (source.Not is { Enum: { Count: > 0 } denied })
		{
			var nodes = ToNodes(denied);
			if (target.Not is not OpenApiSchema { } current || !SameNodes(current.Enum, nodes))
				target.Not = new OpenApiSchema { Enum = nodes };
		}

		if (source.Const is { } constant && ToNode(constant) is { } node)
		{
			// Microsoft.OpenApi models const as a string and writes it as a JSON string, so only a string
			// literal can be expressed faithfully; any other literal is expressed as a single-value enum.
			if (node.GetValueKind() == JsonValueKind.String)
				target.Const = Set("const", target.Const, node.GetValue<string>(), context);
			else if (!SameNodes(target.Enum, [node]))
				target.Enum = [node];
		}
	}

	static void ApplyVendorExtensions(OpenApiSchema target, JsonSchemaDefinition source, MapperContext context)
	{
		if (!context.Options.EmitVendorExtensions)
			return;

		foreach (var (name, value) in source.GetVendorExtensions())
		{
			// Structural markers the transformer itself consumes; they are not descriptions of a value.
			if (name is DiscriminatorExtensionName or DiscriminatorValueExtensionName)
				continue;

			if (ToNode(value) is { } node)
			{
				target.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
				target.Extensions[name] = new JsonNodeExtension(node);
			}
		}

		if (source.UnsupportedRules is { Count: > 0 } unsupported)
		{
			JsonArray names = [];
			foreach (var name in unsupported)
				names.Add((JsonNode?)JsonValue.Create(name));

			target.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
			target.Extensions[UnsupportedExtensionName] = new JsonNodeExtension(names);
		}
	}

	/// <summary>
	/// Removes ZodSharp's nullable wrapper (<c>anyOf: [inner, {type: null}]</c>).
	/// </summary>
	public static JsonSchemaDefinition UnwrapNullable(JsonSchemaDefinition source, out bool nullable)
	{
		if (
			source.AnyOf is { Count: 2 } options
			&& source.Type is null
			&& options.FindIndex(IsNullSchema) is var nullIndex and >= 0
		)
		{
			nullable = true;
			return options[1 - nullIndex];
		}

		nullable = false;
		return source;
	}

	public static bool IsObject(JsonSchemaDefinition source) =>
		UnwrapNullable(source, out _) is { Properties: not null } or { Type: "object", AdditionalProperties: bool };

	static bool HasOwnType(JsonSchemaDefinition source) => source.Type is not null || source.Enum is not null;

	static bool IsNullSchema(JsonSchemaDefinition schema) =>
		string.Equals(schema.Type, "null", StringComparison.Ordinal) && schema.Properties is null;

	static bool IsPatternBranch(JsonSchemaDefinition branch) =>
		branch.Pattern is not null && branch.Properties is null && branch.Items is null && branch.AllOf is null;

	static T Set<T>(string keyword, T current, T value, MapperContext context)
	{
		if (!EqualityComparer<T>.Default.Equals(current, value))
			context.Conflict(keyword, current, value);

		return value;
	}

	static string? SetNumber(string keyword, string? current, double? value, MapperContext context)
	{
		if (value is not { } number)
		{
			if (current is not null)
				context.Conflict(keyword, current, null);

			return null;
		}

		if (
			current is not null
			&& double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out var existing)
			&& existing.Equals(number)
		)
		{
			return current;
		}

		var formatted = FormatNumber(number);
		if (current is not null)
			context.Conflict(keyword, current, formatted);

		return formatted;
	}

	static string FormatNumber(double value) =>
		value == Math.Floor(value) && Math.Abs(value) < 1e15
			? ((long)value).ToString(CultureInfo.InvariantCulture)
			: value.ToString("R", CultureInfo.InvariantCulture);

	static List<JsonNode> ToNodes(IEnumerable<object?> values)
	{
		List<JsonNode> nodes = [];
		foreach (var value in values)
		{
			if (ToNode(value) is { } node)
				nodes.Add(node);
		}

		return nodes;
	}

	/// <summary>
	/// Converts a boxed JSON Schema value to a <see cref="JsonNode"/> without reflection-based serialization,
	/// so the conversion is trim- and AOT-safe. A value with no JSON primitive form is skipped.
	/// </summary>
	public static JsonNode? ToNode(object? value) =>
		value switch
		{
			null => null,
			JsonNode node => node.DeepClone(),
			string text => JsonValue.Create(text),
			bool boolean => JsonValue.Create(boolean),
			char character => JsonValue.Create(character.ToString()),
			byte number => JsonValue.Create(number),
			sbyte number => JsonValue.Create(number),
			short number => JsonValue.Create(number),
			ushort number => JsonValue.Create(number),
			int number => JsonValue.Create(number),
			uint number => JsonValue.Create(number),
			long number => JsonValue.Create(number),
			ulong number => JsonValue.Create(number),
			float number => JsonValue.Create(number),
			double number => JsonValue.Create(number),
			decimal number => JsonValue.Create(number),
			Enum member => JsonValue.Create(member.ToString()),
			_ => null,
		};

	static bool SameNodes(IList<JsonNode>? current, List<JsonNode> values)
	{
		if (current is null || current.Count != values.Count)
			return false;

		for (var i = 0; i < values.Count; i++)
		{
			if (!JsonNode.DeepEquals(current[i], values[i]))
				return false;
		}

		return true;
	}

	static string? Render(IEnumerable<JsonNode>? nodes) =>
		nodes is null ? null : "[" + string.Join(", ", nodes.Select(static node => node.ToJsonString())) + "]";
}

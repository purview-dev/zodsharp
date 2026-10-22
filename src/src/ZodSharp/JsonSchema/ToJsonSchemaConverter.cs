using ZodSharp.Core;
using ZodSharp.Rules;
using ZodSharp.Schemas;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Converts ZodSharp schemas to JSON Schema (Draft 2020-12).
/// </summary>
public static class ToJsonSchemaConverter
{
	const string SchemaUri = "https://json-schema.org/draft/2020-12/schema";

	/// <summary>
	/// Converts a ZodSharp schema to JSON Schema.
	/// </summary>
	/// <typeparam name="T">The schema output type</typeparam>
	/// <param name="schema">The ZodSharp schema to convert</param>
	/// <param name="options">Conversion options</param>
	/// <returns>A JSON Schema definition</returns>
	public static JsonSchemaDefinition Convert<T>(IZodSchema<T, T> schema, ToJsonSchemaOptions? options = null)
	{
		options ??= new ToJsonSchemaOptions();
		ConversionContext context = new() { DateTimeOffsetSerialization = options.DateTimeOffsetSerialization };

		var result = ConvertSchema(schema, context);

		// Add schema metadata
		if (options.IncludeSchema)
		{
			result.Schema = SchemaUri;
		}

		if (options.Id != null)
		{
			result.Id = options.Id;
		}

		if (options.Title != null)
		{
			result.Title = options.Title;
		}

		return result;
	}

	sealed class ConversionContext
	{
		public HashSet<object> Seen { get; } = [];

		public Dictionary<string, JsonSchemaDefinition> Defs { get; } = [];

		public int Counter { get; set; }

		public DateTimeOffsetSerialization DateTimeOffsetSerialization { get; init; }
	}

	static JsonSchemaDefinition ConvertSchema(object schema, ConversionContext ctx)
	{
		// Unwrap adapters that do not affect the exported shape, so the switch below sees the schema that
		// determines it. The object builder wraps every field schema to present it untyped, and before this
		// the wrapper fell through to the generic fallback and exported with no type.
		while (schema is IJsonSchemaInnerSchema wrapper)
			schema = wrapper.InnerSchema;

		// A generated [ZodSchema] validator (or any schema) that describes its own JSON Schema takes over
		// here. This is the bridge that makes an attribute-generated schema exportable without rebuilding
		// its rules by hand with Z.Object()/Z.String().
		if (schema is IJsonSchemaProvider provider)
			return provider.BuildJsonSchema();

		// Handle circular references
		if (ctx.Seen.Contains(schema))
		{
			var defId = $"__schema{ctx.Counter++}";
			return new JsonSchemaDefinition { Ref = $"#/$defs/{defId}" };
		}
		ctx.Seen.Add(schema);

		var result = schema switch
		{
			ZodString zodString => ConvertString(zodString, ctx),
			ZodNumber zodNumber => ConvertNumber(zodNumber, ctx),
			// A DateTime is the RFC 3339 date-time string System.Text.Json writes. The schema is expressed
			// here rather than in ConvertGeneric so a rule that contributes a pattern (for example
			// NonSentinelRule<DateTime>) has the typed schema to apply it to.
			ZodDate zodDate => ConvertDate(zodDate, ctx),
			ZodDateTimeOffset zodDateTimeOffset => ConvertDateTimeOffset(zodDateTimeOffset, ctx),
			ZodBoolean => new JsonSchemaDefinition { Type = "boolean" },
			ZodNull => new JsonSchemaDefinition { Type = "null" },
			ZodObject zodObject => ConvertObject(zodObject, ctx),
			// ZodOptional is handled by the unwrap loop above: in JSON Schema optionality lives in the
			// parent's "required" list, not in the property's own type.
			ZodUnion zodUnion => ConvertUnion(zodUnion, ctx),
			_ => ConvertGeneric(schema, ctx),
		};

		// Add description if available
		if (schema is ZodType<object, object> zodType && zodType.Description != null)
		{
			result.Description = zodType.Description;
		}

		ctx.Seen.Remove(schema);
		return result;
	}

	static JsonSchemaDefinition ConvertString(ZodString schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { Type = "string" };

		// Rules are matched by type and read through internal members. This used to walk
		// BaseType.BaseType to find a field called "_rules" and then read each rule's value by field name —
		// name-based reflection into the library's own private state, which the trimmer cannot see and which
		// fails silently the moment a field is renamed.
		foreach (var rule in schema.AppliedRules)
		{
			switch (rule)
			{
				case MinLengthRule minLength:
					result.MinLength = minLength.MinLength;
					break;

				case MaxLengthRule maxLength:
					result.MaxLength = maxLength.MaxLength;
					break;

				case EmailRule:
					result.Format = "email";
					break;

				case UrlRule:
					result.Format = "uri";
					break;

				case UUIDRule uuid:
					uuid.ApplyTo(result);
					break;

				case GuidRule guid:
					guid.ApplyTo(result);
					break;

				case RegexRule regex:
					JsonSchemaRules.ConstrainPattern(result, regex.Pattern.ToString());
					break;

				default:
					ApplyUnmappedRule(result, rule, ctx.DateTimeOffsetSerialization);
					break;
			}
		}

		// Also check for description
		if (schema.Description != null)
		{
			result.Description = schema.Description;
		}

		return result;
	}

	static JsonSchemaDefinition ConvertDate(ZodDate schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { Type = "string", Format = "date-time" };

		foreach (var rule in schema.AppliedRules)
			ApplyUnmappedRule(result, rule, ctx.DateTimeOffsetSerialization);

		if (schema.Description != null)
			result.Description = schema.Description;

		return result;
	}

	static JsonSchemaDefinition ConvertDateTimeOffset(ZodDateTimeOffset schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { Type = "string", Format = "date-time" };

		foreach (var rule in schema.AppliedRules)
			ApplyUnmappedRule(result, rule, ctx.DateTimeOffsetSerialization);

		if (schema.Description != null)
			result.Description = schema.Description;

		return result;
	}

	static JsonSchemaDefinition ConvertNumber(ZodNumber schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { Type = "number" };

		// ZodNumber closes the generic numeric rules with double, so they can be matched as closed types and
		// read through internal members. This previously matched rule type names with StartsWith (to cope
		// with the `1 arity suffix) and read each value by field name.
		foreach (var rule in schema.AppliedRules)
		{
			switch (rule)
			{
				case MinValueRule<double> minValue:
					result.Minimum = minValue.MinValue;
					break;

				case MaxValueRule<double> maxValue:
					result.Maximum = maxValue.MaxValue;
					break;

				case GreaterThanOrEqualRule<double> greaterThanOrEqual:
					result.Minimum = greaterThanOrEqual.MinValue;
					break;

				case LessThanOrEqualRule<double> lessThanOrEqual:
					result.Maximum = lessThanOrEqual.MaxValue;
					break;

				case IntRule<double>:
					result.Type = "integer";
					break;

				case MultipleOfRule<double> multipleOf:
					result.MultipleOf = multipleOf.Divisor;
					break;

				default:
					ApplyUnmappedRule(result, rule, ctx.DateTimeOffsetSerialization);
					break;
			}
		}

		if (schema.Description != null)
		{
			result.Description = schema.Description;
		}

		return result;
	}

	static JsonSchemaDefinition ConvertObject(ZodObject schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new()
		{
			Type = "object",
			Properties = [],
			Required = [],
			AdditionalProperties = false,
		};

		// ZodObject.Shape is public, so read it directly. This previously reflected a private field named
		// "_shape", which does not exist — the shape is a primary-constructor parameter — so GetField
		// returned null and every exported object schema came back with no properties at all.
		foreach (var (key, propSchema) in schema.Shape)
		{
			result.Properties[key] = ConvertSchema(propSchema, ctx);

			// An optional property is not required. Asked through IOptionalSchema rather than by matching the
			// type name: the builders store each field in a wrapper to present it untyped, so a name check
			// sees the wrapper instead of ZodOptional and marks every optional field required. The wrappers
			// forward IsOptional to the schema they wrap.
			if (propSchema is not IOptionalSchema { IsOptional: true })
				result.Required.Add(key);
		}

		// Remove required array if empty
		if (result.Required.Count == 0)
		{
			result.Required = null;
		}

		return result;
	}

	static JsonSchemaDefinition ConvertUnion(ZodUnion schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { AnyOf = [] };

		// ZodUnion.Options is read directly. This previously reflected a field named "_options" and cast it
		// to an array; the options are a primary-constructor parameter typed IReadOnlyList, so neither the
		// name nor the cast matched and every union exported with an empty "anyOf".
		{
			foreach (var option in schema.Options)
			{
				result.AnyOf.Add(ConvertSchema(option, ctx));
			}
		}

		return result;
	}

	static JsonSchemaDefinition ConvertGeneric(object schema, ConversionContext ctx)
	{
		var schemaType = schema.GetType();
		var typeName = schemaType.Name;

		// Handle enum schemas (ZodNativeEnum<TEnum> / ZodEnum) through their introspection seam rather than
		// by type name: a generic enum cannot be pattern-matched without naming its type.
		if (schema is IJsonSchemaEnumInfo enumInfo)
		{
			return new JsonSchemaDefinition { Enum = [.. enumInfo.EnumValues] };
		}

		// Handle ZodArray<T>. Matched through the internal IJsonSchemaArrayInfo seam rather than by type
		// name plus private-field reflection: the old lookups named fields and rule types that do not
		// exist, so element schemas and bounds never reached the exported schema. See IJsonSchemaArrayInfo.
		if (schema is IJsonSchemaArrayInfo arrayInfo)
		{
			return new JsonSchemaDefinition
			{
				Type = "array",
				Items = ConvertSchema(arrayInfo.ElementSchema, ctx),
				MinItems = arrayInfo.MinItems,
				MaxItems = arrayInfo.MaxItems,
			};
		}

		// Handle ZodRecord<TValue> through the internal IJsonSchemaRecordInfo seam. A dictionary is written as a
		// JSON object, so its exported schema is an object whose additionalProperties is the value schema.
		if (schema is IJsonSchemaRecordInfo recordInfo)
		{
			return new JsonSchemaDefinition
			{
				Type = "object",
				AdditionalProperties = ConvertSchema(recordInfo.ValueSchema, ctx),
			};
		}

		// Handle ZodLiteral<T> through its introspection seam rather than by type name and field name.
		if (schema is IJsonSchemaLiteralInfo literalInfo && literalInfo.LiteralValue is { } value)
		{
			return new JsonSchemaDefinition
			{
				Const = value,
				Type = value switch
				{
					string => "string",
					int or long or double or float => "number",
					bool => "boolean",
					_ => null,
				},
			};
		}

		// Handle ZodNullable<T>. Unlike an optional schema this is not unwrapped: null permission changes
		// the exported shape rather than living in the parent's "required" list.
		if (schema is IJsonSchemaNullableInfo nullableInfo)
		{
			var inner = ConvertSchema(nullableInfo.NullableInnerSchema, ctx);

			return new JsonSchemaDefinition { AnyOf = [inner, new JsonSchemaDefinition { Type = "null" }] };
		}

		// Handle ZodLazy<T>
		if (typeName.StartsWith("ZodLazy", StringComparison.Ordinal))
		{
			// For lazy schemas, we need special handling to avoid infinite recursion
			// Return a $ref placeholder
			var defId = $"__lazy{ctx.Counter++}";
			return new JsonSchemaDefinition { Ref = $"#/$defs/{defId}" };
		}

		// Default: empty schema (any)
		return new JsonSchemaDefinition();
	}

	/// <summary>
	/// Applies a rule the switch did not recognise. A rule that describes its own JSON Schema constraint
	/// contributes it; anything else is recorded as unsupported so the omission is visible rather than
	/// silently weakening the exported schema.
	/// </summary>
	static void ApplyUnmappedRule(
		JsonSchemaDefinition schema,
		object rule,
		DateTimeOffsetSerialization dateTimeOffsetSerialization
	)
	{
		if (rule is NonSentinelRule<DateTimeOffset> dateTimeOffsetRule)
		{
			dateTimeOffsetRule.ApplyTo(schema, dateTimeOffsetSerialization);
			return;
		}

		if (rule is IJsonSchemaRule mapped)
		{
			mapped.ApplyTo(schema);
			return;
		}

		if (rule is IZodRule)
			JsonSchemaRules.MarkUnsupported(schema, rule.GetType().Name);
	}
}

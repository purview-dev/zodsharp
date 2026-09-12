using ZodSharp.Core;
using ZodSharp.Rules;
using ZodSharp.Schemas;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Options for converting ZodSharp schemas to JSON Schema.
/// </summary>
public class ToJsonSchemaOptions
{
	/// <summary>
	/// Whether to include the $schema property in the output.
	/// Default: true
	/// </summary>
	public bool IncludeSchema { get; set; } = true;

	/// <summary>
	/// Custom $id for the schema.
	/// </summary>
	public string? Id { get; set; }

	/// <summary>
	/// Custom title for the schema.
	/// </summary>
	public string? Title { get; set; }
}

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
		ConversionContext context = new();

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
	}

	static JsonSchemaDefinition ConvertSchema(object schema, ConversionContext ctx)
	{
		// Handle circular references
		if (ctx.Seen.Contains(schema))
		{
			var defId = $"__schema{ctx.Counter++}";
			return new JsonSchemaDefinition { Ref = $"#/$defs/{defId}" };
		}
		ctx.Seen.Add(schema);

		var result = schema switch
		{
			ZodString zodString => ConvertString(zodString),
			ZodNumber zodNumber => ConvertNumber(zodNumber),
			ZodBoolean => new JsonSchemaDefinition { Type = "boolean" },
			ZodNull => new JsonSchemaDefinition { Type = "null" },
			ZodObject zodObject => ConvertObject(zodObject, ctx),
			ZodOptional<object> zodOptional => ConvertOptional(zodOptional, ctx),
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0010:Add missing cases")]
	static JsonSchemaDefinition ConvertString(ZodString schema)
	{
		JsonSchemaDefinition result = new() { Type = "string" };

		// Extract rules from the schema using reflection (since rules are private)
		// We'll use the built-in rule inspection if available
		var schemaType = schema.GetType();
		var rulesField = schemaType.BaseType?.BaseType?.GetField(
			"_rules",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		if (rulesField?.GetValue(schema) is System.Collections.IEnumerable rules)
		{
			foreach (var rule in rules)
			{
				var ruleType = rule.GetType();
				var ruleName = ruleType.Name;

				switch (ruleName)
				{
					case nameof(MinLengthRule):
						var minLengthField = ruleType.GetField(
							"_minLength",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
						);
						if (minLengthField?.GetValue(rule) is int minLength)
							result.MinLength = minLength;
						break;

					case nameof(MaxLengthRule):
						var maxLengthField = ruleType.GetField(
							"_maxLength",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
						);
						if (maxLengthField?.GetValue(rule) is int maxLength)
							result.MaxLength = maxLength;
						break;

					case nameof(EmailRule):
						result.Format = "email";
						break;

					case nameof(UrlRule):
						result.Format = "uri";
						break;

					case nameof(UUIDRule):
						result.Format = "uuid";
						break;

					case nameof(RegexRule):
						var patternField = ruleType.GetField(
							"_pattern",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
						);
						if (patternField?.GetValue(rule) is System.Text.RegularExpressions.Regex regex)
							result.Pattern = regex.ToString();
						break;
				}
			}
		}

		// Also check for description
		if (schema.Description != null)
		{
			result.Description = schema.Description;
		}

		return result;
	}

	static JsonSchemaDefinition ConvertNumber(ZodNumber schema)
	{
		JsonSchemaDefinition result = new() { Type = "number" };

		// Extract rules from the schema
		var schemaType = schema.GetType();
		var rulesField = schemaType.BaseType?.BaseType?.GetField(
			"_rules",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		if (rulesField?.GetValue(schema) is System.Collections.IEnumerable rules)
		{
			foreach (var rule in rules)
			{
				var ruleType = rule.GetType();
				var ruleName = ruleType.Name;

				if (ruleName.StartsWith("MinValueRule", StringComparison.Ordinal))
				{
					var minValueField = ruleType.GetField(
						"_minValue",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					);
					if (minValueField?.GetValue(rule) is double minValue)
						result.Minimum = minValue;
				}
				else if (ruleName.StartsWith("MaxValueRule", StringComparison.Ordinal))
				{
					var maxValueField = ruleType.GetField(
						"_maxValue",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					);
					if (maxValueField?.GetValue(rule) is double maxValue)
						result.Maximum = maxValue;
				}
				else if (ruleName == "IntRule")
				{
					result.Type = "integer";
				}
				else if (ruleName == "MultipleOfRule")
				{
					var divisorField = ruleType.GetField(
						"_divisor",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					);
					if (divisorField?.GetValue(rule) is double divisor)
						result.MultipleOf = divisor;
				}
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

		// Get the shape from ZodObject using reflection
		var shapeField = typeof(ZodObject).GetField(
			"_shape",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		if (
			shapeField?.GetValue(schema)
			is System.Collections.Immutable.ImmutableDictionary<string, IZodSchema<object, object>> shape
		)
		{
			foreach (var (key, propSchema) in shape)
			{
				result.Properties[key] = ConvertSchema(propSchema, ctx);

				// Check if the property is optional
				var propSchemaType = propSchema.GetType();
				if (!propSchemaType.Name.StartsWith("ZodOptional", StringComparison.Ordinal))
				{
					result.Required.Add(key);
				}
			}
		}

		// Remove required array if empty
		if (result.Required.Count == 0)
		{
			result.Required = null;
		}

		return result;
	}

	static JsonSchemaDefinition ConvertOptional<T>(ZodOptional<T> schema, ConversionContext ctx)
		where T : class
	{
		// Get inner schema
		var innerField = typeof(ZodOptional<T>).GetField(
			"_innerSchema",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		if (innerField?.GetValue(schema) is object innerSchema)
		{
			return ConvertSchema(innerSchema, ctx);
		}

		// Fallback: return an empty schema (any)
		return new JsonSchemaDefinition();
	}

	static JsonSchemaDefinition ConvertUnion(ZodUnion schema, ConversionContext ctx)
	{
		JsonSchemaDefinition result = new() { AnyOf = [] };

		// Get options from ZodUnion
		var optionsField = typeof(ZodUnion).GetField(
			"_options",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
		);

		if (optionsField?.GetValue(schema) is IZodSchema<object, object>[] options)
		{
			foreach (var option in options)
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

		// Handle ZodArray<T>
		if (typeName.StartsWith("ZodArray", StringComparison.Ordinal))
		{
			JsonSchemaDefinition result = new() { Type = "array" };

			// Get element schema
			var elementField = schemaType.GetField(
				"_elementSchema",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			if (elementField?.GetValue(schema) is object elementSchema)
			{
				result.Items = ConvertSchema(elementSchema, ctx);
			}

			// Get min/max from rules
			var rulesField = schemaType.BaseType?.BaseType?.GetField(
				"_rules",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			if (rulesField?.GetValue(schema) is System.Collections.IEnumerable rules)
			{
				foreach (var rule in rules)
				{
					var ruleType = rule.GetType();
					var ruleName = ruleType.Name;

					if (ruleName == "MinItemsRule")
					{
						var minField = ruleType.GetField(
							"_minItems",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
						);
						if (minField?.GetValue(rule) is int min)
							result.MinItems = min;
					}
					else if (ruleName == "MaxItemsRule")
					{
						var maxField = ruleType.GetField(
							"_maxItems",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
						);
						if (maxField?.GetValue(rule) is int max)
							result.MaxItems = max;
					}
				}
			}

			return result;
		}

		// Handle ZodLiteral<T>
		if (typeName.StartsWith("ZodLiteral", StringComparison.Ordinal))
		{
			var valueField = schemaType.GetField(
				"_value",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			if (valueField?.GetValue(schema) is object value)
			{
				JsonSchemaDefinition result = new()
				{
					Const = value, // Set type based on value type
					Type = value switch
					{
						string => "string",
						int or long or double or float => "number",
						bool => "boolean",
						_ => null,
					},
				};

				return result;
			}
		}

		// Handle ZodNullable<T>
		if (typeName.StartsWith("ZodNullable", StringComparison.Ordinal))
		{
			var innerField = schemaType.GetField(
				"_innerSchema",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
			);

			if (innerField?.GetValue(schema) is object innerSchema)
			{
				var inner = ConvertSchema(innerSchema, ctx);
				return new JsonSchemaDefinition { AnyOf = [inner, new JsonSchemaDefinition { Type = "null" }] };
			}
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
}

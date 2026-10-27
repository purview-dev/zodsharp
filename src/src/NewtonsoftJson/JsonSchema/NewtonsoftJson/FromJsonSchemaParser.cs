using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp.JsonSchema.NewtonsoftJson;

/// <summary>
/// Parses JSON Schema (Draft 2020-12) to ZodSharp schemas.
/// Following the same patterns as Zod's fromJSONSchema.
/// </summary>
public static class FromJsonSchemaParser
{
	/// <summary>
	/// Parses a JSON Schema definition to a ZodSharp schema.
	/// </summary>
	/// <param name="schema">The JSON Schema definition</param>
	/// <returns>A ZodSharp schema that validates according to the JSON Schema</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	public static IZodSchema<object, object> Parse(JsonSchemaDefinition schema)
	{
		if (schema is null)
		{
			throw new ArgumentNullException(nameof(schema));
		}

		ConversionContext ctx = new(schema, schema.Defs ?? schema.Definitions ?? []);

		return ConvertSchema(schema, ctx);
	}

	/// <summary>
	/// Parses a JSON Schema string to a ZodSharp schema.
	/// </summary>
	/// <param name="jsonSchema">The JSON Schema as a string</param>
	/// <returns>A ZodSharp schema that validates according to the JSON Schema</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	public static IZodSchema<object, object> Parse(string jsonSchema)
	{
		var schema = JsonConvert.DeserializeObject<JsonSchemaDefinition>(
			jsonSchema,
			JsonSchemaSerializerOptions.Reading
		);

		return schema == null
			? throw new ArgumentException("Invalid JSON Schema: could not parse JSON", nameof(jsonSchema))
			: Parse(schema);
	}

	sealed class ConversionContext(JsonSchemaDefinition rootSchema, Dictionary<string, JsonSchemaDefinition> defs)
	{
		public JsonSchemaDefinition RootSchema { get; } = rootSchema;

		public Dictionary<string, JsonSchemaDefinition> Defs { get; } = defs;

		public Dictionary<string, IZodSchema<object, object>> Refs { get; } = [];

		public HashSet<string> Processing { get; } = [];
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static IZodSchema<object, object> ConvertSchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		if (schema is null)
		{
			throw new ArgumentException(
				"The JSON Schema definition contains a null sub-schema, which cannot be converted",
				nameof(schema)
			);
		}

		// Convert the base schema (type and its keywords, enum, const, $ref) first, then layer the
		// composition keywords on top. An explicit type makes the composition an intersection with the base;
		// without one the composition stands alone. This mirrors Zod's fromJSONSchema, and is what lets an
		// `allOf` of pattern branches (for example a Guid plus a UUID-version constraint) keep every
		// constraint instead of taking only the first branch.
		var baseSchema = ConvertBaseSchema(schema, ctx);
		var hasExplicitType = schema.Type != null || schema.Enum is { Count: > 0 } || schema.Const != null;

		if (schema.AnyOf is { Count: > 0 } anyOf)
		{
			ZodUnion union = new(anyOf.Select(s => ConvertSchema(s, ctx)).ToArray());
			baseSchema = hasExplicitType ? Z.Intersection(baseSchema, union) : union;
		}

		if (schema.OneOf is { Count: > 0 } oneOf)
		{
			// oneOf is semantically different (exactly one must match) but we approximate with union
			ZodUnion union = new(oneOf.Select(s => ConvertSchema(s, ctx)).ToArray());
			baseSchema = hasExplicitType ? Z.Intersection(baseSchema, union) : union;
		}

		if (schema.AllOf is { Count: > 0 } allOf)
		{
			var result = hasExplicitType ? baseSchema : ConvertSchema(allOf[0], ctx);
			var start = hasExplicitType ? 0 : 1;
			for (var i = start; i < allOf.Count; i++)
				result = Z.Intersection(result, ConvertSchema(allOf[i], ctx));

			baseSchema = result;
		}

		return baseSchema;
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static IZodSchema<object, object> ConvertBaseSchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		// Handle $ref
		if (schema.Ref != null)
		{
			return ResolveRef(schema.Ref, ctx);
		}

		// Handle enum
		if (schema.Enum != null && schema.Enum.Count > 0)
		{
			if (schema.Enum.Count == 1)
			{
				return CreateLiteralSchema(schema.Enum[0]);
			}

			// Create union of literals
			var literals = schema.Enum.Select(CreateLiteralSchema).ToArray();
			return new ZodUnion(literals);
		}

		// Handle const
		if (schema.Const != null)
		{
			return CreateLiteralSchema(schema.Const);
		}

		// Handle type
		if (schema.Type == null)
		{
			// No type specified - any
			return new AnySchema();
		}

		return schema.Type switch
		{
			"string" => ConvertStringSchema(schema),
			"number" => ConvertNumberSchema(schema, isInteger: false),
			"integer" => ConvertNumberSchema(schema, isInteger: true),
			"boolean" => new BooleanSchemaWrapper(),
			"null" => new NullSchemaWrapper(),
			"object" => ConvertObjectSchema(schema, ctx),
			"array" => ConvertArraySchema(schema, ctx),
			_ => new AnySchema(),
		};
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertStringSchema(JsonSchemaDefinition schema)
	{
		var stringSchema = Z.String();

		// Apply constraints
		if (schema.MinLength.HasValue)
		{
			stringSchema = stringSchema.Min(schema.MinLength.Value);
		}

		if (schema.MaxLength.HasValue)
		{
			stringSchema = stringSchema.Max(schema.MaxLength.Value);
		}

		if (schema.Pattern != null)
		{
			// Both the pattern and the values later validated against it come from outside the
			// application, so this is the most exposed regex path in the library. Bound it.
			stringSchema = stringSchema.Regex(
				new Regex(schema.Pattern, RegexOptions.None, Rules.RegexRule.DefaultMatchTimeout)
			);
		}

		// Apply format
		if (schema.Format != null)
		{
			stringSchema = schema.Format switch
			{
				"email" => stringSchema.Email(),
				"uri" or "uri-reference" => stringSchema.Url(),
				"uuid" or "guid" => stringSchema.UUID(),
				_ => stringSchema, // Ignore unknown formats
			};
		}

		return new StringSchemaWrapper(stringSchema);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	static IZodSchema<object, object> ConvertNumberSchema(JsonSchemaDefinition schema, bool isInteger)
	{
		var numberSchema = Z.Number();

		if (isInteger)
		{
			numberSchema = numberSchema.Int();
		}

		// Apply constraints
		if (schema.Minimum.HasValue)
		{
			numberSchema = numberSchema.Min(schema.Minimum.Value);
		}

		if (schema.Maximum.HasValue)
		{
			numberSchema = numberSchema.Max(schema.Maximum.Value);
		}

		if (schema.MultipleOf.HasValue)
		{
			numberSchema = numberSchema.MultipleOf(schema.MultipleOf.Value);
		}

		return new NumberSchemaWrapper(numberSchema);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static IZodSchema<object, object> ConvertObjectSchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		// An object with no declared properties whose additionalProperties is a schema is a record: every
		// member is validated by the same value schema. This is the shape the generated exporter writes for a
		// dictionary property, and matches Zod's z.record(z.string(), V).
		if (
			schema.Properties is not { Count: > 0 }
			&& TryConvertAdditionalProperties(schema.AdditionalProperties, ctx, out var recordValueSchema)
		)
		{
			return new RecordSchemaWrapper(recordValueSchema);
		}

		var builder = Z.Object();
		HashSet<string> requiredSet = [.. schema.Required ?? []];

		if (schema.Properties != null)
		{
			foreach (var (key, propSchema) in schema.Properties)
			{
				var propZodSchema = ConvertSchema(propSchema, ctx);

				if (requiredSet.Contains(key))
				{
					builder = builder.Field(key, propZodSchema);
				}
				else
				{
					// Make optional - wrap in optional
					builder = builder.Field(key, new OptionalSchemaWrapper(propZodSchema));
				}
			}
		}

		return new ObjectSchemaWrapper(builder.Build());
	}

	/// <summary>
	/// Converts the <c>additionalProperties</c> value of an object schema to the record's value schema. A
	/// schema object (or a <c>true</c> wildcard) means the object is a record; <c>false</c> or an absent value
	/// means it is not.
	/// </summary>
	/// <param name="additionalProperties">The raw <see cref="JsonSchemaDefinition.AdditionalProperties"/> value.</param>
	/// <param name="ctx">The conversion context.</param>
	/// <param name="valueSchema">The converted value schema, when the object is a record.</param>
	/// <returns><see langword="true"/> when the object is a record.</returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static bool TryConvertAdditionalProperties(
		object? additionalProperties,
		ConversionContext ctx,
		out IZodSchema<object, object> valueSchema
	)
	{
		switch (additionalProperties)
		{
			case JsonSchemaDefinition definition:
				valueSchema = ConvertSchema(definition, ctx);
				return true;

			// A string-parsed schema keeps additionalProperties as a JObject, because the model declares the
			// member as object.
			case JObject nested:
				var parsed = nested.ToObject<JsonSchemaDefinition>(
					JsonSerializer.Create(JsonSchemaSerializerOptions.Reading)
				);
				valueSchema = parsed is null ? new AnySchema() : ConvertSchema(parsed, ctx);
				return true;

			case true:
				valueSchema = new AnySchema();
				return true;

			default:
				valueSchema = null!;
				return false;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static object? ConvertJToken(JToken token)
	{
		return token.Type switch
		{
			JTokenType.String => token.Value<string>(),
			JTokenType.Integer => token.Value<long>(),
			JTokenType.Float => token.Value<double>(),
			JTokenType.Boolean => token.Value<bool>(),
			JTokenType.Null => null,
			JTokenType.Object => token,
			JTokenType.Array => token,
			_ => token,
		};
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1859:Use concrete types when possible for improved performance"
	)]
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static IZodSchema<object, object> ConvertArraySchema(JsonSchemaDefinition schema, ConversionContext ctx)
	{
		var elementSchema = schema.Items != null ? ConvertSchema(schema.Items, ctx) : new AnySchema();

		return new ArraySchemaWrapper(elementSchema, schema.MinItems, schema.MaxItems);
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
	static IZodSchema<object, object> ResolveRef(string refPath, ConversionContext ctx)
	{
		if (!refPath.StartsWith('#'))
		{
			throw new NotSupportedException(
				$"External $ref '{refPath}' is not supported. Only local references ('#/...', for example '#/$defs/Name') can be resolved; inline or pre-resolve external schemas before importing."
			);
		}

		// Check if already resolved
		if (ctx.Refs.TryGetValue(refPath, out var existing))
		{
			return existing;
		}

		// Check for circular reference
		if (ctx.Processing.Contains(refPath))
		{
			// Return a lazy placeholder
			return new LazySchemaWrapper(() =>
				ctx.Refs.TryGetValue(refPath, out var resolved) ? resolved : new AnySchema()
			);
		}

		ctx.Processing.Add(refPath);

		// Parse the path
		var parts = refPath[1..].Split('/').Where(p => !string.IsNullOrEmpty(p)).ToArray();

		JsonSchemaDefinition? resolved = null;

		if (parts.Length == 0)
		{
			// Root reference
			resolved = ctx.RootSchema;
		}
		else if (parts.Length >= 2 && (parts[0] == "$defs" || parts[0] == "definitions"))
		{
			var defKey = parts[1];
			if (ctx.Defs.TryGetValue(defKey, out var def))
			{
				resolved = def;
			}
		}

		if (resolved == null)
		{
			throw new ArgumentException($"Reference not found: {refPath}");
		}

		var zodSchema = ConvertSchema(resolved, ctx);
		ctx.Refs[refPath] = zodSchema;
		ctx.Processing.Remove(refPath);

		return zodSchema;
	}

	static IZodSchema<object, object> CreateLiteralSchema(object? value)
	{
		// Handle JToken (from Newtonsoft.Json)
		if (value is JToken jToken)
		{
			return CreateLiteralFromJToken(jToken);
		}

		// Handle primitive types
		return value switch
		{
			string s => new LiteralSchemaWrapper<string>(s),
			int i => new LiteralSchemaWrapper<int>(i),
			long l => new LiteralSchemaWrapper<long>(l),
			double d => new LiteralSchemaWrapper<double>(d),
			float f => new LiteralSchemaWrapper<float>(f),
			bool b => new LiteralSchemaWrapper<bool>(b),
			null => new NullSchemaWrapper(),
			_ => new AnySchema(),
		};
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static IZodSchema<object, object> CreateLiteralFromJToken(JToken token)
	{
		return token.Type switch
		{
			JTokenType.String => new LiteralSchemaWrapper<string>(token.Value<string>()!),
			JTokenType.Integer => new LiteralSchemaWrapper<long>(token.Value<long>()),
			JTokenType.Float => new LiteralSchemaWrapper<double>(token.Value<double>()),
			JTokenType.Boolean => new LiteralSchemaWrapper<bool>(token.Value<bool>()),
			JTokenType.Null => new NullSchemaWrapper(),
			_ => new AnySchema(),
		};
	}

	// ========== Errors ==========

	const string InvalidTypeFormat = "Expected {expected}, but got {received}";
	const string InvalidLiteralFormat = "Expected literal value '{expected}', but got {received}";
	const string TooSmallArrayFormat = "Array must have at least {minimum} elements, but got {count}";
	const string TooBigArrayFormat = "Array must have at most {maximum} elements, but got {count}";

	/// <summary>
	/// An <c>invalid_type</c> error naming the expected and received JSON kinds with Zod's type names. Only the
	/// kind of <paramref name="value"/> is reported, never the value.
	/// </summary>
	static ValidationError InvalidType(string expected, object? value) =>
		CreateError(
			"invalid_type",
			InvalidTypeFormat,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[ZodIssueParameters.Expected] = expected,
				[ZodIssueParameters.Received] = Json.JsonKinds.OfValue(value),
			},
			ZodIssueCategories.InvalidType
		);

	/// <summary>
	/// An <c>invalid_literal</c> error. The expected literal comes from the schema; only the kind of the
	/// received value is reported, never the value.
	/// </summary>
	static ValidationError InvalidLiteral<T>(T expected, object? value) =>
		CreateError(
			"invalid_literal",
			InvalidLiteralFormat,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				[ZodIssueParameters.Expected] = string.Create(CultureInfo.InvariantCulture, $"{expected}"),
				[ZodIssueParameters.Received] = Json.JsonKinds.OfValue(value),
			},
			ZodIssueCategories.InvalidValue
		);

	/// <summary>
	/// The <c>minItems</c>/<c>maxItems</c> failure for <paramref name="count"/>, shaped exactly like a runtime
	/// <c>ZodArray</c> length failure (code, origin <c>array</c>, bounds, inclusivity, "elements" wording), or
	/// <see langword="null"/> when the length is acceptable.
	/// </summary>
	static ValidationError? ArrayLengthFailure(int count, int? minItems, int? maxItems)
	{
		var tooSmall = minItems is { } min && count < min;
		if (!tooSmall && !(maxItems is { } max && count > max))
			return null;

		Dictionary<string, object?> parameters = new(StringComparer.Ordinal) { [ZodIssueParameters.Count] = count };
		if (minItems is { } minimum)
			parameters["minimum"] = minimum;
		if (maxItems is { } maximum)
			parameters["maximum"] = maximum;

		var format = tooSmall ? TooSmallArrayFormat : TooBigArrayFormat;
		ValidationError error = new(
			tooSmall ? "too_small" : "too_big",
			ZodMessageFormat.Format(format, parameters, CultureInfo.InvariantCulture),
			path: null,
			parameters: new Dictionary<string, object?>(StringComparer.Ordinal) { [ZodIssueParameters.Count] = count },
			origin: "array",
			minimum: minItems,
			maximum: maxItems,
			inclusive: true,
			category: tooSmall ? ZodIssueCategories.TooSmall : ZodIssueCategories.TooBig
		);

		return error with
		{
			MessageFormat = format,
		};
	}

	static ValidationError CreateError(
		string code,
		string format,
		Dictionary<string, object?> parameters,
		string category
	) =>
		new ValidationError(
			code,
			ZodMessageFormat.Format(format, parameters, CultureInfo.InvariantCulture),
			path: null,
			parameters,
			category: category
		) with
		{
			MessageFormat = format,
		};

	// ========== Wrapper Classes ==========

	sealed class StringSchemaWrapper(ZodString inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value is not string str)
			{
				// Handle JToken
				if (value is JToken jt && jt.Type == JTokenType.String)
				{
					str = jt.Value<string>()!;
				}
				else
				{
					return ValidationResult<object>.Failure(InvalidType("string", value));
				}
			}

			var result = inner.Validate(str);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value!)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class NumberSchemaWrapper(ZodNumber inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			double num;
			if (value is double d)
				num = d;
			else if (value is int i)
				num = i;
			else if (value is long l)
				num = l;
			else if (value is float f)
				num = f;
			else if (value is JToken jt && (jt.Type == JTokenType.Integer || jt.Type == JTokenType.Float))
			{
				num = jt.Value<double>();
			}
			else
			{
				return ValidationResult<object>.Failure(InvalidType("number", value));
			}

			var result = inner.Validate(num);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class BooleanSchemaWrapper : IZodSchema<object, object>
	{
		readonly ZodBoolean _inner = Z.Boolean();

		public ValidationResult<object> Validate(object value)
		{
			bool b;
			if (value is bool boolVal)
			{
				b = boolVal;
			}
			else if (value is JToken jt && jt.Type == JTokenType.Boolean)
			{
				b = jt.Value<bool>();
			}
			else
			{
				return ValidationResult<object>.Failure(InvalidType("boolean", value));
			}

			var result = _inner.Validate(b);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class NullSchemaWrapper : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value != null && !(value is JToken jt && jt.Type == JTokenType.Null))
			{
				return ValidationResult<object>.Failure(InvalidType("null", value));
			}

			// If matches, return success with null
			return ValidationResult<object>.Success(null!);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class ObjectSchemaWrapper(ZodObject inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			Dictionary<string, object?> dict;

			if (value is Dictionary<string, object?> d)
			{
				dict = d;
			}
			else if (value is JObject jo)
			{
				dict = [];
				foreach (var prop in jo.Properties())
				{
					dict[prop.Name] = ConvertJToken(prop.Value);
				}
			}
			else
			{
				return ValidationResult<object>.Failure(InvalidType("object", value));
			}

			var result = inner.Validate(dict);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	/// <summary>
	/// Validates a JSON object as a record: every member is validated by <c>valueSchema</c> and a failure is
	/// reported at the member's key, matching <c>Z.Record</c> and Zod's <c>z.record</c> issue path.
	/// </summary>
	class RecordSchemaWrapper(IZodSchema<object, object> valueSchema) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			Dictionary<string, object?> dict;

			if (value is Dictionary<string, object?> d)
			{
				dict = d;
			}
			else if (value is JObject jo)
			{
				dict = [];
				foreach (var prop in jo.Properties())
					dict[prop.Name] = ConvertJToken(prop.Value);
			}
			else
			{
				return ValidationResult<object>.Failure(InvalidType("object", value));
			}

			Dictionary<string, object?> validated = [];
			List<ValidationError> errors = [];

			foreach (var (key, item) in dict)
			{
				var result = valueSchema.Validate(item!);
				if (!result.IsSuccess)
				{
					foreach (var error in result.Errors)
						errors.Add(error.WithPathPrefix(ZodPathSegment.Key(key)));
				}
				else
				{
					validated[key] = result.Value;
				}
			}

			return errors.Count > 0
				? ValidationResult<object>.Failure(errors)
				: ValidationResult<object>.Success(validated);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class ArraySchemaWrapper(IZodSchema<object, object> elementSchema, int? minItems, int? maxItems)
		: IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			List<object?> items;

			if (value is JArray jArray)
			{
				items = [.. jArray.Cast<object?>()];
			}
			// A JToken is enumerable whatever its kind (a JValue enumerates nothing), so only a JArray is an array.
			else if (value is System.Collections.IEnumerable enumerable and not string and not JToken)
			{
				items = [.. enumerable.Cast<object?>()];
			}
			else
			{
				return ValidationResult<object>.Failure(InvalidType("array", value));
			}

			// Length is checked before any element, exactly as ZodArray does.
			if (ArrayLengthFailure(items.Count, minItems, maxItems) is { } lengthFailure)
				return ValidationResult<object>.Failure(lengthFailure);

			// Validate each item
			List<ValidationError> errors = [];
			List<object?> validatedItems = [];

			for (var i = 0; i < items.Count; i++)
			{
				var itemResult = elementSchema.Validate(items[i]!);
				if (itemResult.IsSuccess)
				{
					validatedItems.Add(itemResult.Value);
				}
				else
				{
					foreach (var error in itemResult.Errors)
						errors.Add(error.WithPathPrefix(ZodPathSegment.At(i)));
				}
			}

			if (errors.Count > 0)
			{
				return ValidationResult<object>.Failure(errors);
			}

			// Return the validated items as a list
			return ValidationResult<object>.Success(validatedItems);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class OptionalSchemaWrapper(IZodSchema<object, object> inner) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value)
		{
			if (value == null)
				return ValidationResult<object>.Success(null!);
			if (value is JToken jt && jt.Type == JTokenType.Null)
				return ValidationResult<object>.Success(null!);

			// Validate using the inner schema
			return inner.Validate(value);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class LazySchemaWrapper(Func<IZodSchema<object, object>> getter) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => getter().Validate(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => getter().ValidateAsync(value, cancellationToken);
	}

	class LiteralSchemaWrapper<T>(T value) : IZodSchema<object, object>
		where T : IEquatable<T>
	{
		readonly T _value = value;

		public ValidationResult<object> Validate(object value)
		{
			T? typedValue = default;
			var matches = false;

			if (value is T t)
			{
				typedValue = t;
				matches = _value.Equals(t);
			}
			else if (value is JToken jt)
			{
				// Handle JToken
#pragma warning disable IDE0072 // Add missing cases
				object? extracted = jt.Type switch
				{
					JTokenType.String => jt.Value<string>(),
					JTokenType.Integer when typeof(T) == typeof(int) => jt.Value<int>(),
					JTokenType.Integer when typeof(T) == typeof(long) => jt.Value<long>(),
					JTokenType.Float when typeof(T) == typeof(double) => jt.Value<double>(),
					JTokenType.Boolean => jt.Value<bool>(),
					_ => null,
				};
#pragma warning restore IDE0072 // Add missing cases

				if (extracted is T ext)
				{
					typedValue = ext;
					matches = _value.Equals(ext);
				}
			}

			if (!matches)
			{
				return ValidationResult<object>.Failure(InvalidLiteral(_value, value));
			}

			// If matches, return success with the typed value
			return ValidationResult<object>.Success(typedValue!);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	class AnySchema : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(ValidationResult<object>.Success(value));
	}
}

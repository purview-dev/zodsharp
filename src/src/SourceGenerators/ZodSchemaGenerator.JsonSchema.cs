using System.Globalization;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	const string JsonSchemaDefinitionTypeName = "global::ZodSharp.JsonSchema.JsonSchemaDefinition";
	const string JsonSchemaRulesTypeName = "global::ZodSharp.JsonSchema.JsonSchemaRules";

	static readonly TypeIdentity JsonSchemaDefinitionIdentity = new("JsonSchemaDefinition", "ZodSharp.JsonSchema");
	static readonly TypeIdentity JsonSchemaProviderIdentity = new("IJsonSchemaProvider", "ZodSharp.JsonSchema");

	/// <summary>
	/// Emits the <c>BuildJsonSchema</c> method on the generated schema class: a complete JSON Schema
	/// (Draft 2020-12) definition built from the same metadata the validator uses. This is the bridge that
	/// lets an attribute-generated schema be exported through <c>Z.ToJsonSchema</c> without rebuilding its
	/// rules by hand.
	/// </summary>
	/// <param name="writer">The writer positioned inside the generated schema class.</param>
	/// <param name="schema">The schema descriptor.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	static void GenerateJsonSchemaMethod(
		CodeWriter writer,
		ZodSchemaDescriptor schema,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		writer
			.XmlSummary(
				$"Builds the JSON Schema (Draft 2020-12) definition for {CodeWriter.XmlSee(schema.TargetType.Name)}.",
				"The definition is produced from the same metadata as Validate, so the two cannot drift apart."
			)
			.XmlReturn("The JSON Schema definition (without document metadata such as $schema/$id).");

		writer.Method(
			new MethodDeclarationOptions(
				"BuildJsonSchema",
				JsonSchemaDefinitionIdentity.AsTypeReference(),
				TypeDeclarationAccessibility.Public
			)
			{
				IsStatic = true,
			},
			body =>
			{
				if (schema.ScalarUnderlyingType is { } underlying)
				{
					EmitScalarJsonSchema(body, schema, underlying);
					return;
				}

				body.Assignment(
					"var",
					"schema",
					$"new {JsonSchemaDefinitionTypeName} {{ Type = \"object\", Properties = new global::System.Collections.Generic.Dictionary<string, {JsonSchemaDefinitionTypeName}>(global::System.StringComparer.Ordinal), Required = new global::System.Collections.Generic.List<string>(), AdditionalProperties = false }}"
				);
				body.NewLine();

				foreach (var property in schema.Properties)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!property.ShouldProcess)
						continue;

					EmitJsonSchemaProperty(body, property.Value, schema);
					body.NewLine();
				}

				body.Return("schema");
			}
		);

		writer.NewLine();
	}

	/// <summary>
	/// Emits the JSON Schema for a scalar value object: the underlying value's schema (a Guid scalar is a
	/// UUID string), with the scalar's own rules applied and a <c>null</c> branch when the underlying value
	/// is nullable.
	/// </summary>
	static void EmitScalarJsonSchema(CodeWriter body, ZodSchemaDescriptor schema, TypeIdentity underlying)
	{
		body.Assignment("var", "inner", BuildBaseSchemaExpression(underlying));

		foreach (var rule in schema.TypeRules)
			body.Line($"{JsonSchemaRulesTypeName}.Apply(inner, {BuildRuleInstanceExpression(rule)});");

		foreach (var property in schema.Properties)
		{
			if (!property.ShouldProcess)
				continue;

			EmitAttributeConstraints(body, property.Value, "inner");
			foreach (var rule in property.Value.CustomRules)
				body.Line($"{JsonSchemaRulesTypeName}.Apply(inner, {BuildRuleInstanceExpression(rule)});");
		}

		body.Return(
			schema.ScalarUnderlyingCanBeNull
				? $"new {JsonSchemaDefinitionTypeName} {{ AnyOf = new() {{ inner, new {JsonSchemaDefinitionTypeName} {{ Type = \"null\" }} }} }}"
				: "inner"
		);
	}

	static void EmitJsonSchemaProperty(CodeWriter body, ZodPropertyDescriptor property, ZodSchemaDescriptor schema)
	{
		var localName = CodeGenHelpers.GetLocalIdentifier(property.Name, "JsonSchema");
		var nameLiteral = GetJsonName(property).StringLiteral();
		var hasRequiredAttribute =
			property.ValidationAttributes.Required.ShouldProcess && property.ValidationAttributes.Required.Value.Exists;
		var isRequired = hasRequiredAttribute || !property.CanBeNull;
		var isNullable = property.CanBeNull && !hasRequiredAttribute;

		body.Assignment("var", localName, BuildPropertySchemaExpression(property, schema));

		EmitAttributeConstraints(body, property, localName);

		foreach (var rule in property.CustomRules)
			body.Line($"{JsonSchemaRulesTypeName}.Apply({localName}, {BuildRuleInstanceExpression(rule)});");

		if (isNullable)
		{
			body.Line(
				$"schema.Properties[{nameLiteral}] = new {JsonSchemaDefinitionTypeName} {{ AnyOf = new() {{ {localName}, new {JsonSchemaDefinitionTypeName} {{ Type = \"null\" }} }} }};"
			);
		}
		else
		{
			body.Line($"schema.Properties[{nameLiteral}] = {localName};");
		}

		if (isRequired)
			body.Line($"schema.Required.Add({nameLiteral});");
	}

	/// <summary>
	/// Builds the C# expression for a property's base schema (its JSON type, nested schema, enum values, or
	/// array items), before any rule or attribute constraint is applied.
	/// </summary>
	static string BuildPropertySchemaExpression(ZodPropertyDescriptor property, ZodSchemaDescriptor schema) =>
		property.ValidationKind switch
		{
			PropertyValidationKind.Complex when property.NestedSchemaType is { } nested =>
				property.NestedSchemaExportsJsonSchema
					? $"{nested.AsTypeReference().RenderFullName}.BuildJsonSchema()"
					: $"new {JsonSchemaDefinitionTypeName}()",
			PropertyValidationKind.Collection => BuildCollectionSchemaExpression(property),
			// A [Flags] enum is not validated against its defined members (any combination is valid), so the
			// schema must not enumerate them either; the wire form is an integer bitmask.
			PropertyValidationKind.Enum when property.IsEnum && property.IsFlagsEnum => Schema("integer"),
			PropertyValidationKind.Enum when property.IsEnum => BuildEnumSchemaExpression(property, schema),
			_ => BuildBaseSchemaExpression(property.PropertyType),
		};

	static string BuildCollectionSchemaExpression(ZodPropertyDescriptor property)
	{
		var items =
			property.NestedSchemaType is { } nested
				? (
					property.NestedSchemaExportsJsonSchema
						? $"{nested.AsTypeReference().RenderFullName}.BuildJsonSchema()"
						: $"new {JsonSchemaDefinitionTypeName}()"
				)
			: property.ElementType is { } element ? BuildBaseSchemaExpression(element)
			: $"new {JsonSchemaDefinitionTypeName}()";

		return $"new {JsonSchemaDefinitionTypeName} {{ Type = \"array\", Items = {items} }}";
	}

	static string BuildEnumSchemaExpression(ZodPropertyDescriptor property, ZodSchemaDescriptor schema)
	{
		// A string-serialized enum (a per-type/per-property JsonStringEnumConverter, or the assembly-level
		// ZodSharpEnumSerialization=String switch) lists the member JSON names rather than their numeric
		// values. A naming policy mirrors the converter's JsonNamingPolicy.
		if (property.EnumSerializedAsString || schema.EnumAsString)
		{
			var names = property.EnumJsonNames.Select(name =>
				EnumJsonResolver.ApplyNamingPolicy(name, schema.EnumNamingPolicy).StringLiteral()
			);
			return $"new {JsonSchemaDefinitionTypeName} {{ Enum = new() {{ {string.Join(", ", names)} }} }}";
		}

		var enumType = property.PropertyType.AsTypeReference().RenderFullName;
		var members =
			property.IgnoredEnumMembers.Count == 0 ? string.Empty : string.Join(", ", property.IgnoredEnumMembers);
		return $"{JsonSchemaRulesTypeName}.EnumSchema<{enumType}>({members})";
	}

	/// <summary>
	/// Maps a property type to the JSON Schema its System.Text.Json wire form implies. A scalar with no
	/// JSON Schema representation (or a type the generator cannot classify) exports as the empty schema
	/// (<c>any</c>) rather than being guessed.
	/// </summary>
	static string BuildBaseSchemaExpression(TypeIdentity type)
	{
		if (type.SpecialType is SpecialType.System_String or SpecialType.System_Char)
			return Schema("string");

		if (type.SpecialType == SpecialType.System_Boolean)
			return Schema("boolean");

		if (IsIntegerType(type.SpecialType))
			return Schema("integer");

		if (IsFloatingType(type.SpecialType))
			return Schema("number");

		if (type.SpecialType == SpecialType.System_Object)
			return $"new {JsonSchemaDefinitionTypeName}()";

		if (IsWellKnown(type, "System", "Guid"))
			return Schema("string", "uuid");

		if (type.SpecialType == SpecialType.System_DateTime || IsWellKnown(type, "System", "DateTimeOffset"))
			return Schema("string", "date-time");

		if (IsWellKnown(type, "System", "DateOnly"))
			return Schema("string", "date");

		if (IsWellKnown(type, "System", "TimeOnly"))
			return Schema("string", "time");

		if (IsWellKnown(type, "System", "TimeSpan"))
			return Schema("string", "duration");

		if (IsWellKnown(type, "System", "Version"))
			return Schema("string");

		return $"new {JsonSchemaDefinitionTypeName}()";
	}

	/// <summary>
	/// The property name as System.Text.Json writes it: an explicit <c>[JsonPropertyName]</c> wins, otherwise
	/// the camelCase form that matches the ASP.NET Core / <c>JsonSerializerDefaults.Web</c> default.
	/// </summary>
	/// <remarks>
	/// A non-camelCase global naming policy configured at runtime is not visible to the generator, so a
	/// consumer using one must adjust the exported names (or declare <c>[JsonPropertyName]</c>).
	/// </remarks>
	static string GetJsonName(ZodPropertyDescriptor property) =>
		property.JsonPropertyName
		?? (
			property.Name.Length == 0 || char.IsLower(property.Name[0])
				? property.Name
				: char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1)
		);

	static string Schema(string type) => $"new {JsonSchemaDefinitionTypeName} {{ Type = \"{type}\" }}";

	static string Schema(string type, string format) =>
		$"new {JsonSchemaDefinitionTypeName} {{ Type = \"{type}\", Format = \"{format}\" }}";

	static bool IsWellKnown(TypeIdentity type, string @namespace, string name) =>
		type.Name == name && type.Namespace == @namespace;

	static bool IsIntegerType(SpecialType specialType) =>
		specialType
			is SpecialType.System_SByte
				or SpecialType.System_Byte
				or SpecialType.System_Int16
				or SpecialType.System_UInt16
				or SpecialType.System_Int32
				or SpecialType.System_UInt32
				or SpecialType.System_Int64
				or SpecialType.System_UInt64;

	static bool IsFloatingType(SpecialType specialType) =>
		specialType is SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

	static string BuildRuleInstanceExpression(CustomRuleDescriptor rule)
	{
		// A scalar rule adapter wraps a rule written against the scalar's underlying value; the wrapped rule
		// owns the JSON Schema meaning, so construct that one. The adapter itself has none.
		var ruleType = (rule.AdaptedFrom ?? rule.RuleType).AsTypeReference().RenderFullName;
		var arguments = rule.Arguments.Count == 0 ? string.Empty : $"({string.Join(", ", rule.Arguments)})";
		return $"new {ruleType}{arguments}";
	}

	static void EmitAttributeConstraints(CodeWriter body, ZodPropertyDescriptor property, string localName)
	{
		var attributes = property.ValidationAttributes;
		EmitLengthConstraints(body, property, attributes, localName);
		EmitFormatConstraints(body, attributes, localName);
		EmitRangeConstraint(body, attributes.Range, localName);
		EmitValueConstraints(body, attributes, localName);
		EmitUnsupportedAttributeConstraints(body, attributes, localName);
	}

	static void EmitLengthConstraints(
		CodeWriter body,
		ZodPropertyDescriptor property,
		ValidationAttributes attributes,
		string localName
	)
	{
		// [MinLength]/[MaxLength] apply to strings and collections; the JSON Schema keyword differs.
		var isCollection = property.ValidationKind == PropertyValidationKind.Collection;
		var minKeyword = isCollection ? "MinItems" : "MinLength";
		var maxKeyword = isCollection ? "MaxItems" : "MaxLength";

		if (attributes.MinLength.ShouldProcess && attributes.MinLength.Value.Exists)
			body.Line(
				$"{localName}.{minKeyword} = {attributes.MinLength.Value.Length.ToString(CultureInfo.InvariantCulture)};"
			);

		if (
			attributes.MaxLength.ShouldProcess
			&& attributes.MaxLength.Value.Exists
			&& attributes.MaxLength.Value.Length >= 0
		)
		{
			body.Line(
				$"{localName}.{maxKeyword} = {attributes.MaxLength.Value.Length.ToString(CultureInfo.InvariantCulture)};"
			);
		}

		if (attributes.StringLength.ShouldProcess && attributes.StringLength.Value.Exists)
		{
			if (attributes.StringLength.Value.MinimumLength > 0)
			{
				body.Line(
					$"{localName}.MinLength = {attributes.StringLength.Value.MinimumLength.ToString(CultureInfo.InvariantCulture)};"
				);
			}

			body.Line(
				$"{localName}.MaxLength = {attributes.StringLength.Value.MaximumLength.ToString(CultureInfo.InvariantCulture)};"
			);
		}

		if (attributes.Length.ShouldProcess && attributes.Length.Value.Exists)
		{
			var isString = property.PropertyType.SpecialType == SpecialType.System_String;
			var minimum = attributes.Length.Value.MinimumLength.ToString(CultureInfo.InvariantCulture);
			var maximum = attributes.Length.Value.MaximumLength.ToString(CultureInfo.InvariantCulture);
			body.Line(isString ? $"{localName}.MinLength = {minimum};" : $"{localName}.MinItems = {minimum};");
			body.Line(isString ? $"{localName}.MaxLength = {maximum};" : $"{localName}.MaxItems = {maximum};");
		}
	}

	static void EmitFormatConstraints(CodeWriter body, ValidationAttributes attributes, string localName)
	{
		if (attributes.EmailAddress.ShouldProcess && attributes.EmailAddress.Value.Exists)
			body.Line($"{localName}.Format = \"email\";");

		if (attributes.Url.ShouldProcess && attributes.Url.Value.Exists)
			body.Line($"{localName}.Format = \"uri\";");

		if (
			attributes.RegularExpression.ShouldProcess
			&& attributes.RegularExpression.Value.Exists
			&& attributes.RegularExpression.Value.Pattern is { Length: > 0 } pattern
		)
		{
			body.Line($"{localName}.Pattern = {pattern.StringLiteral()};");
		}

		if (attributes.Base64String.ShouldProcess && attributes.Base64String.Value.Exists)
			body.Line($"{localName}.ContentEncoding = \"base64\";");
	}

	static void EmitValueConstraints(CodeWriter body, ValidationAttributes attributes, string localName)
	{
		if (attributes.AllowedValues.ShouldProcess && attributes.AllowedValues.Value.Exists)
		{
			var values = RenderJsonValues(attributes.AllowedValues.Value.Values);
			if (values.Count > 0)
				body.Line($"{localName}.Enum = new() {{ {string.Join(", ", values)} }};");
		}

		if (attributes.DeniedValues.ShouldProcess && attributes.DeniedValues.Value.Exists)
		{
			var values = RenderJsonValues(attributes.DeniedValues.Value.Values);
			if (values.Count > 0)
			{
				body.Line(
					$"{localName}.Not = new {JsonSchemaDefinitionTypeName} {{ Enum = new() {{ {string.Join(", ", values)} }} }};"
				);
			}
		}
	}

	static void EmitUnsupportedAttributeConstraints(CodeWriter body, ValidationAttributes attributes, string localName)
	{
		if (attributes.CreditCard.ShouldProcess && attributes.CreditCard.Value.Exists)
			body.Line($"{JsonSchemaRulesTypeName}.MarkUnsupported({localName}, \"CreditCardAttribute\");");

		if (attributes.Phone.ShouldProcess && attributes.Phone.Value.Exists)
			body.Line($"{JsonSchemaRulesTypeName}.MarkUnsupported({localName}, \"PhoneAttribute\");");
	}

	static void EmitRangeConstraint(CodeWriter body, GeneratorResult<RangeAttributeData> range, string localName)
	{
		if (!range.ShouldProcess || !range.Value.Exists)
			return;

		var data = range.Value;
		if (data.Minimum is int minimumInt && data.Maximum is int maximumInt)
		{
			body.Line($"{localName}.Minimum = {minimumInt.ToString(CultureInfo.InvariantCulture)};");
			body.Line($"{localName}.Maximum = {maximumInt.ToString(CultureInfo.InvariantCulture)};");
			return;
		}

		if (data.Minimum is double minimumDouble && data.Maximum is double maximumDouble)
		{
			body.Line($"{localName}.Minimum = {minimumDouble.ToString("R", CultureInfo.InvariantCulture)};");
			body.Line($"{localName}.Maximum = {maximumDouble.ToString("R", CultureInfo.InvariantCulture)};");
		}
	}

	static List<string> RenderJsonValues(EquatableArray<TypedConstant> values)
	{
		List<string> rendered = [with(values.Count)];
		for (var i = 0; i < values.Count; i++)
		{
			if (TryRenderJsonValue(values[i], out var expression))
				rendered.Add(expression);
		}

		return rendered;
	}

	static bool TryRenderJsonValue(TypedConstant constant, out string expression)
	{
		if (constant.IsNull)
		{
			expression = "null";
			return true;
		}

		expression = constant.Value switch
		{
			string text => text.StringLiteral(),
			char character => character.ToString().StringLiteral(),
			bool boolean => boolean ? "true" : "false",
			byte value => value.ToString(CultureInfo.InvariantCulture),
			sbyte value => value.ToString(CultureInfo.InvariantCulture),
			short value => value.ToString(CultureInfo.InvariantCulture),
			ushort value => value.ToString(CultureInfo.InvariantCulture),
			int value => value.ToString(CultureInfo.InvariantCulture),
			uint value => value.ToString(CultureInfo.InvariantCulture),
			long value => value.ToString(CultureInfo.InvariantCulture),
			ulong value => value.ToString(CultureInfo.InvariantCulture),
			float value => value.ToString("R", CultureInfo.InvariantCulture),
			double value => value.ToString("R", CultureInfo.InvariantCulture),
			decimal value => value.ToString(CultureInfo.InvariantCulture),
			_ => string.Empty,
		};

		return expression.Length > 0;
	}
}

using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateCollectionValidations(CodeWriter writer, ZodPropertyDescriptor property, bool recurse)
	{
		var lengthAttr = property.ValidationAttributes.Length;
		var minLengthAttr = property.ValidationAttributes.MinLength;
		var maxLengthAttr = property.ValidationAttributes.MaxLength;
		var lengthAccessor = property.LengthAccessor;

		var hasSizeRules =
			(lengthAttr.ShouldProcess && lengthAttr.Value.Exists)
			|| (minLengthAttr.ShouldProcess && minLengthAttr.Value.Exists)
			|| (maxLengthAttr.ShouldProcess && maxLengthAttr.Value.Exists);

		if (hasSizeRules && lengthAccessor.IsSupported)
			GenerateCollectionSizeValidations(writer, property);

		GenerateCollectionElementValidation(writer, property, recurse);
	}

	static void GenerateCollectionSizeValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var lengthAttr = property.ValidationAttributes.Length;
		var minLengthAttr = property.ValidationAttributes.MinLength;
		var maxLengthAttr = property.ValidationAttributes.MaxLength;
		var lengthAccessor = property.LengthAccessor;

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var propertyLengthName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Length");
		var origin = lengthAccessor.Origin.Surround();
		var singularNoun = property.IsMap ? "entry" : "element";
		var pluralNoun = property.IsMap ? "entries" : "elements";
		var countValues = ValuesExpression(("count", propertyLengthName));

		writer.Assignment("var", propertyValueName, $"value.{propertyName}");
		using (writer.IfBlockScope($"{propertyValueName} is not null"))
		{
			writer.Assignment("var", "propertyValue", propertyValueName);
			writer.Assignment("var", propertyLengthName, lengthAccessor.LengthExpression);

			if (lengthAttr.ShouldProcess && lengthAttr.Value.Exists)
			{
				var length = lengthAttr.Value;
				if (length.MinimumLength >= 0 && length.MaximumLength >= length.MinimumLength)
				{
					// LengthAttribute formats a custom message with (name, MinimumLength, MaximumLength).
					var tooSmall = BuildMessageTemplate(
						length.ValidationAttribute,
						AtLeast(length.MinimumLength, singularNoun, pluralNoun),
						"minimum",
						"maximum"
					);
					var tooBig = BuildMessageTemplate(
						length.ValidationAttribute,
						NoMoreThan(length.MaximumLength, singularNoun, pluralNoun),
						"minimum",
						"maximum"
					);
					var lower = Including(length.MinimumLength);
					var upper = Including(length.MaximumLength);

					using (writer.IfBlockScope($"{propertyLengthName} < {length.MinimumLength}"))
					{
						WriteAddError(
							writer,
							ErrorExpression(
								"\"too_small\"",
								tooSmall,
								PathOf(property),
								MemberOf(property),
								origin: origin,
								category: Category("TooSmall"),
								lowerExpression: lower,
								upperExpression: upper,
								valuesExpression: countValues
							)
						);
					}

#pragma warning disable PSGFR23 // ElseIfScope is not yet available in Purview.SourceGeneratorFramework
					using (writer.OpenBlockScope($"else if ({propertyLengthName} > {length.MaximumLength})"))
#pragma warning restore PSGFR23
					{
						WriteAddError(
							writer,
							ErrorExpression(
								"\"too_big\"",
								tooBig,
								PathOf(property),
								MemberOf(property),
								origin: origin,
								category: Category("TooBig"),
								lowerExpression: lower,
								upperExpression: upper,
								valuesExpression: countValues
							)
						);
					}
				}
			}

			if (minLengthAttr.ShouldProcess && minLengthAttr.Value.Exists && minLengthAttr.Value.Length > 0)
			{
				var minLength = minLengthAttr.Value.Length;
				var message = BuildMessageTemplate(
					minLengthAttr.Value.ValidationAttribute,
					AtLeast(minLength, singularNoun, pluralNoun),
					"minimum"
				);

				using (writer.IfBlockScope($"{propertyLengthName} < {minLength}"))
				{
					WriteAddError(
						writer,
						ErrorExpression(
							"\"too_small\"",
							message,
							PathOf(property),
							MemberOf(property),
							origin: origin,
							category: Category("TooSmall"),
							lowerExpression: Including(minLength),
							valuesExpression: countValues
						)
					);
				}
			}

			if (maxLengthAttr.ShouldProcess && maxLengthAttr.Value.Exists && maxLengthAttr.Value.Length >= 0)
			{
				var maxLength = maxLengthAttr.Value.Length;
				var message = BuildMessageTemplate(
					maxLengthAttr.Value.ValidationAttribute,
					NoMoreThan(maxLength, singularNoun, pluralNoun),
					"maximum"
				);

				using (writer.IfBlockScope($"{propertyLengthName} > {maxLength}"))
				{
					WriteAddError(
						writer,
						ErrorExpression(
							"\"too_big\"",
							message,
							PathOf(property),
							MemberOf(property),
							origin: origin,
							category: Category("TooBig"),
							upperExpression: Including(maxLength),
							valuesExpression: countValues
						)
					);
				}
			}
		}

		writer.NewLine();
	}

	/// <summary>
	/// Whether the collection rejects null elements (or map values), so the element loop runs even when nested
	/// schemas are not recursed into.
	/// </summary>
	static bool RejectsNullItems(ZodPropertyDescriptor property) =>
		property.ElementTypeCanBeNull && property.AllowNulls != true;

	static void GenerateCollectionElementValidation(CodeWriter writer, ZodPropertyDescriptor property, bool recurse)
	{
		var hasNestedSchema = recurse && property.NestedSchemaType is not null;
		var rejectNullItems = RejectsNullItems(property);

		// A value-element collection with no nested schema has nothing to check per element.
		if (!hasNestedSchema && !rejectNullItems)
			return;

		var collectionValueName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Collection");

		writer.Assignment("var", collectionValueName, $"value.{property.Name}");
		using (writer.IfBlockScope($"{collectionValueName} is not null"))
		{
			if (property.IsMap)
				GenerateMapEntryValidation(writer, property, hasNestedSchema, rejectNullItems, collectionValueName);
			else
				GenerateSequenceItemValidation(writer, property, hasNestedSchema, rejectNullItems, collectionValueName);
		}

		writer.NewLine();
	}

	/// <summary>
	/// Emits the per-element loop for a sequence. Nested element validation runs only when the member method is
	/// asked to recurse, so <c>ValidateMember</c> keeps the null-element check (it belongs to the member) without
	/// validating the elements' own members.
	/// </summary>
	static void GenerateSequenceItemValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		bool hasNestedSchema,
		bool rejectNullItems,
		string collectionValueName
	)
	{
		var propertyName = property.Name;
		var itemValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Item");
		var itemResultName = CodeGenHelpers.GetLocalIdentifier(propertyName, "ItemResult");
		var indexName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Index");
		var itemExpression = property.ElementTypeIsNullableValueType ? $"{itemValueName}.Value" : itemValueName;

		writer.Assignment("var", indexName, "0");
		using (writer.ForeachScope($"var {itemValueName} in {collectionValueName}"))
		{
			if (property.ElementTypeCanBeNull)
			{
				using (writer.IfBlockScope($"{itemValueName} is null"))
				{
					if (rejectNullItems)
						WriteNullItemError(writer, property, $"{ZodPathSegmentType}.At({indexName})", "array");

					writer.Line($"{indexName}++;");
					writer.Line("continue;");
				}
			}

			if (hasNestedSchema)
			{
				using (writer.IfBlockScope("recurse"))
				{
					writer.Assignment("var", itemResultName, NestedValidateCall(property, itemExpression));
					using (writer.IfBlockScope($"!{itemResultName}.IsSuccess"))
					{
						writer.MethodCall(
							$"{GeneratedValidation}.AddNested",
							"ref errors",
							itemResultName,
							$"{PathOf(property)}.Append({ZodPathSegmentType}.At({indexName}))"
						);
					}
				}
			}

			writer.Line($"{indexName}++;");
		}
	}

	/// <summary>
	/// Emits the per-value loop for a dictionary. The key is used only in error paths, as a typed key segment
	/// rendered with the invariant culture, matching Zod's <c>z.record</c> issue path.
	/// </summary>
	static void GenerateMapEntryValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		bool hasNestedSchema,
		bool rejectNullItems,
		string collectionValueName
	)
	{
		var propertyName = property.Name;
		var entryName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Entry");
		var itemValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Item");
		var itemResultName = CodeGenHelpers.GetLocalIdentifier(propertyName, "ItemResult");
		var keySegment = $"{ZodPathSegmentType}.Key({entryName}.Key)";
		var itemExpression = property.ElementTypeIsNullableValueType ? $"{itemValueName}.Value" : itemValueName;

		using (writer.ForeachScope($"var {entryName} in {collectionValueName}"))
		{
			writer.Assignment("var", itemValueName, $"{entryName}.Value");

			if (property.ElementTypeCanBeNull)
			{
				using (writer.IfBlockScope($"{itemValueName} is null"))
				{
					if (rejectNullItems)
						WriteNullItemError(writer, property, keySegment, "record");

					writer.Line("continue;");
				}
			}

			if (hasNestedSchema)
			{
				using (writer.IfBlockScope("recurse"))
				{
					writer.Assignment("var", itemResultName, NestedValidateCall(property, itemExpression));
					using (writer.IfBlockScope($"!{itemResultName}.IsSuccess"))
					{
						writer.MethodCall(
							$"{GeneratedValidation}.AddNested",
							"ref errors",
							itemResultName,
							$"{PathOf(property)}.Append({keySegment})"
						);
					}
				}
			}
		}
	}

	/// <summary>
	/// Emits the <c>invalid_type</c> error for a null entry in a collection that rejects null entries, at the
	/// entry's own path (<c>Items[0]</c> or <c>Map[key]</c>), matching Zod's per-entry issue path and the
	/// runtime schemas' null message.
	/// </summary>
	static void WriteNullItemError(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		string segmentExpression,
		string origin
	)
	{
		var expected = property.ElementType is { } elementType
			? $"global::ZodSharp.Core.ZodTypeNames.Of(typeof({elementType.AsTypeReference()}))"
			: "global::ZodSharp.Core.ZodTypeNames.Unknown";

		WriteAddError(
			writer,
			MemberError(
				$"global::ZodSharp.Core.ZodIssues.NullValue({expected}, {PathOf(property)}.Append({segmentExpression}), {origin.Surround()})",
				MemberOf(property)
			)
		);
	}

	/// <summary>
	/// The nested <c>Validate</c> call for a member, threading the depth when the nested schema supports it.
	/// </summary>
	static string NestedValidateCall(ZodPropertyDescriptor property, string valueExpression) =>
		NestedValidateCall(property.NestedSchemaType!.Value, property.NestedSchemaIsModern, valueExpression);

	static string NestedValidateCall(TypeIdentity schemaType, bool isModern, string valueExpression) =>
		isModern
			? schemaType.StaticMember($"ValidateCore({valueExpression}, depth + 1)")
			: schemaType.StaticMember($"Validate({valueExpression})");

	/// <summary>
	/// Adds the member's parameters (<c>displayName</c>, <c>member</c>, <c>declaringType</c>) to an error
	/// built by a shared factory.
	/// </summary>
	static string MemberError(string errorExpression, string memberExpression) =>
		$"{GeneratedValidation}.ForMember({errorExpression}, {memberExpression})";
}

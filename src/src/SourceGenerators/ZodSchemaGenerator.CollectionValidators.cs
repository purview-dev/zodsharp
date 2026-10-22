using System.Globalization;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateCollectionValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var lengthAttr = property.ValidationAttributes.Length;
		var minLengthAttr = property.ValidationAttributes.MinLength;
		var maxLengthAttr = property.ValidationAttributes.MaxLength;

		if (
			(!lengthAttr.ShouldProcess || !lengthAttr.Value.Exists)
			&& (!minLengthAttr.ShouldProcess || !minLengthAttr.Value.Exists)
			&& (!maxLengthAttr.ShouldProcess || !maxLengthAttr.Value.Exists)
		)
		{
			GenerateCollectionElementValidation(writer, property);
			return;
		}

		var lengthAccessor = property.LengthAccessor;
		if (!lengthAccessor.IsSupported)
		{
			GenerateCollectionElementValidation(writer, property);
			return;
		}

		var propertyName = property.Name;
		var displayName = property.DisplayName;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var propertyLengthName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Length");
		var origin = lengthAccessor.Origin;
		var singularNoun = property.IsMap ? "entry" : "element";
		var pluralNoun = property.IsMap ? "entries" : "elements";

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
					var tooSmallMessage = BuildMessageExpression(
						length.ValidationAttribute,
						$"{$"Field '{displayName}' must contain at least ".StringLiteral()} + FormatCount({length.MinimumLength}, {singularNoun.Surround()}, {pluralNoun.Surround()}) + {".".Surround()}",
						displayName.StringLiteral(),
						length.MaximumLength.ToString(CultureInfo.InvariantCulture),
						length.MinimumLength.ToString(CultureInfo.InvariantCulture)
					);
					var tooBigMessage = BuildMessageExpression(
						length.ValidationAttribute,
						$"{$"Field '{displayName}' must contain no more than ".StringLiteral()} + FormatCount({length.MaximumLength}, {singularNoun.Surround()}, {pluralNoun.Surround()}) + {".".Surround()}",
						displayName.StringLiteral(),
						length.MaximumLength.ToString(CultureInfo.InvariantCulture),
						length.MinimumLength.ToString(CultureInfo.InvariantCulture)
					);

					using (writer.IfBlockScope($"{propertyLengthName} < {length.MinimumLength}"))
					{
						WriteValidationError(
							writer,
							"too_small",
							tooSmallMessage,
							CodeGenHelpers.GetPathFieldName(propertyName),
							origin,
							minimum: length.MinimumLength
						);
					}

#pragma warning disable PSGFR23 // ElseIfScope is not yet available in Purview.SourceGeneratorFramework
					using (writer.OpenBlockScope($"else if ({propertyLengthName} > {length.MaximumLength})"))
#pragma warning restore PSGFR23
					{
						WriteValidationError(
							writer,
							"too_big",
							tooBigMessage,
							CodeGenHelpers.GetPathFieldName(propertyName),
							origin,
							maximum: length.MaximumLength
						);
					}
				}
			}

			if (minLengthAttr.ShouldProcess && minLengthAttr.Value.Exists && minLengthAttr.Value.Length > 0)
			{
				var minLength = minLengthAttr.Value.Length;
				var messageExpression = BuildMessageExpression(
					minLengthAttr.Value.ValidationAttribute,
					$"{$"Field '{displayName}' must contain at least ".StringLiteral()} + FormatCount({minLength}, {singularNoun.Surround()}, {pluralNoun.Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					minLength.ToString(CultureInfo.InvariantCulture)
				);

				using (writer.IfBlockScope($"{propertyLengthName} < {minLength}"))
				{
					WriteValidationError(
						writer,
						"too_small",
						messageExpression,
						CodeGenHelpers.GetPathFieldName(propertyName),
						origin,
						minimum: minLength
					);
				}
			}

			if (maxLengthAttr.ShouldProcess && maxLengthAttr.Value.Exists && maxLengthAttr.Value.Length >= 0)
			{
				var maxLength = maxLengthAttr.Value.Length;
				var messageExpression = BuildMessageExpression(
					maxLengthAttr.Value.ValidationAttribute,
					$"{$"Field '{displayName}' must contain no more than ".StringLiteral()} + FormatCount({maxLength}, {singularNoun.Surround()}, {pluralNoun.Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					maxLength.ToString(CultureInfo.InvariantCulture)
				);

				using (writer.IfBlockScope($"{propertyLengthName} > {maxLength}"))
				{
					WriteValidationError(
						writer,
						"too_big",
						messageExpression,
						CodeGenHelpers.GetPathFieldName(propertyName),
						origin,
						maximum: maxLength
					);
				}
			}
		}

		writer.NewLine();
		GenerateCollectionElementValidation(writer, property);
	}
}

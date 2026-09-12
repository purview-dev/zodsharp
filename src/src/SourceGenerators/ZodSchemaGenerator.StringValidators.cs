using System.Globalization;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static void GenerateStringValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		StringLengthValidators(writer, property);

		var emailAttribute = property.ValidationAttributes.EmailAddress;
		if (emailAttribute.ShouldProcess && emailAttribute.Value.Exists)
		{
			var propertyName = property.Name;
			var displayName = property.DisplayName;
			var messageExpression = BuildErrorMessageExpression(
				emailAttribute.Value.ValidationAttributeData,
				"Field '{0}' must be a valid email address.",
				displayName.StringLiteral()
			);

			using (writer.OpenBlockScope())
			{
				var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				using (
					writer.IfBlockScope(
						$"{propertyValueName}.Length != 0 && !global::ZodSharp.Rules.EmailRule.EmailRegex.IsMatch({propertyValueName})"
					)
				)
				{
					WriteValidationError(
						writer,
						"invalid_string",
						messageExpression,
						CodeGenHelpers.GetPathFieldName(propertyName),
						"string"
					);
				}
			}

			writer.NewLine();
		}

		var regularExpressionAttribute = property.ValidationAttributes.RegularExpression;
		if (regularExpressionAttribute.ShouldProcess && regularExpressionAttribute.Value.Exists)
		{
			var propertyName = property.Name;
			var displayName = property.DisplayName;
			var pattern = regularExpressionAttribute.Value.Pattern ?? string.Empty;
			var messageExpression = BuildErrorMessageExpression(
				regularExpressionAttribute.Value.ValidationAttribute,
				"Field '{0}' must match the regular expression '{1}'.",
				displayName.StringLiteral(),
				pattern.StringLiteral()
			);

			using (writer.OpenBlockScope())
			{
				var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				using (
					writer.IfBlockScope(
						$"{propertyValueName}.Length != 0 && !{GetRegexFieldName(propertyName)}.IsMatch({propertyValueName})"
					)
				)
				{
					WriteValidationError(
						writer,
						"invalid_string",
						messageExpression,
						CodeGenHelpers.GetPathFieldName(propertyName),
						"string"
					);
				}
			}

			writer.NewLine();
		}

		GenerateURLValidation(writer, property);
		GeneratePhoneValidation(writer, property);
		GenerateCreditCardValidation(writer, property);
		GenerateBase64StringValidation(writer, property);
	}

	static void GenerateURLValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var urlAttribute = property.ValidationAttributes.Url;
		if (!urlAttribute.ShouldProcess || !urlAttribute.Value.Exists)
			return;

		GenerateStringRuleValidation(
			writer,
			property,
			urlAttribute.Value.ValidationAttribute,
			"global::ZodSharp.Rules.UrlRule",
			"Field '{0}' must be a valid URL.",
			"invalid_string"
		);
	}

	static void GeneratePhoneValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var phoneAttribute = property.ValidationAttributes.Phone;
		if (!phoneAttribute.ShouldProcess || !phoneAttribute.Value.Exists)
			return;

		GenerateStringRuleValidation(
			writer,
			property,
			phoneAttribute.Value.ValidationAttribute,
			"global::ZodSharp.Rules.PhoneRule",
			"Field '{0}' must be a valid phone number.",
			"invalid_string"
		);
	}

	static void GenerateCreditCardValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var creditCardAttribute = property.ValidationAttributes.CreditCard;
		if (!creditCardAttribute.ShouldProcess || !creditCardAttribute.Value.Exists)
			return;

		GenerateStringRuleValidation(
			writer,
			property,
			creditCardAttribute.Value.ValidationAttribute,
			"global::ZodSharp.Rules.CreditCardRule",
			"Field '{0}' must be a valid credit card number.",
			"invalid_string"
		);
	}

	static void GenerateBase64StringValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var base64StringAttribute = property.ValidationAttributes.Base64String;
		if (!base64StringAttribute.ShouldProcess || !base64StringAttribute.Value.Exists)
			return;

		GenerateStringRuleValidation(
			writer,
			property,
			base64StringAttribute.Value.ValidationAttribute,
			"global::ZodSharp.Rules.Base64StringRule",
			"Field '{0}' must be a valid Base64 string.",
			"invalid_string"
		);
	}

	static void GenerateStringRuleValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		ValidationAttributeData validationAttribute,
		string ruleType,
		string defaultFormat,
		string errorCode
	)
	{
		var propertyName = property.Name;
		var displayName = property.DisplayName;
		var messageExpression = BuildErrorMessageExpression(
			validationAttribute,
			defaultFormat,
			displayName.StringLiteral()
		);

		using (writer.OpenBlockScope())
		{
			var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
			writer.Assignment("var", propertyValueName, $"value.{propertyName}");
			using (
				writer.IfBlockScope(
					$"{propertyValueName}.Length != 0 && !new {ruleType}().IsValid({propertyValueName})"
				)
			)
			{
				WriteValidationError(
					writer,
					errorCode,
					messageExpression,
					CodeGenHelpers.GetPathFieldName(propertyName),
					"string"
				);
			}
		}

		writer.NewLine();
	}

	static void StringLengthValidators(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var propertyName = property.Name;
		var displayName = property.DisplayName;
		var propertyPath = CodeGenHelpers.GetPathFieldName(propertyName);
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var propertyLengthName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Length");

		var lengthAttr = property.ValidationAttributes.Length;
		if (lengthAttr.ShouldProcess && lengthAttr.Value.Exists)
		{
			var length = lengthAttr.Value;
			if (length.MinimumLength >= 0 && length.MaximumLength >= length.MinimumLength)
			{
				using (writer.OpenBlockScope())
				{
					var tooSmallMessage = BuildMessageExpression(
						length.ValidationAttribute,
						$"{$"Field '{displayName}' must contain at least ".StringLiteral()} + FormatCount({length.MinimumLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
						displayName.StringLiteral(),
						length.MaximumLength.ToString(CultureInfo.InvariantCulture),
						length.MinimumLength.ToString(CultureInfo.InvariantCulture)
					);
					var tooBigMessage = BuildMessageExpression(
						length.ValidationAttribute,
						$"{$"Field '{displayName}' must contain no more than ".StringLiteral()} + FormatCount({length.MaximumLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
						displayName.StringLiteral(),
						length.MaximumLength.ToString(CultureInfo.InvariantCulture),
						length.MinimumLength.ToString(CultureInfo.InvariantCulture)
					);

					writer.Assignment("var", propertyValueName, $"value.{propertyName}");
					using (writer.IfBlockScope($"({propertyValueName} is not null)"))
					{
						writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");

						writer
							.IfBlock(
								$"{propertyLengthName} < {length.MinimumLength}",
								w =>
									WriteValidationError(
										w,
										"too_small",
										tooSmallMessage,
										propertyPath,
										"string",
										minimum: length.MinimumLength
									)
							)
							.ElseIf(
								$"{propertyLengthName} > {length.MaximumLength}",
								w =>
									WriteValidationError(
										w,
										"too_big",
										tooBigMessage,
										propertyPath,
										"string",
										maximum: length.MaximumLength
									)
							);
					}
				}

				writer.NewLine();
			}
		}

		var stringLengthAttr = property.ValidationAttributes.StringLength;
		if (stringLengthAttr.ShouldProcess && stringLengthAttr.Value.Exists)
		{
			var stringLength = stringLengthAttr.Value;
			using (writer.OpenBlockScope())
			{
				var tooSmallMessage = BuildMessageExpression(
					stringLength.ValidationAttribute,
					$"{$"Field '{displayName}' must contain at least ".StringLiteral()} + FormatCount({stringLength.MinimumLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					stringLength.MaximumLength.ToString(CultureInfo.InvariantCulture),
					stringLength.MinimumLength.ToString(CultureInfo.InvariantCulture)
				);
				var tooBigMessage = BuildMessageExpression(
					stringLength.ValidationAttribute,
					$"{$"Field '{displayName}' must contain no more than ".StringLiteral()} + FormatCount({stringLength.MaximumLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					stringLength.MaximumLength.ToString(CultureInfo.InvariantCulture),
					stringLength.MinimumLength.ToString(CultureInfo.InvariantCulture)
				);

				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
				if (stringLength.MinimumLength > 0)
				{
					using (writer.IfBlockScope($"{propertyLengthName} < {stringLength.MinimumLength}"))
					{
						WriteValidationError(
							writer,
							"too_small",
							tooSmallMessage,
							propertyPath,
							"string",
							minimum: stringLength.MinimumLength
						);
					}
				}

				using (writer.IfBlockScope($"{propertyLengthName} > {stringLength.MaximumLength}"))
				{
					WriteValidationError(
						writer,
						"too_big",
						tooBigMessage,
						propertyPath,
						"string",
						maximum: stringLength.MaximumLength
					);
				}
			}

			writer.NewLine();
		}

		var minLengthAttr = property.ValidationAttributes.MinLength;
		if (minLengthAttr.ShouldProcess && minLengthAttr.Value.Exists && minLengthAttr.Value.Length > 0)
		{
			var minLength = minLengthAttr.Value.Length;
			using (writer.OpenBlockScope())
			{
				var messageExpression = BuildMessageExpression(
					minLengthAttr.Value.ValidationAttribute,
					$"{$"Field '{displayName}' must contain at least ".StringLiteral()} + FormatCount({minLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					minLength.ToString(CultureInfo.InvariantCulture)
				);

				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
				using (writer.IfBlockScope($"{propertyLengthName} < {minLength}"))
				{
					WriteValidationError(
						writer,
						"too_small",
						messageExpression,
						propertyPath,
						"string",
						minimum: minLength
					);
				}
			}

			writer.NewLine();
		}

		var maxLengthAttr = property.ValidationAttributes.MaxLength;
		if (maxLengthAttr.ShouldProcess && maxLengthAttr.Value.Exists && maxLengthAttr.Value.Length >= 0)
		{
			var maxLength = maxLengthAttr.Value.Length;
			using (writer.OpenBlockScope())
			{
				var messageExpression = BuildMessageExpression(
					maxLengthAttr.Value.ValidationAttribute,
					$"{$"Field '{displayName}' must contain no more than ".StringLiteral()} + FormatCount({maxLength}, {"character".Surround()}, {"characters".Surround()}) + {".".Surround()}",
					displayName.StringLiteral(),
					maxLength.ToString(CultureInfo.InvariantCulture)
				);

				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
				using (writer.IfBlockScope($"{propertyLengthName} > {maxLength}"))
				{
					WriteValidationError(
						writer,
						"too_big",
						messageExpression,
						propertyPath,
						"string",
						maximum: maxLength
					);
				}
			}

			writer.NewLine();
		}
	}
}

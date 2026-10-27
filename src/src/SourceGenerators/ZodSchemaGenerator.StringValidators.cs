using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	const string StringOrigin = "\"string\"";

	static void GenerateStringValidations(CodeWriter writer, ZodPropertyDescriptor property)
	{
		StringLengthValidators(writer, property);

		var emailAttribute = property.ValidationAttributes.EmailAddress;
		if (emailAttribute.ShouldProcess && emailAttribute.Value.Exists)
		{
			GenerateFormatValidation(
				writer,
				property,
				emailAttribute.Value.ValidationAttributeData,
				"Field '{displayName}' must be a valid email address.",
				valueName => $"!global::ZodSharp.Rules.EmailRule.EmailRegex.IsMatch({valueName})",
				"email"
			);
		}

		var regularExpressionAttribute = property.ValidationAttributes.RegularExpression;
		if (regularExpressionAttribute.ShouldProcess && regularExpressionAttribute.Value.Exists)
		{
			var pattern = regularExpressionAttribute.Value.Pattern ?? string.Empty;
			GenerateFormatValidation(
				writer,
				property,
				regularExpressionAttribute.Value.ValidationAttribute,
				"Field '{displayName}' must match the regular expression '{pattern}'.",
				valueName => $"!{GetRegexFieldName(property.Name)}.IsMatch({valueName})",
				"regex",
				[("pattern", pattern.StringLiteral())],
				"pattern"
			);
		}

		GenerateStringRuleValidation(
			writer,
			property,
			property.ValidationAttributes.Url,
			"global::ZodSharp.Rules.UrlRule",
			"Field '{displayName}' must be a valid URL.",
			"url"
		);
		GenerateStringRuleValidation(
			writer,
			property,
			property.ValidationAttributes.Phone,
			"global::ZodSharp.Rules.PhoneRule",
			"Field '{displayName}' must be a valid phone number.",
			"phone"
		);
		GenerateStringRuleValidation(
			writer,
			property,
			property.ValidationAttributes.CreditCard,
			"global::ZodSharp.Rules.CreditCardRule",
			"Field '{displayName}' must be a valid credit card number.",
			"credit_card"
		);
		GenerateStringRuleValidation(
			writer,
			property,
			property.ValidationAttributes.Base64String,
			"global::ZodSharp.Rules.Base64StringRule",
			"Field '{displayName}' must be a valid Base64 string.",
			"base64"
		);
	}

	static void GenerateStringRuleValidation<TAttribute>(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		GeneratorResult<TAttribute> attribute,
		string ruleType,
		string defaultFormat,
		string format
	)
		where TAttribute : struct
	{
		if (!attribute.ShouldProcess || !TryGetValidationAttribute(attribute.Value, out var data))
			return;

		GenerateFormatValidation(
			writer,
			property,
			data,
			defaultFormat,
			valueName => $"!new {ruleType}().IsValid({valueName})",
			format
		);
	}

	static bool TryGetValidationAttribute<TAttribute>(TAttribute attribute, out ValidationAttributeData data)
	{
		switch (attribute)
		{
			case UrlAttribute { Exists: true } url:
				data = url.ValidationAttribute;
				return true;
			case PhoneAttribute { Exists: true } phone:
				data = phone.ValidationAttribute;
				return true;
			case CreditCardAttributeData { Exists: true } creditCard:
				data = creditCard.ValidationAttribute;
				return true;
			case Base64StringAttributeData { Exists: true } base64:
				data = base64.ValidationAttribute;
				return true;
			default:
				data = default;
				return false;
		}
	}

	/// <summary>
	/// Emits a string format check (email, regex, URL, …): an empty string is valid, as in DataAnnotations, and
	/// a failure reports <c>invalid_string</c> with category <c>invalid_format</c> and the format name.
	/// </summary>
	static void GenerateFormatValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		ValidationAttributeData validationAttribute,
		string defaultFormat,
		Func<string, string> invalidCondition,
		string format,
		(string Name, string ValueExpression)[]? extraValues = null,
		params string[] ordinalNames
	)
	{
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Value");
		var message = BuildMessageTemplate(validationAttribute, defaultFormat, ordinalNames);
		(string Name, string ValueExpression)[] values = [("format", format.Surround()), .. extraValues ?? []];

		using (writer.OpenBlockScope())
		{
			writer.Assignment("var", propertyValueName, $"value.{property.Name}");
			using (writer.IfBlockScope($"{propertyValueName}.Length != 0 && {invalidCondition(propertyValueName)}"))
			{
				WriteAddError(
					writer,
					ErrorExpression(
						"\"invalid_string\"",
						message,
						PathOf(property),
						MemberOf(property),
						origin: StringOrigin,
						category: Category("InvalidFormat"),
						valuesExpression: ValuesExpression(values)
					)
				);
			}
		}

		writer.NewLine();
	}

	static void StringLengthValidators(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var propertyLengthName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Length");
		var countValues = ValuesExpression(("count", propertyLengthName));

		var lengthAttr = property.ValidationAttributes.Length;
		if (lengthAttr.ShouldProcess && lengthAttr.Value.Exists)
		{
			var length = lengthAttr.Value;
			if (length.MinimumLength >= 0 && length.MaximumLength >= length.MinimumLength)
			{
				// LengthAttribute formats a custom message with (name, MinimumLength, MaximumLength).
				var tooSmall = BuildMessageTemplate(
					length.ValidationAttribute,
					AtLeast(length.MinimumLength, "character", "characters"),
					"minimum",
					"maximum"
				);
				var tooBig = BuildMessageTemplate(
					length.ValidationAttribute,
					NoMoreThan(length.MaximumLength, "character", "characters"),
					"minimum",
					"maximum"
				);

				WriteStringLengthCheck(
					writer,
					property,
					"global::ZodSharp.Rules.LengthRule.ErrorCode",
					length.MinimumLength,
					length.MaximumLength,
					tooSmall,
					tooBig,
					checkNull: true,
					countValues
				);
			}
		}

		var stringLengthAttr = property.ValidationAttributes.StringLength;
		if (stringLengthAttr.ShouldProcess && stringLengthAttr.Value.Exists)
		{
			var stringLength = stringLengthAttr.Value;

			// StringLengthAttribute formats a custom message with (name, MaximumLength, MinimumLength).
			var tooSmall = BuildMessageTemplate(
				stringLength.ValidationAttribute,
				AtLeast(stringLength.MinimumLength, "character", "characters"),
				"maximum",
				"minimum"
			);
			var tooBig = BuildMessageTemplate(
				stringLength.ValidationAttribute,
				NoMoreThan(stringLength.MaximumLength, "character", "characters"),
				"maximum",
				"minimum"
			);

			WriteStringLengthCheck(
				writer,
				property,
				"global::ZodSharp.Rules.StringLengthRule.ErrorCode",
				stringLength.MinimumLength,
				stringLength.MaximumLength,
				tooSmall,
				tooBig,
				checkNull: false,
				countValues
			);
		}

		var minLengthAttr = property.ValidationAttributes.MinLength;
		if (minLengthAttr.ShouldProcess && minLengthAttr.Value.Exists && minLengthAttr.Value.Length > 0)
		{
			var minLength = minLengthAttr.Value.Length;
			var message = BuildMessageTemplate(
				minLengthAttr.Value.ValidationAttribute,
				AtLeast(minLength, "character", "characters"),
				"minimum"
			);

			using (writer.OpenBlockScope())
			{
				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
				using (writer.IfBlockScope($"{propertyLengthName} < {minLength}"))
				{
					WriteAddError(
						writer,
						ErrorExpression(
							"\"too_small\"",
							message,
							PathOf(property),
							MemberOf(property),
							origin: StringOrigin,
							category: Category("TooSmall"),
							lowerExpression: Including(minLength),
							valuesExpression: countValues
						)
					);
				}
			}

			writer.NewLine();
		}

		var maxLengthAttr = property.ValidationAttributes.MaxLength;
		if (maxLengthAttr.ShouldProcess && maxLengthAttr.Value.Exists && maxLengthAttr.Value.Length >= 0)
		{
			var maxLength = maxLengthAttr.Value.Length;
			var message = BuildMessageTemplate(
				maxLengthAttr.Value.ValidationAttribute,
				NoMoreThan(maxLength, "character", "characters"),
				"maximum"
			);

			using (writer.OpenBlockScope())
			{
				writer.Assignment("var", propertyValueName, $"value.{propertyName}");
				writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
				using (writer.IfBlockScope($"{propertyLengthName} > {maxLength}"))
				{
					WriteAddError(
						writer,
						ErrorExpression(
							"\"too_big\"",
							message,
							PathOf(property),
							MemberOf(property),
							origin: StringOrigin,
							category: Category("TooBig"),
							upperExpression: Including(maxLength),
							valuesExpression: countValues
						)
					);
				}
			}

			writer.NewLine();
		}
	}

	/// <summary>
	/// Emits a two-sided string length check that reports the rule's own code (<c>invalid_length</c>, the same
	/// code the runtime <c>LengthRule</c>/<c>StringLengthRule</c> report), with the category by direction and
	/// both bounds.
	/// </summary>
	static void WriteStringLengthCheck(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		string codeExpression,
		int minimum,
		int maximum,
		MessageTemplate tooSmall,
		MessageTemplate tooBig,
		bool checkNull,
		string countValues
	)
	{
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Value");
		var propertyLengthName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Length");
		var lower = Including(minimum);
		var upper = Including(maximum);

		using (writer.OpenBlockScope())
		{
			writer.Assignment("var", propertyValueName, $"value.{property.Name}");
			CodeWriter.BlockScope? nullGuard = checkNull
				? writer.IfBlockScope($"({propertyValueName} is not null)")
				: null;

			writer.Assignment("var", propertyLengthName, $"{propertyValueName}.Length");
			writer
				.IfBlock($"{propertyLengthName} < {minimum}", w =>
					WriteAddError(
						w,
						ErrorExpression(
							codeExpression,
							tooSmall,
							PathOf(property),
							MemberOf(property),
							origin: StringOrigin,
							category: Category("TooSmall"),
							lowerExpression: lower,
							upperExpression: upper,
							valuesExpression: countValues
						)
					)
				)
				.ElseIf($"{propertyLengthName} > {maximum}", w =>
					WriteAddError(
						w,
						ErrorExpression(
							codeExpression,
							tooBig,
							PathOf(property),
							MemberOf(property),
							origin: StringOrigin,
							category: Category("TooBig"),
							lowerExpression: lower,
							upperExpression: upper,
							valuesExpression: countValues
						)
					)
				);

			nullGuard?.Dispose();
		}

		writer.NewLine();
	}

	static string AtLeast(int count, string singular, string plural) =>
		$"Field '{{displayName}}' must contain at least {{minimum}} {(count == 1 ? singular : plural)}.";

	static string NoMoreThan(int count, string singular, string plural) =>
		$"Field '{{displayName}}' must contain no more than {{maximum}} {(count == 1 ? singular : plural)}.";
}

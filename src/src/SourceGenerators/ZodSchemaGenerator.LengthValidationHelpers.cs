using System.Globalization;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	static string BuildMessageExpression(
		ValidationAttributeData validationAttribute,
		string defaultMessageExpression,
		params string[] formatArguments
	)
	{
		if (
			!string.IsNullOrEmpty(validationAttribute.ErrorMessageResourceName)
			&& validationAttribute.ErrorMessageResourceType is not null
		)
		{
			var resourceType = validationAttribute.ErrorMessageResourceType.Value;
			return BuildFormatExpression(
				$"{new TypeReference(resourceType).RenderFullName}.{validationAttribute.ErrorMessageResourceName}",
				formatArguments
			);
		}

		var errorMessage = validationAttribute.ErrorMessage;
		return errorMessage is { Length: > 0 }
			? BuildFormatExpression(errorMessage.StringLiteral(), formatArguments)
			: defaultMessageExpression;
	}

	static string BuildErrorMessageExpression(
		ValidationAttributeData validationAttribute,
		string defaultFormat,
		params string[] formatArguments
	)
	{
		string formatExpression;

		if (
			!string.IsNullOrEmpty(validationAttribute.ErrorMessageResourceName)
			&& validationAttribute.ErrorMessageResourceType is not null
		)
		{
			var resourceType = validationAttribute.ErrorMessageResourceType.Value;
			formatExpression =
				$"{new TypeReference(resourceType).RenderFullName}.{validationAttribute.ErrorMessageResourceName}";
		}
		else
		{
			var errorMessage = validationAttribute.ErrorMessage;
			formatExpression = errorMessage is { Length: > 0 }
				? errorMessage.StringLiteral()
				: defaultFormat.StringLiteral();
		}

		// If there are no format arguments, we can return the format expression directly.
		return BuildFormatExpression(formatExpression, formatArguments);
	}

	static string BuildFormatExpression(string formatExpression, params string[] formatArguments) =>
		formatArguments.Length == 0
			? formatExpression
			: $"string.Format(global::System.Globalization.CultureInfo.CurrentCulture, {formatExpression}, {string.Join(", ", formatArguments)})";

	static void WriteValidationError(
		CodeWriter writer,
		string errorCode,
		string messageExpression,
		string pathFieldName,
		string origin,
		int? minimum = null,
		int? maximum = null
	)
	{
		writer.IfBlock(
			"errors is null",
			ifBody =>
				ifBody.Assignment(
					"errors",
					"new global::System.Collections.Generic.List<global::ZodSharp.Core.ValidationError>()"
				)
		);
		writer.MethodCallOn(
			"errors",
			"Add",
			$"{TypeLibrary.ZodSharp.Core.ValidationError}.Create({errorCode.Surround()}, {messageExpression}, {pathFieldName}, origin: {origin.Surround()}, minimum: {(minimum.HasValue ? minimum.Value.ToString(CultureInfo.InvariantCulture) : "null")}, maximum: {(maximum.HasValue ? maximum.Value.ToString(CultureInfo.InvariantCulture) : "null")}, inclusive: true)"
		);
	}

	static void WriteValidationError(
		CodeWriter writer,
		string errorCode,
		string messageExpression,
		string pathFieldName
	)
	{
		writer.IfBlock(
			"errors is null",
			ifBody =>
				ifBody.Assignment(
					"errors",
					"new global::System.Collections.Generic.List<global::ZodSharp.Core.ValidationError>()"
				)
		);
		writer.MethodCallOn(
			"errors",
			"Add",
			$"{TypeLibrary.ZodSharp.Core.ValidationError}.Create({errorCode.Surround()}, {messageExpression}, {pathFieldName})"
		);
	}

	static string GetRegexFieldName(string propertyName) => $"Regex_{propertyName}";

	static string GetRangeMinimumFieldName(string propertyName) => $"RangeMinimum_{propertyName}";

	static string GetRangeMaximumFieldName(string propertyName) => $"RangeMaximum_{propertyName}";
}

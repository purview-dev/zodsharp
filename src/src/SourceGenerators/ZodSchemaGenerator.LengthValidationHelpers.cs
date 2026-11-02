using System.Globalization;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	const string GeneratedValidation = "global::ZodSharp.Core.ZodGeneratedValidation";

	const string ZodPathType = "global::ZodSharp.Core.ZodPath";

	const string ZodPathSegmentType = "global::ZodSharp.Core.ZodPathSegment";

	const string ErrorBoundType = "global::ZodSharp.Core.ErrorBound";

	const string IssueCategories = "global::ZodSharp.Core.ZodIssueCategories";

	/// <summary>
	/// The message template of an attribute-driven error, with the names its positional placeholders bind to.
	/// The generator never formats the text: the template is formatted at runtime by the never-throw
	/// <c>ZodMessageFormat</c> engine, so a stray brace in a custom <c>ErrorMessage</c> cannot fail the build
	/// or the validation.
	/// </summary>
	/// <param name="FormatExpression">The expression that yields the template (a literal or a resource property).</param>
	/// <param name="OrdinalNamesExpression">
	/// The <c>string[]</c> expression naming the positional placeholders, or <see langword="null"/> when the
	/// template uses named placeholders only.
	/// </param>
	readonly record struct MessageTemplate(string FormatExpression, string? OrdinalNamesExpression);

	/// <summary>
	/// Resolves the message template for an attribute: its <c>ErrorMessageResourceType</c>/<c>Name</c>, its
	/// <c>ErrorMessage</c>, or the generator's named default. A user-supplied message keeps the DataAnnotations
	/// positional convention (<c>{0}</c> is the display name, then the attribute's own arguments), bound through
	/// <paramref name="ordinalNames"/>.
	/// </summary>
	static MessageTemplate BuildMessageTemplate(
		ValidationAttributeData validationAttribute,
		string defaultFormat,
		params string[] ordinalNames
	)
	{
		var ordinals = BuildOrdinalNames(ordinalNames);

		if (
			!string.IsNullOrEmpty(validationAttribute.ErrorMessageResourceName)
			&& validationAttribute.ErrorMessageResourceType is not null
		)
		{
			var resourceType = validationAttribute.ErrorMessageResourceType.Value;
			return new(
				$"{new TypeReference(resourceType).RenderFullName}.{validationAttribute.ErrorMessageResourceName}",
				ordinals
			);
		}

		return validationAttribute.ErrorMessage is { Length: > 0 } errorMessage
			? new(errorMessage.StringLiteral(), ordinals)
			: new(defaultFormat.StringLiteral(), null);
	}

	static string BuildOrdinalNames(string[] ordinalNames) =>
		$"new string[] {{ {string.Join(", ", ["displayName".Surround(), .. ordinalNames.Select(static name => name.Surround())])} }}";

	/// <summary>
	/// Renders a <c>ZodGeneratedValidation.Error(...)</c> call.
	/// </summary>
	static string ErrorExpression(
		string codeExpression,
		MessageTemplate message,
		string pathExpression,
		string memberExpression,
		string? origin = null,
		string? category = null,
		string? lowerExpression = null,
		string? upperExpression = null,
		string? valuesExpression = null
	)
	{
		List<string> arguments = [codeExpression, message.FormatExpression, pathExpression, memberExpression];
		if (origin is not null)
			arguments.Add($"origin: {origin}");
		if (category is not null)
			arguments.Add($"category: {category}");
		if (lowerExpression is not null)
			arguments.Add($"lower: {lowerExpression}");
		if (upperExpression is not null)
			arguments.Add($"upper: {upperExpression}");
		if (message.OrdinalNamesExpression is not null)
			arguments.Add($"ordinalNames: {message.OrdinalNamesExpression}");
		if (valuesExpression is not null)
			arguments.Add($"values: {valuesExpression}");

		return $"{GeneratedValidation}.Error({string.Join(", ", arguments)})";
	}

	/// <summary>
	/// Renders the named values passed alongside an error, as a <c>KeyValuePair&lt;string, object?&gt;[]</c>.
	/// </summary>
	static string ValuesExpression(params (string Name, string ValueExpression)[] values) =>
		$"new global::System.Collections.Generic.KeyValuePair<string, object?>[] {{ {string.Join(", ", values.Select(static value => $"new global::System.Collections.Generic.KeyValuePair<string, object?>({value.Name.Surround()}, {value.ValueExpression})"))} }}";

	static string Category(string name) => $"{IssueCategories}.{name}";

	static string Including(int value) => $"{ErrorBoundType}.Including({value.ToString(CultureInfo.InvariantCulture)})";

	static string Bound(string valueExpression, bool inclusive) =>
		$"{ErrorBoundType}.{(inclusive ? "Including" : "Excluding")}({valueExpression})";

	/// <summary>
	/// Emits <c>ZodGeneratedValidation.Add(ref errors, error)</c>.
	/// </summary>
	static void WriteAddError(CodeWriter writer, string errorExpression) =>
		writer.MethodCall($"{GeneratedValidation}.Add", "ref errors", errorExpression);

	static string GetRegexFieldName(string propertyName) => $"Regex_{propertyName}";

	static string GetRangeMinimumFieldName(string propertyName) => $"RangeMinimum_{propertyName}";

	static string GetRangeMaximumFieldName(string propertyName) => $"RangeMaximum_{propertyName}";

	/// <summary>The static path field (<c>Paths.X</c>) for a property.</summary>
	static string PathOf(ZodPropertyDescriptor property) => $"Paths.{GetPathMemberName(property.Name)}";

	/// <summary>The static member-info field for a property.</summary>
	static string MemberOf(ZodPropertyDescriptor property) => CodeGenHelpers.GetMemberFieldName(property.Name);

	/// <summary>
	/// The name of a property's field in the generated <c>Paths</c> class. A member cannot share its enclosing
	/// type's name (CS0542), so a property named <c>Paths</c> gets a trailing underscore.
	/// </summary>
	static string GetPathMemberName(string propertyName) =>
		propertyName is "Paths" or "JsonNames" ? propertyName + "_" : propertyName;
}

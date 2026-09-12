using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators.Models.DataAttributes;

[Generate("System.ComponentModel.DataAnnotations.ValidationAttribute", MatchByInheritance = true)]
readonly partial record struct ValidationAttributeData(
	string? ErrorMessage,
	string? ErrorMessageResourceName,
	TypeIdentity? ErrorMessageResourceType
);

[Generate("System.ComponentModel.DataAnnotations.RequiredAttribute")]
readonly partial record struct RequiredAttributeData(
	bool AllowEmptyStrings,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

[Generate("System.ComponentModel.DataAnnotations.CompareAttribute")]
readonly partial record struct CompareAttributeData(
	[Argument("otherProperty", DefaultValue = "")] string OtherProperty,
	string? OtherPropertyDisplayName,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

[Generate("System.ComponentModel.DataAnnotations.DisplayAttribute")]
readonly partial record struct DisplayAttributeData(
	string? ShortName,
	string? Name,
	string? Description,
	string? Prompt,
	TypeIdentity? ResourceType,
	bool AutoGenerateField,
	bool AutoGenerateFilter,
	int Order
);

[Generate("System.ComponentModel.DataAnnotations.EmailAddressAttribute")]
readonly partial record struct EmailAddressAttributeData([NestedModel] ValidationAttributeData ValidationAttributeData);

[Generate("System.ComponentModel.DataAnnotations.CreditCardAttribute")]
readonly partial record struct CreditCardAttributeData([NestedModel] ValidationAttributeData ValidationAttribute);

[Generate("System.ComponentModel.DataAnnotations.PhoneAttribute")]
readonly partial record struct PhoneAttribute([NestedModel] ValidationAttributeData ValidationAttribute);

[Generate("System.ComponentModel.DataAnnotations.UrlAttribute")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Naming",
	"PDS0004:Use correct acronym capitalization",
	Justification = "Real name"
)]
readonly partial record struct UrlAttribute([NestedModel] ValidationAttributeData ValidationAttribute);

[Generate("System.ComponentModel.DataAnnotations.StringLengthAttribute")]
readonly partial record struct StringLengthAttribute(
	[Argument("maximumLength")] int MaximumLength,
	[Property] int MinimumLength,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

[Generate("System.ComponentModel.DataAnnotations.MinLengthAttribute")]
readonly partial record struct MinLengthAttributeData(
	[Argument("length")] int Length,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

[Generate("System.ComponentModel.DataAnnotations.MaxLengthAttribute")]
readonly partial record struct MaxLengthAttributeData(
	[Argument("length", DefaultValue = -1)] int Length,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

[Generate("System.ComponentModel.DataAnnotations.RegularExpressionAttribute")]
readonly partial record struct RegularExpressionAttributeData(
	[Argument("pattern")] string? Pattern,
	[Property(DefaultValue = 2000)] int MatchTimeoutInMilliseconds,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

readonly partial record struct AllowedValuesAttributeData(
	bool Exists,
	EquatableArray<TypedConstant> Values,
	ValidationAttributeData ValidationAttribute
)
{
	public static readonly AllowedValuesAttributeData Empty = new(
		false,
		new EquatableArray<TypedConstant>([]),
		ValidationAttributeData.Empty
	);

	public static readonly TypeIdentity TargetAttribute = new(
		"AllowedValuesAttribute",
		"System.ComponentModel.DataAnnotations"
	);

	public static AllowedValuesAttributeData FromAttributeData(AttributeData attributeData)
	{
		if (!TargetAttribute.Equals(attributeData.AttributeClass))
			return Empty;

		var values = GetValues(attributeData);
		var validationAttribute = ValidationAttributeData.FromAttributeData(attributeData);
		return new(true, values, validationAttribute);
	}

	public static AllowedValuesAttributeData FromAttributeData(ImmutableArray<AttributeData> attributes)
	{
		foreach (var attribute in attributes)
		{
			var result = FromAttributeData(attribute);
			if (result.Exists)
				return result;
		}

		return Empty;
	}

	public static AllowedValuesAttributeData FromAttributeData(ISymbol symbol) =>
		FromAttributeData(symbol.GetAttributes());

	public static bool TryFromAttributeData(
		ISymbol symbol,
		out AllowedValuesAttributeData attributeData,
		out AttributeData? attribute
	)
	{
		attributeData = Empty;
		attribute = null;

		foreach (var attr in symbol.GetAttributes())
		{
			var result = FromAttributeData(attr);
			if (!result.Exists)
				continue;

			attributeData = result;
			attribute = attr;
			return true;
		}

		return false;
	}

	static EquatableArray<TypedConstant> GetValues(AttributeData attributeData)
	{
		if (attributeData.ConstructorArguments.Length == 0)
			return new EquatableArray<TypedConstant>([]);

		var argument = attributeData.ConstructorArguments[0];
		if (argument.Kind == TypedConstantKind.Array)
			return new(argument.Values);

		// If the attribute was constructed with a single value, we wrap it in an array for consistency.
		return new([argument]);
	}
}

[Generate("System.ComponentModel.DataAnnotations.Base64StringAttribute")]
readonly partial record struct Base64StringAttributeData([NestedModel] ValidationAttributeData ValidationAttribute);

readonly partial record struct DeniedValuesAttributeData(
	bool Exists,
	EquatableArray<TypedConstant> Values,
	ValidationAttributeData ValidationAttribute
)
{
	public static readonly DeniedValuesAttributeData Empty = new(
		false,
		new EquatableArray<TypedConstant>([]),
		ValidationAttributeData.Empty
	);

	public static readonly TypeIdentity TargetAttribute = new(
		"DeniedValuesAttribute",
		"System.ComponentModel.DataAnnotations"
	);

	public static DeniedValuesAttributeData FromAttributeData(AttributeData attributeData)
	{
		if (!TargetAttribute.Equals(attributeData.AttributeClass))
			return Empty;

		var values = GetValues(attributeData);
		var validationAttribute = ValidationAttributeData.FromAttributeData(attributeData);
		return new(true, values, validationAttribute);
	}

	public static DeniedValuesAttributeData FromAttributeData(ImmutableArray<AttributeData> attributes)
	{
		foreach (var attribute in attributes)
		{
			var result = FromAttributeData(attribute);
			if (result.Exists)
				return result;
		}

		return Empty;
	}

	public static DeniedValuesAttributeData FromAttributeData(ISymbol symbol) =>
		FromAttributeData(symbol.GetAttributes());

	public static bool TryFromAttributeData(
		ISymbol symbol,
		out DeniedValuesAttributeData attributeData,
		out AttributeData? attribute
	)
	{
		attributeData = Empty;
		attribute = null;

		foreach (var attr in symbol.GetAttributes())
		{
			var result = FromAttributeData(attr);
			if (!result.Exists)
				continue;

			attributeData = result;
			attribute = attr;
			return true;
		}

		return false;
	}

	static EquatableArray<TypedConstant> GetValues(AttributeData attributeData)
	{
		if (attributeData.ConstructorArguments.Length == 0)
			return new EquatableArray<TypedConstant>([]);

		var argument = attributeData.ConstructorArguments[0];
		if (argument.Kind == TypedConstantKind.Array)
			return new(argument.Values);

		// If the attribute was constructed with a single value, we wrap it in an array for consistency.
		return new([argument]);
	}
}

[Generate("System.ComponentModel.DataAnnotations.LengthAttribute")]
readonly partial record struct LengthAttributeData(
	[Argument("minimumLength")] int MinimumLength,
	[Argument("maximumLength")] int MaximumLength,
	[NestedModel] ValidationAttributeData ValidationAttribute
);

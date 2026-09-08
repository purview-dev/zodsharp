using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZodSharp.SourceGenerators.Models.DataAttributes;

enum RangeAttributeKind
{
	None,
	Int32,
	Double,
	Converted,
}

readonly record struct RangeAttributeData(
	bool Exists,
	RangeAttributeKind Kind,
	object? Minimum,
	object? Maximum,
	TypeIdentity? OperandType,
	bool MinimumIsExclusive,
	bool MaximumIsExclusive,
	bool ConvertValueInInvariantCulture,
	bool ParseLimitsInInvariantCulture,
	ValidationAttributeData ValidationAttribute
)
{
	public static readonly RangeAttributeData Empty;

	public string? MinimumExpression { get; init; }

	public string? MaximumExpression { get; init; }

	public static RangeAttributeData FromAttributeData(ImmutableArray<AttributeData> attributes) =>
		FromAttributeData(attributes, out _);

	public static RangeAttributeData FromAttributeData(
		ImmutableArray<AttributeData> attributes,
		out AttributeData? attribute
	)
	{
		attribute = null;
		for (var i = 0; i < attributes.Length; i++)
		{
			var result = FromAttributeData(attributes[i]);
			if (result.Exists)
			{
				attribute = attributes[i];
				return result;
			}
		}

		return Empty;
	}

	public static RangeAttributeData FromAttributeData(AttributeData attributeData)
	{
		if (!TypeLibrary.System.ComponentModel.DataAnnotations.RangeAttribute.Equals(attributeData.AttributeClass))
			return Empty;

		var constructorArguments = attributeData.ConstructorArguments;

		if (constructorArguments.Length is not (2 or 3))
		{
			// RangeAttribute has an unsupported constructor shape
			return Empty;
		}

		var kind = RangeAttributeKind.None;
		object? minimum = null;
		object? maximum = null;
		TypeIdentity? operandType = null;

		if (constructorArguments.Length == 2)
		{
			// Min + Max
			ReadNumericRange(constructorArguments, ref kind, ref minimum, ref maximum, ref operandType);
		}
		else if (constructorArguments.Length == 3)
		{
			// OperandType + Min + Max
			ReadConvertedRange(constructorArguments, ref kind, ref minimum, ref maximum, ref operandType);
		}
		else
		{
			// This is kind of future proofing, but if the constructor arguments are not 2 or 3, we don't know how to handle it.
			return Empty;
		}

		attributeData.TryGetNamedArgument<bool>(nameof(MinimumIsExclusive), out var minimumIsExclusive);
		attributeData.TryGetNamedArgument<bool>(nameof(MaximumIsExclusive), out var maximumIsExclusive);
		attributeData.TryGetNamedArgument<bool>(
			nameof(ConvertValueInInvariantCulture),
			out var convertValueInInvariantCulture
		);
		attributeData.TryGetNamedArgument<bool>(
			nameof(ParseLimitsInInvariantCulture),
			out var parseLimitsInInvariantCulture
		);

		var validationAttribute = ValidationAttributeData.FromAttributeData(attributeData);

		// Success..!!
		return new(
			Exists: true,
			Kind: kind,
			Minimum: minimum,
			Maximum: maximum,
			OperandType: operandType,
			minimumIsExclusive,
			MaximumIsExclusive: maximumIsExclusive,
			ConvertValueInInvariantCulture: convertValueInInvariantCulture,
			ParseLimitsInInvariantCulture: parseLimitsInInvariantCulture,
			ValidationAttribute: validationAttribute
		);
	}

	static void ReadNumericRange(
		ImmutableArray<TypedConstant> arguments,
		ref RangeAttributeKind kind,
		ref object? minimum,
		ref object? maximum,
		ref TypeIdentity? operandType
	)
	{
		var minimumArgument = arguments[0];
		var maximumArgument = arguments[1];

		if (minimumArgument.Value is int minimumValue && maximumArgument.Value is int maximumValue)
		{
			kind = RangeAttributeKind.Int32;
			minimum = minimumValue;
			maximum = maximumValue;
			operandType = minimumArgument.Type is not null ? new TypeIdentity(minimumArgument.Type) : null;
			return;
		}

		if (minimumArgument.Value is double minimumValueDouble && maximumArgument.Value is double maximumValueDouble)
		{
			kind = RangeAttributeKind.Double;
			minimum = minimumValueDouble;
			maximum = maximumValueDouble;
			operandType = minimumArgument.Type is not null ? new TypeIdentity(minimumArgument.Type) : null;
		}
	}

	static void ReadConvertedRange(
		ImmutableArray<TypedConstant> arguments,
		ref RangeAttributeKind kind,
		ref object? minimum,
		ref object? maximum,
		ref TypeIdentity? operandType
	)
	{
		if (
			arguments[0].Value is not ITypeSymbol type
			|| arguments[1].Value is not string minimumValue
			|| arguments[2].Value is not string maximumValue
		)
		{
			return;
		}

		kind = RangeAttributeKind.Converted;
		operandType = new TypeIdentity(type);
		minimum = minimumValue;
		maximum = maximumValue;
	}
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Resolves the validation attributes that apply to a property: the attributes on the property itself plus,
/// when the property is synthesized from a positional record parameter, the attributes on that parameter.
/// </summary>
/// <remarks>
/// <para>
/// The C# compiler applies an attribute that is valid on both a parameter and a property to the parameter
/// only, so an attribute written on a positional record parameter without a <c>property:</c> target is not
/// present on the synthesized property. Reading the property alone would silently drop the rule, so the
/// parameter's attributes are merged in.
/// </para>
/// <para>
/// A property attribute of the same type wins: it is the more specific target for property validation, and
/// honoring both would emit the rule twice.
/// </para>
/// </remarks>
static class PropertyAttributeResolver
{
	/// <summary>
	/// Gets the validation attributes that apply to <paramref name="property"/>, in declaration order.
	/// </summary>
	/// <param name="property">The property the schema validates.</param>
	/// <param name="compilation">The compilation being generated, used to bind the declaring parameter.</param>
	/// <returns>The effective validation attributes.</returns>
	public static ImmutableArray<AttributeData> Resolve(IPropertySymbol property, Compilation compilation)
	{
		var attributes = property.GetAttributes();
		var parameter = GetPositionalRecordParameter(property, compilation);
		if (parameter is null)
			return attributes;

		var parameterAttributes = parameter.GetAttributes();
		if (parameterAttributes.IsEmpty)
			return attributes;

		var merged = ImmutableArray.CreateBuilder<AttributeData>(attributes.Length + parameterAttributes.Length);
		merged.AddRange(attributes);

		foreach (var parameterAttribute in parameterAttributes)
		{
			var alreadyPresent = false;
			foreach (var attribute in attributes)
			{
				if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parameterAttribute.AttributeClass))
				{
					alreadyPresent = true;
					break;
				}
			}

			if (!alreadyPresent)
				merged.Add(parameterAttribute);
		}

		return merged.ToImmutable();
	}

	/// <summary>
	/// Gets the primary-constructor parameter a positional record property is synthesized from.
	/// </summary>
	/// <param name="property">The property to inspect.</param>
	/// <param name="compilation">The compilation being generated, used to bind the declaring parameter.</param>
	/// <returns>The declaring parameter, or <see langword="null"/> for a declared property.</returns>
	/// <remarks>
	/// A positional record property's declaring syntax is the parameter itself; a declared property's is its
	/// own declaration, so this returns <see langword="null"/> for every non-positional property.
	/// </remarks>
	static IParameterSymbol? GetPositionalRecordParameter(IPropertySymbol property, Compilation compilation)
	{
		foreach (var reference in property.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax() is not ParameterSyntax parameterSyntax)
				continue;

			if (
				compilation.GetSemanticModel(parameterSyntax.SyntaxTree).GetDeclaredSymbol(parameterSyntax)
				is not IParameterSymbol parameter
			)
			{
				continue;
			}

			return parameter;
		}

		return null;
	}
}

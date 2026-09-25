using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	/// <summary>
	/// Builds the pipeline that turns rules marked with the parameterless <c>[ZodRule]</c> into
	/// DataAnnotations-style validation attributes.
	/// </summary>
	internal static IncrementalValuesProvider<GeneratorResult<RuleAttributeGenerationModel>> GetRuleAttributeProvider(
		IncrementalGeneratorInitializationContext context
	) =>
		IncrementalPipeline.ForAttributeWithMetadataName(
			context,
			TypeLibrary.ZodSharp.Core.ZodRuleAttribute,
			predicate: static (node, _) => node is TypeDeclarationSyntax,
			transform: static (attributeContext, cancellationToken) =>
				GetRuleAttributeModel(attributeContext, cancellationToken)
		);

	internal static void EmitRuleAttributes(
		SourceProductionContext context,
		SchemaGenerationModel model,
		ImmutableArray<GeneratorResult<RuleAttributeGenerationModel>> rules
	)
	{
		foreach (var rule in rules)
		{
			foreach (var diagnostic in rule.Diagnostics)
				context.ReportDiagnostic(diagnostic.ToDiagnostic());

			if (!rule.ShouldProcess)
				continue;

			var writer = model.Context.CreateCodeWriter();
			BuildRuleAttribute(writer, rule.Value);
			context.AddSource($"{rule.Value.AttributeType.Namespace}.{rule.Value.AttributeType.Name}.g.cs", writer);
		}
	}

	static GeneratorResult<RuleAttributeGenerationModel> GetRuleAttributeModel(
		GeneratorAttributeSyntaxContext context,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (context.TargetSymbol is not INamedTypeSymbol ruleType)
			return default;

		// Only the parameterless form (marking a rule) is handled here; the mapping form carries a rule
		// type and is resolved per property by CustomRuleResolver.
		if (!IsValidationRule(ruleType))
			return default;

		var attribute = context.Attributes[0];
		if (attribute.ConstructorArguments.Length > 0)
			return default;

		if (ruleType.IsGenericType || ruleType.ContainingType is not null || ruleType.IsAbstract)
		{
			return GeneratorResult<RuleAttributeGenerationModel>.Create(
				default(RuleAttributeGenerationModel),
				ReportableDiagnostic.Create(
					DiagnosticLibrary.UnsupportedRuleAttributeGeneration,
					true,
					ruleType,
					ruleType.Name,
					"only non-generic, non-nested, non-abstract rules are supported"
				)
			);
		}

		var attributeName = ResolveAttributeName(ruleType, attribute);
		if (attributeName is null)
			return default;

		var properties = ImmutableArray.CreateBuilder<GeneratedAttributeProperty>();
		var constructor = ruleType
			.InstanceConstructors.Where(static c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
			.OrderByDescending(static c => c.Parameters.Length)
			.FirstOrDefault();

		if (constructor is not null)
		{
			foreach (var parameter in constructor.Parameters)
			{
				if (IsMessageParameter(parameter) || IsCancellationToken(parameter.Type))
					continue;

				if (!IsSupportedAttributePropertyType(parameter.Type))
				{
					return GeneratorResult<RuleAttributeGenerationModel>.Create(
						default(RuleAttributeGenerationModel),
						ReportableDiagnostic.Create(
							DiagnosticLibrary.UnsupportedRuleAttributeGeneration,
							true,
							ruleType,
							ruleType.Name,
							$"constructor parameter '{parameter.Name}' has type '{parameter.Type.ToDisplayString()}', which cannot be represented as an attribute property"
						)
					);
				}

				properties.Add(
					new GeneratedAttributeProperty(
						new TypeIdentity(TypeHelpers.StripNullableAnnotations(parameter.Type)),
						ToPascalCase(parameter.Name),
						BuildInitializer(parameter),
						TypeHelpers.CanBeNull(parameter.Type)
					)
				);
			}
		}

		return GeneratorResult<RuleAttributeGenerationModel>.Create(
			new RuleAttributeGenerationModel(
				new TypeIdentity(ruleType),
				new TypeIdentity(attributeName, ruleType.ContainingNamespace.ToDisplayString()),
				ruleType.DeclaredAccessibility == Accessibility.Public
					? TypeDeclarationAccessibility.Public
					: TypeDeclarationAccessibility.Internal,
				GetNamedString(attribute, "Code"),
				GetNamedString(attribute, "Origin"),
				new(properties.ToImmutable())
			)
		);
	}

	static bool IsValidationRule(INamedTypeSymbol ruleType) =>
		ruleType.AllInterfaces.Any(static iface =>
			iface.OriginalDefinition is { Name: "IValidationRule", Arity: 1 } definition
			&& definition.ContainingNamespace.ToDisplayString() == TypeLibraryGenerator.ZodSharpCoreNamespace
		);

	static bool IsMessageParameter(IParameterSymbol parameter) =>
		string.Equals(parameter.Name, "message", StringComparison.OrdinalIgnoreCase);

	static bool IsCancellationToken(ITypeSymbol type) => type.ToDisplayString() == "System.Threading.CancellationToken";

	static string BuildInitializer(IParameterSymbol parameter)
	{
		if (
			parameter.HasExplicitDefaultValue
			&& CustomRuleResolver.TryConvertValue(parameter.ExplicitDefaultValue, parameter.Type, out var literal)
		)
		{
			return literal;
		}

		return parameter.Type.IsValueType ? "default!" : "null!";
	}

	static string ToPascalCase(string name) =>
		name.Length == 0 ? name : string.Concat(char.ToUpperInvariant(name[0]), name.AsSpan(1).ToString());

	static string? GetNamedString(AttributeData attribute, string name)
	{
		foreach (var pair in attribute.NamedArguments)
		{
			if (pair.Key == name)
				return pair.Value.Value as string;
		}

		return null;
	}

	static string? ResolveAttributeName(INamedTypeSymbol ruleType, AttributeData attribute)
	{
		const string ruleSuffix = "Rule";

		var explicitName = GetNamedString(attribute, "AttributeName");
		var baseName =
			explicitName is { Length: > 0 } ? explicitName
			: ruleType.Name.EndsWith(ruleSuffix, StringComparison.Ordinal) && ruleType.Name.Length > ruleSuffix.Length
				? ruleType.Name.Substring(0, ruleType.Name.Length - ruleSuffix.Length)
			: ruleType.Name;
		var attributeName = $"{baseName}Attribute";

		// A hand-authored attribute already claims the name: leave it untouched.
		return ruleType.ContainingNamespace.GetTypeMembers(attributeName).Length > 0 ? null : attributeName;
	}

	static bool IsSupportedAttributePropertyType(ITypeSymbol type)
	{
		var unwrapped = TypeHelpers.UnwrapNullableType(type);
		if (unwrapped is INamedTypeSymbol { TypeKind: TypeKind.Enum })
			return true;

		if (unwrapped.ToDisplayString() == "System.Type")
			return true;

		return unwrapped.SpecialType
			is SpecialType.System_Boolean
				or SpecialType.System_Byte
				or SpecialType.System_SByte
				or SpecialType.System_Char
				or SpecialType.System_Int16
				or SpecialType.System_UInt16
				or SpecialType.System_Int32
				or SpecialType.System_UInt32
				or SpecialType.System_Int64
				or SpecialType.System_UInt64
				or SpecialType.System_Single
				or SpecialType.System_Double
				or SpecialType.System_String;
	}

	static void BuildRuleAttribute(CodeWriter writer, RuleAttributeGenerationModel model)
	{
		writer.AutoGeneratedHeader();
		writer.Using("System").NewLine();
		writer.FileScopedNamespace(model.AttributeType.Namespace);

		writer.XmlSummary(
			$"Validation attribute that applies {CodeWriter.XmlSee(model.RuleType.Name)}.",
			"Generated from the rule's [ZodRule] marker; the properties mirror the rule's constructor parameters."
		);

		using (
			writer.ClassScope(
				new TypeDeclarationOptions(model.AttributeType, model.Accessibility)
				{
					IsSealed = true,
					BaseType = TypeLibrary.System.ComponentModel.DataAnnotations.ValidationAttribute.AsTypeReference(),
					Attributes =
					[
						new AttributeDeclarationOptions(TypeLibrary.System.AttributeUsageAttribute)
						{
							Arguments =
							[
								new AttributeArgumentOptions(
									"global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter",
									null,
									false
								),
								new AttributeArgumentOptions("true", "Inherited", true),
								new AttributeArgumentOptions("false", "AllowMultiple", true),
							],
						},
						new AttributeDeclarationOptions(TypeLibrary.ZodSharp.Core.ZodRuleAttribute)
						{
							Arguments = BuildRuleArguments(model),
						},
					],
				}
			)
		)
		{
			foreach (var property in model.Properties)
			{
				var propertyType = property.IsNullable
					? property.Type.AsTypeReference().Nullable(writer)
					: property.Type.AsTypeReference();

				writer.Property(
					new PropertyDeclarationOptions(property.Name, propertyType, TypeDeclarationAccessibility.Public)
					{
						HasGetter = true,
						HasSetter = true,
						Initializer = property.Initializer,
					}
				);
			}
		}
	}

	static ImmutableArray<AttributeArgumentOptions> BuildRuleArguments(RuleAttributeGenerationModel model)
	{
		var builder = ImmutableArray.CreateBuilder<AttributeArgumentOptions>();
		builder.Add(new($"typeof({model.RuleType.RenderFullName})", null, false));

		if (model.Code is { Length: > 0 } code)
			builder.Add(new(code.StringLiteral(), "Code", true));

		if (model.Origin is { Length: > 0 } origin)
			builder.Add(new(origin.StringLiteral(), "Origin", true));

		return builder.ToImmutable();
	}
}

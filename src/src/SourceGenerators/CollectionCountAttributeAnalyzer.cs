using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Reports ZODSGEN053 for <c>[MinLength]</c>, <c>[MaxLength]</c> or <c>[Length]</c> on a collection member of a
/// <c>[ZodSchema]</c> type when the project is trimmed, AOT-compiled, or analyzed as trimmable/AOT-compatible. The
/// BCL attributes' constructors are <c>[RequiresUnreferencedCode]</c> (they may reflect over a <c>Count</c> property),
/// so each usage raises IL2026 even though the generated validator never runs their reflection. The ZodSharp
/// collection-count attributes (<c>[MinCount]</c>, <c>[MaxCount]</c>, <c>[CountRange]</c>) are validated identically.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionCountAttributeAnalyzer : DiagnosticAnalyzer
{
	/// <summary>The diagnostic property naming the replacement attribute (without the <c>Attribute</c> suffix).</summary>
	public const string ReplacementProperty = "Replacement";

	/// <summary>
	/// The MSBuild properties that mark a project as trimmed or AOT-compiled. Any one set to <c>true</c> enables the
	/// diagnostic: the publish switches, the library switches, and the analyzer switches they turn on.
	/// </summary>
	internal static readonly ImmutableArray<string> TrimmingProperties =
	[
		"PublishTrimmed",
		"PublishAot",
		"IsTrimmable",
		"IsAotCompatible",
		"EnableTrimAnalyzer",
		"EnableAotAnalyzer",
	];

	const string DataAnnotationsNamespace = "System.ComponentModel.DataAnnotations";

	/// <inheritdoc/>
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
	[DiagnosticLibrary.UseTrimSafeCollectionCountAttribute];

	/// <inheritdoc/>
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			if (!IsTrimmedOrAot(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
				return;

			// The replacements ship in Purview.ZodSharp; without it there is nothing to suggest.
			if (start.Compilation.GetTypeByMetadataName("ZodSharp.Rules.MinCountAttribute") is null)
				return;

			start.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
			start.RegisterSymbolAction(AnalyzeParameter, SymbolKind.Parameter);
		});
	}

	/// <summary>
	/// Whether any of <see cref="TrimmingProperties"/> is <c>true</c>.
	/// </summary>
	/// <param name="options">The global analyzer config options.</param>
	/// <returns><see langword="true"/> for a trimmed or AOT project.</returns>
	internal static bool IsTrimmedOrAot(AnalyzerConfigOptions options)
	{
		foreach (var property in TrimmingProperties)
		{
			if (
				options.TryGetValue($"build_property.{property}", out var value)
				&& bool.TryParse(value, out var enabled)
				&& enabled
			)
				return true;
		}

		return false;
	}

	static void AnalyzeProperty(SymbolAnalysisContext context)
	{
		if (context.Symbol is IPropertySymbol property && IsZodSchemaType(property.ContainingType))
			Analyze(context, property.Name, property.Type, property.GetAttributes());
	}

	// A positional record's attributes are written on the primary-constructor parameter.
	static void AnalyzeParameter(SymbolAnalysisContext context)
	{
		if (
			context.Symbol
				is IParameterSymbol
				{
					ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
				} parameter
			&& constructor.ContainingType is { IsRecord: true } record
			&& IsZodSchemaType(record)
		)
			Analyze(context, parameter.Name, parameter.Type, parameter.GetAttributes());
	}

	static bool IsZodSchemaType(INamedTypeSymbol? type) =>
		type is not null && ZodSchemaAttributeData.FromAttributeData(type, out _).Exists;

	static void Analyze(
		SymbolAnalysisContext context,
		string memberName,
		ITypeSymbol memberType,
		ImmutableArray<AttributeData> attributes
	)
	{
		if (!CustomRuleResolver.IsCountableCollectionType(memberType))
			return;

		foreach (var attribute in attributes)
		{
			if (
				attribute.AttributeClass is not { } attributeClass
				|| attributeClass.ContainingNamespace.ToDisplayString() != DataAnnotationsNamespace
				|| GetReplacement(attributeClass.Name) is not { } replacement
				|| attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is not { } syntax
			)
				continue;

			context.ReportDiagnostic(
				Diagnostic.Create(
					DiagnosticLibrary.UseTrimSafeCollectionCountAttribute,
					syntax.GetLocation(),
					ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, replacement),
					TrimSuffix(attributeClass.Name),
					memberName,
					replacement
				)
			);
		}
	}

	/// <summary>
	/// The ZodSharp collection-count attribute that replaces a DataAnnotations size attribute.
	/// </summary>
	/// <param name="attributeName">The DataAnnotations attribute class name.</param>
	/// <returns>The replacement attribute name without its suffix, or <see langword="null"/>.</returns>
	internal static string? GetReplacement(string attributeName) =>
		attributeName switch
		{
			"MinLengthAttribute" => "MinCount",
			"MaxLengthAttribute" => "MaxCount",
			"LengthAttribute" => "CountRange",
			_ => null,
		};

	static string TrimSuffix(string name) =>
		name.EndsWith("Attribute", StringComparison.Ordinal)
			? name.Substring(0, name.Length - "Attribute".Length)
			: name;
}

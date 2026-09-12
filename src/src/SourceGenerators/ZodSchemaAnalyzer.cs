using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ZodSchemaAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics =
	[
		DiagnosticLibrary.InvalidLengthAttribute,
		DiagnosticLibrary.UnsupportedLengthAttributeTarget,
		DiagnosticLibrary.InvalidDataAnnotationsErrorMessage,
		DiagnosticLibrary.UnsupportedDataAnnotationsUsage,
		DiagnosticLibrary.CustomValidationMethodNotFound,
		DiagnosticLibrary.CustomValidationInvalidReturnType,
		DiagnosticLibrary.CustomValidationInvalidParameterCount,
		DiagnosticLibrary.CustomValidationInvalidModelParameter,
		DiagnosticLibrary.CustomValidationInvalidCancellationToken,
		DiagnosticLibrary.CustomValidationGenericMethod,
		DiagnosticLibrary.CustomValidationInvalidStaticInstance,
		DiagnosticLibrary.CustomValidationInaccessible,
		DiagnosticLibrary.CustomValidationAmbiguousOverloads,
		DiagnosticLibrary.CustomValidationInvalidMethodName,
		DiagnosticLibrary.CustomValidationAbstractMethod,
		DiagnosticLibrary.CustomValidationUnimplementedPartial,
		DiagnosticLibrary.CustomValidationInvalidParameterModifier,
		DiagnosticLibrary.ComparePropertyNotFound,
		DiagnosticLibrary.DataAnnotationsReferenceNotFound,
		DiagnosticLibrary.SyncValidationInvalidReturnType,
		DiagnosticLibrary.SyncValidationInvalidParameterCount,
		DiagnosticLibrary.SyncValidationInvalidStaticInstance,
		DiagnosticLibrary.SyncValidationInaccessible,
		DiagnosticLibrary.SyncValidationInvalidContextParameter,
		DiagnosticLibrary.IValidateOptionsReferenceNotFound,
		DiagnosticLibrary.IValidateOptionsValueTypeTarget,
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			var hasDataAnnotations = TypeHelpers.HasType(
				compilationContext.Compilation,
				TypeLibrary.System.ComponentModel.DataAnnotations.RequiredAttribute
			);
			var hasIValidateOptions =
				compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Options.IValidateOptions`1")
				is not null;

			compilationContext.RegisterSymbolAction(
				symbolContext => AnalyzeNamedType(symbolContext, hasDataAnnotations, hasIValidateOptions),
				SymbolKind.NamedType
			);
		});
	}

	static void AnalyzeNamedType(SymbolAnalysisContext context, bool hasDataAnnotations, bool hasIValidateOptions)
	{
		if (context.Symbol is not INamedTypeSymbol type)
			return;

		var zodSchemaData = ZodSchemaAttributeData.FromAttributeData(type, out var zodSchemaAttribute);
		if (!zodSchemaData.Exists)
			return;

		var typeLocation = GetTypeLocation(type);

		if (!hasDataAnnotations)
		{
			context.ReportDiagnostic(
				Diagnostic.Create(DiagnosticLibrary.DataAnnotationsReferenceNotFound, typeLocation)
			);
			return;
		}

		var customValidationResult = SourceGenLibrary.ResolveCustomValidationMethod(
			type,
			zodSchemaData,
			zodSchemaAttribute!
		);
		foreach (var diagnosticInfo in customValidationResult.Diagnostics)
			context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());

		var syncValidationResult = SourceGenLibrary.ResolveSyncValidationMethod(
			type,
			zodSchemaData,
			zodSchemaAttribute!
		);
		foreach (var diagnosticInfo in syncValidationResult.Diagnostics)
			context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());

		if (zodSchemaData.GenerateIValidateOptions == true)
		{
			if (!hasIValidateOptions)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(DiagnosticLibrary.IValidateOptionsReferenceNotFound, typeLocation, type.Name)
				);
			}
			else if (type.TypeKind == TypeKind.Struct)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(DiagnosticLibrary.IValidateOptionsValueTypeTarget, typeLocation, type.Name)
				);
			}
		}

		foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
		{
			if (
				property.DeclaredAccessibility != Accessibility.Public
				|| property.IsStatic
				|| property.IsIndexer
				|| !TypeHelpers.HasDataAnnotationAttribute(property)
			)
			{
				continue;
			}

			var propertyResult = SourceGenLibrary.GetValidatablePropertyDescriptor(property);
			foreach (var diagnosticInfo in propertyResult.Diagnostics)
			{
				var diagnostic = diagnosticInfo.ToDiagnostic();
				if (diagnostic.Location == Location.None)
				{
					var propertyLocation = GetMemberLocation(property);
					diagnostic = Diagnostic.Create(
						diagnostic.Descriptor,
						propertyLocation,
						diagnosticInfo.MessageArgs.ToArray()
					);
				}

				context.ReportDiagnostic(diagnostic);
			}
		}
	}

	static Location GetTypeLocation(INamedTypeSymbol type)
	{
		foreach (var location in type.Locations)
		{
			if (location.IsInSource)
				return location;
		}

		return Location.None;
	}

	static Location GetMemberLocation(ISymbol member)
	{
		foreach (var location in member.Locations)
		{
			if (location.IsInSource)
				return location;
		}

		return Location.None;
	}
}

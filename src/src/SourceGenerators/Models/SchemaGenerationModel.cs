using System.Collections.Immutable;
using ZodSharp.SourceGenerators.Models.DataAttributes;

namespace ZodSharp.SourceGenerators.Models;

sealed record SchemaGenerationModel(GenerationContext<SchemaGenerationCapabilities> Context)
{
	public EquatableArray<GeneratorResult<ZodSchemaDescriptor>> ZodSchemas { get; init; } = [];
}

sealed record SchemaGenerationCapabilities : IGenerationCapabilities
{
	public bool HasRequiredAttribute { get; init; }

	public bool HasIValidateOptions { get; init; }
}

readonly record struct SchemaSet(EquatableArray<ZodSchemaDescriptor> Schemas);

enum PropertyValidationKind
{
	String,
	Numeric,
	Comparable,
	Collection,
	Complex,
	Unsupported,
}

readonly record struct LengthAccessor(string LengthExpression, string Origin, bool IsSupported);

/// <param name="TargetType">This is the target of the attribute, the type where the attribute was defined.</param>
/// <param name="SchemaType">This is the schema type, the one that will be generated.</param>
/// <param name="TargetCanBeNull">Indicates whether the target type can be null.</param>
/// <param name="ContainingTypes">The containing types of the target type, if it is a nested type.</param>
/// <param name="TargetAccessibility">The accessibility of the target type, if it is a type declaration.</param>
/// <param name="IsValueType">Indicates whether the target type is a struct.</param>
/// <param name="Properties">The validatable properties of the target type that will be included in the schema.</param>
/// <param name="CustomValidationMethod">The custom validation method data, if any.</param>
/// <param name="SyncValidationMethod">The synchronous refinement method data, if any.</param>
/// <param name="GenerateIValidateOptions">Requested IValidateOptions generation: null = auto, true = force, false = opt out.</param>
/// <param name="EnableComposition">Whether the value-first composition methods (ApplyAnd/ApplyOr/ApplyRefine) are generated.</param>
/// <param name="GenerateValidateMethod">Whether the static <c>Validate</c> method is generated.</param>
/// <param name="GenerateParseMethod">Whether the static <c>Parse</c> method is generated (requires <c>Validate</c>).</param>
/// <param name="TypeRules">
/// Rules bound to the target type itself (type-level <c>[ZodRule]</c>-mapped attributes). They are
/// evaluated against the whole value with an empty path.
/// </param>
/// <param name="IsPrimary">True if this is the primary schema for the target type, false if it is a secondary schema.</param>
readonly record struct ZodSchemaDescriptor(
	TypeIdentity TargetType,
	TypeIdentity SchemaType,
	bool TargetCanBeNull,
	EquatableArray<TypeDeclarationOptions> ContainingTypes,
	TypeDeclarationAccessibility? TargetAccessibility,
	bool IsValueType,
	EquatableArray<GeneratorResult<ZodPropertyDescriptor>> Properties,
	GeneratorResult<CustomValidationMethodData> CustomValidationMethod,
	GeneratorResult<SyncValidationMethodData> SyncValidationMethod,
	bool? GenerateIValidateOptions,
	bool EnableComposition,
	bool GenerateValidateMethod,
	bool GenerateParseMethod,
	EquatableArray<CustomRuleDescriptor> TypeRules,
	bool IsPrimary
);

readonly record struct ZodPropertyDescriptor(
	TypeIdentity PropertyType,
	string Name,
	string DisplayName,
	bool CanBeNull,
	bool IsEnum,
	PropertyValidationKind ValidationKind,
	TypeIdentity? ElementType,
	bool ElementTypeCanBeNull,
	TypeIdentity? NestedSchemaType,
	LengthAccessor LengthAccessor,
	bool CompareViaCompareTo,
	ValidationAttributes ValidationAttributes,
	EquatableArray<CustomRuleDescriptor> CustomRules
);

[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Naming",
	"PDS0004:Use correct acronym capitalization",
	Justification = "Real names"
)]
readonly record struct ValidationAttributes(
	GeneratorResult<RequiredAttributeData> Required,
	GeneratorResult<CompareAttributeData> Compare,
	GeneratorResult<DisplayAttributeData> Display,
	GeneratorResult<EmailAddressAttributeData> EmailAddress,
	GeneratorResult<CreditCardAttributeData> CreditCard,
	GeneratorResult<PhoneAttribute> Phone,
	GeneratorResult<UrlAttribute> Url,
	GeneratorResult<StringLengthAttribute> StringLength,
	GeneratorResult<MinLengthAttributeData> MinLength,
	GeneratorResult<MaxLengthAttributeData> MaxLength,
	GeneratorResult<RegularExpressionAttributeData> RegularExpression,
	GeneratorResult<Base64StringAttributeData> Base64String,
	GeneratorResult<DeniedValuesAttributeData> DeniedValues,
	GeneratorResult<AllowedValuesAttributeData> AllowedValues,
	GeneratorResult<LengthAttributeData> Length,
	GeneratorResult<RangeAttributeData> Range
)
{
	public bool HasDiagnostics =>
		Required.HasDiagnostics
		|| Compare.HasDiagnostics
		|| Display.HasDiagnostics
		|| EmailAddress.HasDiagnostics
		|| CreditCard.HasDiagnostics
		|| Phone.HasDiagnostics
		|| Url.HasDiagnostics
		|| StringLength.HasDiagnostics
		|| MinLength.HasDiagnostics
		|| MaxLength.HasDiagnostics
		|| RegularExpression.HasDiagnostics
		|| Base64String.HasDiagnostics
		|| DeniedValues.HasDiagnostics
		|| AllowedValues.HasDiagnostics
		|| Length.HasDiagnostics
		|| Range.HasDiagnostics;

	public ImmutableArray<ReportableDiagnostic> GetDiagnostics() =>
		[
			.. Required.Diagnostics,
			.. Compare.Diagnostics,
			.. Display.Diagnostics,
			.. EmailAddress.Diagnostics,
			.. CreditCard.Diagnostics,
			.. Phone.Diagnostics,
			.. Url.Diagnostics,
			.. StringLength.Diagnostics,
			.. MinLength.Diagnostics,
			.. MaxLength.Diagnostics,
			.. RegularExpression.Diagnostics,
			.. Base64String.Diagnostics,
			.. DeniedValues.Diagnostics,
			.. AllowedValues.Diagnostics,
			.. Length.Diagnostics,
			.. Range.Diagnostics,
		];
}

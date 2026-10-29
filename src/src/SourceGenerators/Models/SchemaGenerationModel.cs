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

	/// <summary>Whether <c>Microsoft.Extensions.Options.IAsyncValidateOptions&lt;T&gt;</c> (.NET 11) resolves.</summary>
	public bool HasIAsyncValidateOptions { get; init; }

	/// <summary>Whether <c>Microsoft.Extensions.DependencyInjection.IServiceCollection</c> resolves.</summary>
	public bool HasServiceCollection { get; init; }

	/// <summary>The compilation's assembly name, used to name the per-assembly registration.</summary>
	public string AssemblyName { get; init; } = string.Empty;
}

/// <summary>
/// How a member typed as a C# 15 union or a closed class hierarchy is validated: by dispatching on the
/// runtime type of the value (the union's active case, or the derived type) to that type's schema.
/// </summary>
enum DispatchKind
{
	/// <summary>No dispatch.</summary>
	None,

	/// <summary>The member is a C# 15 union; dispatch on its <c>Value</c>.</summary>
	Union,

	/// <summary>The member is typed as a closed class; dispatch on the derived type.</summary>
	ClosedHierarchy,
}

/// <param name="CaseType">The case (or derived) type.</param>
/// <param name="SchemaType">The generated schema of <paramref name="CaseType"/>.</param>
/// <param name="IsModern">Whether the schema exposes the depth-aware and member-resolution entry points.</param>
readonly record struct DispatchCase(TypeIdentity CaseType, TypeIdentity SchemaType, bool IsModern);

readonly record struct SchemaSet(EquatableArray<ZodSchemaDescriptor> Schemas);

enum PropertyValidationKind
{
	String,
	Numeric,
	Comparable,
	Collection,
	Complex,
	Enum,
	Unsupported,
}

enum DefaultImmutableArrayBehavior
{
	ValidationError,
	TreatAsEmpty,
}

/// <summary>
/// How a string-serialized enum's member names are transformed, mirroring the
/// <c>JsonNamingPolicy</c> a <c>JsonStringEnumConverter</c> may be constructed with.
/// </summary>
enum EnumJsonNamingPolicy
{
	/// <summary>Member names are used as declared.</summary>
	Unchanged,

	/// <summary>Member names are camelCased, matching <c>JsonNamingPolicy.CamelCase</c>.</summary>
	CamelCase,
}

/// <summary>
/// The DateTimeOffset wire form the generator assumes, mirroring the public
/// <c>ZodSharp.DateTimeOffsetSerialization</c> enum. The generator cannot reference the core assembly, so the
/// values are mapped by their numeric identity.
/// </summary>
enum DateTimeOffsetSerialization
{
	/// <summary>The wire form keeps the value's offset.</summary>
	PreserveOffset = 0,

	/// <summary>The wire form is UTC: a <c>Z</c> or zero offset.</summary>
	Utc = 1,
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
/// <param name="RefinementHook">The <c>OnZodValidate</c> refinement hook data, if any.</param>
/// <param name="GenerateIValidateOptions">Requested IValidateOptions generation: null = auto, true = force, false = opt out.</param>
/// <param name="EnableComposition">Whether the value-first composition methods (ApplyAnd/ApplyOr/ApplyRefine) are generated.</param>
/// <param name="GenerateValidateMethod">Whether the static <c>Validate</c> method is generated.</param>
/// <param name="GenerateParseMethod">Whether the static <c>Parse</c> method is generated (requires <c>Validate</c>).</param>
/// <param name="TypeRules">
/// Rules bound to the target type itself (type-level <c>[ZodRule]</c>-mapped attributes). They are
/// evaluated against the whole value with an empty path.
/// </param>
/// <param name="ValidateEnumValues">
/// Whether the automatic enum validation is emitted for the schema's enum properties. On by default; the
/// <c>[ZodSchema]</c> attribute opts out.
/// </param>
/// <param name="IsPrimary">True if this is the primary schema for the target type, false if it is a secondary schema.</param>
/// <param name="ScalarUnderlyingType">
/// When the target is a Purview.ValueObjects scalar, the underlying value type whose JSON wire form the
/// scalar serialises as (for example a <c>Guid</c> scalar serialises as a canonical Guid string). Null
/// otherwise.
/// </param>
/// <param name="ScalarUnderlyingCanBeNull">
/// Whether the scalar's underlying value may be null (a nullable reference or nullable value scalar).
/// </param>
/// <param name="GenerateJsonSchema">
/// Whether the generated schema exports JSON Schema (the <c>BuildJsonSchema</c> method and the
/// <c>IJsonSchemaProvider</c> implementation on the adapter). Controlled by
/// <c>[ZodSchema(GenerateJsonSchema = false)]</c> and the assembly-level <c>ZodSharpGenerateJsonSchemas</c>
/// switch.
/// </param>
/// <param name="EnumAsString">
/// Whether enums in this schema are serialized as strings. Set by the assembly-level
/// <c>ZodSharpEnumSerialization=String</c> switch (for a global <c>JsonStringEnumConverter</c>); a property
/// can also opt in with a per-type/property <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c>.
/// </param>
/// <param name="EnumNamingPolicy">
/// How a string-serialized enum's member names are transformed, set by the assembly-level
/// <c>ZodSharpEnumNamingPolicy</c> switch.
/// </param>
/// <param name="DateTimeOffsetSerialization">
/// The type-level <c>[DateTimeOffsetBehavior]</c> override for the wire form assumed by JSON Schema
/// generation, or <see langword="null"/> when unset. The generation-settings pass resolves it against the
/// assembly/MSBuild default.
/// </param>
readonly record struct ZodSchemaDescriptor(
	TypeIdentity TargetType,
	TypeIdentity SchemaType,
	bool TargetCanBeNull,
	EquatableArray<TypeDeclarationOptions> ContainingTypes,
	TypeDeclarationAccessibility? TargetAccessibility,
	bool IsValueType,
	EquatableArray<GeneratorResult<ZodPropertyDescriptor>> Properties,
	GeneratorResult<CustomValidationMethodData> CustomValidationMethod,
	GeneratorResult<ZodRefinementHookData> RefinementHook,
	bool? GenerateIValidateOptions,
	bool EnableComposition,
	bool GenerateValidateMethod,
	bool GenerateParseMethod,
	EquatableArray<CustomRuleDescriptor> TypeRules,
	bool ValidateEnumValues,
	bool IsPrimary,
	TypeIdentity? ScalarUnderlyingType,
	bool ScalarUnderlyingCanBeNull,
	bool GenerateJsonSchema,
	bool EnumAsString,
	EnumJsonNamingPolicy EnumNamingPolicy,
	DateTimeOffsetSerialization? DateTimeOffsetSerialization = null
)
{
	/// <summary>Whether the target implements <c>System.ComponentModel.DataAnnotations.IValidatableObject</c>.</summary>
	public bool ImplementsValidatableObject { get; init; }

	/// <summary>Whether the target implements <c>System.ComponentModel.DataAnnotations.IAsyncValidatableObject</c> (.NET 11).</summary>
	public bool ImplementsAsyncValidatableObject { get; init; }

	/// <summary>
	/// Whether a hand-authored <c>[OptionsValidator]</c> class named <c>{Type}Validator</c> exists, so the generated
	/// options validator is skipped (SYSLIB1205).
	/// </summary>
	public bool HasOptionsValidatorConflict { get; init; }
}

/// <param name="PropertyType">The declared type of the property, with a nullable wrapper removed.</param>
/// <param name="Name">The name of the property.</param>
/// <param name="DisplayName">The name reported in error messages, honouring a <c>[Display]</c> attribute.</param>
/// <param name="CanBeNull">Indicates whether the property type can hold null at runtime (a reference type or <c>Nullable&lt;T&gt;</c>).</param>
/// <param name="IsDeclaredNullable">
/// Indicates whether the property is declared nullable (the nullable reference-type annotation, or
/// <c>Nullable&lt;T&gt;</c>). This governs null acceptance in validation and the exported JSON Schema: a
/// non-nullable reference type is required and its rules run unconditionally and reject null.
/// </param>
/// <param name="IsNullableValueType">Indicates whether the property type is a <c>Nullable&lt;T&gt;</c>.</param>
/// <param name="IsEnum">Indicates whether the property type is an enum.</param>
/// <param name="IsFlagsEnum">Indicates whether the enum type is declared with the <c>[Flags]</c> attribute.</param>
/// <param name="IgnoredEnumMembers">
/// The enum members marked with <c>[ZodIgnore]</c>, rendered as member expressions. Empty for a non-enum or
/// flags property.
/// </param>
/// <param name="ValidationKind">The kind of validation the property supports.</param>
/// <param name="ElementType">
/// The collection element type — the map value type when the property is a dictionary — when the property is
/// a collection. A nullable value type is unwrapped, so <c>List&lt;int?&gt;</c> carries <c>int</c>;
/// <paramref name="ElementTypeIsNullableValueType"/> records the wrapper.
/// </param>
/// <param name="ElementTypeCanBeNull">Indicates whether a collection element (or map value) can be null.</param>
/// <param name="ElementTypeIsNullableValueType">
/// Indicates whether the collection element (or map value) is a <c>Nullable&lt;T&gt;</c>, so generated
/// validation has to read <c>.Value</c> when it passes the item to a nested schema.
/// </param>
/// <param name="IsMap">
/// Indicates whether the property is a dictionary (<c>IDictionary&lt;TKey, TValue&gt;</c> /
/// <c>IReadOnlyDictionary&lt;TKey, TValue&gt;</c>). A map is validated per value, keys are used only for
/// error paths, and the exported JSON Schema is an object with <c>additionalProperties</c>.
/// </param>
/// <param name="AllowNulls">
/// Whether the collection may contain null elements (or a map null values), when the property is a
/// collection. Before the generation-settings pass this holds the property/type-level override (null when
/// neither states one); that pass resolves the assembly default into a concrete value, so the generator always
/// sees a non-null value for a collection property.
/// </param>
/// <param name="NestedSchemaType">The generated schema type of a nested complex type, when there is one.</param>
/// <param name="LengthAccessor">The length expression and origin, when the property's length is countable.</param>
/// <param name="CompareViaCompareTo">Indicates whether ranges compare through <c>IComparable</c>.</param>
/// <param name="ValidationAttributes">The DataAnnotations attributes declared on the property.</param>
/// <param name="CustomRules">The rules bound to the property through <c>[ZodRule]</c>-mapped attributes.</param>
/// <param name="JsonPropertyName">
/// The explicit <c>[JsonPropertyName]</c> value, when one is declared. Used to name the property in the
/// exported JSON Schema so it matches the System.Text.Json wire form.
/// </param>
/// <param name="NestedSchemaExportsJsonSchema">
/// Whether the nested complex type's generated schema exports JSON Schema. When <see langword="false"/> the
/// parent's schema cannot reference it and falls back to an empty schema (reported as ZODSGEN045).
/// </param>
/// <param name="EnumSerializedAsString">
/// Whether the property's enum is serialized as a string (a per-type or per-property
/// <c>[JsonConverter(typeof(JsonStringEnumConverter))]</c>). The assembly-level switch is applied separately.
/// </param>
/// <param name="EnumJsonNames">
/// For a non-flags enum, the JSON names of the allowed members, in declaration order: the member name, or its
/// <c>[JsonStringEnumMemberName]</c> value when declared. Used when the enum is serialized as a string.
/// </param>
/// <param name="DateTimeOffsetSerialization">
/// The property-level <c>[DateTimeOffsetBehavior]</c> override, or <see langword="null"/> when unset. The
/// generation-settings pass resolves it against the type and assembly/MSBuild defaults.
/// </param>
readonly record struct ZodPropertyDescriptor(
	TypeIdentity PropertyType,
	string Name,
	string DisplayName,
	bool CanBeNull,
	bool IsDeclaredNullable,
	bool IsNullableValueType,
	bool IsEnum,
	bool IsFlagsEnum,
	EquatableArray<string> IgnoredEnumMembers,
	PropertyValidationKind ValidationKind,
	TypeIdentity? ElementType,
	bool ElementTypeCanBeNull,
	bool ElementTypeIsNullableValueType,
	bool IsMap,
	bool? AllowNulls,
	TypeIdentity? NestedSchemaType,
	LengthAccessor LengthAccessor,
	bool CompareViaCompareTo,
	ValidationAttributes ValidationAttributes,
	EquatableArray<CustomRuleDescriptor> CustomRules,
	string? JsonPropertyName,
	bool NestedSchemaExportsJsonSchema,
	bool EnumSerializedAsString,
	EquatableArray<string> EnumJsonNames,
	DateTimeOffsetSerialization? DateTimeOffsetSerialization = null
)
{
	public bool IsImmutableArray { get; init; }

	public DefaultImmutableArrayBehavior? DefaultImmutableArrayBehavior { get; init; }

	/// <summary>
	/// The expression that reads a localized display name (<c>[Display(Name = ..., ResourceType = ...)]</c>), or
	/// <see langword="null"/> when the display name is a literal.
	/// </summary>
	public string? DisplayNameResourceExpression { get; init; }

	/// <summary>
	/// Whether the nested schema exposes the depth-aware, async and member-resolution entry points. Always true
	/// for a schema generated into this compilation; false for one generated by an older ZodSharp.
	/// </summary>
	public bool NestedSchemaIsModern { get; init; } = true;

	/// <summary>
	/// Whether the nested complex type is a Purview.ValueObjects scalar, so its errors are named after this member (its
	/// display name replaces the scalar's own <c>Value</c>).
	/// </summary>
	public bool NestedIsScalarValueObject { get; init; }

	/// <summary>Whether an external nested schema reports <c>RequiresAsyncValidation</c>.</summary>
	public bool ExternalNestedSchemaRequiresAsync { get; init; }

	/// <summary>
	/// The construction expressions of the <c>AsyncValidationAttribute</c>s (.NET 11) declared on the member, in
	/// declaration order.
	/// </summary>
	public EquatableArray<string> AsyncValidationAttributes { get; init; } = new([]);

	/// <summary>How the member dispatches to a case or derived type's schema, when it does.</summary>
	public DispatchKind DispatchKind { get; init; }

	/// <summary>The cases dispatched to when <see cref="DispatchKind"/> is not <see cref="DispatchKind.None"/>.</summary>
	public EquatableArray<DispatchCase> DispatchCases { get; init; } = new([]);
}

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

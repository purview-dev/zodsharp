namespace ZodSharp.SourceGenerators.Helpers;

partial class TypeLibraryGenerator
{
	public const string SystemDataAnnotations = "System.ComponentModel.DataAnnotations";

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity DisplayAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity RequiredAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity EmailAddressAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity StringLengthAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity MinLengthAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity MaxLengthAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity RangeAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity LengthAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity RegularExpressionAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity AllowedValuesAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity DeniedValuesAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Naming",
		"PDS0004:Use correct acronym capitalization",
		Justification = "Real name"
	)]
	static readonly TypeIdentity UrlAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity PhoneAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity CreditCardAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity CompareAttribute = default;

	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity Base64StringAttribute = default;

	// This is abstract and the base class to all the other validation attributes,
	// so we can use it to get the base properties
	[TypeRef(SystemDataAnnotations)]
	static readonly TypeIdentity ValidationAttribute = default;
}

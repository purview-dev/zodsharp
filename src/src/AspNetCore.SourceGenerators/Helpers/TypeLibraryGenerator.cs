namespace ZodSharp.AspNetCore.SourceGenerators.Helpers;

[GenerateTypeLibrary]
static partial class TypeLibraryGenerator
{
	[TypeRef("ZodSharp.AspNetCore")]
	static readonly TypeIdentity ErrorType = default;

	[TypeRef("ZodSharp.AspNetCore")]
	static readonly TypeIdentity ErrorTypeAttribute = default;

	[TypeRef("ZodSharp.Core")]
	static readonly TypeIdentity ValidationError = default;

	[TypeRef("ZodSharp.Core")]
	static readonly TypeIdentity ZodException = default;

	[TypeRef("System.Diagnostics.CodeAnalysis")]
	static readonly TypeIdentity DoesNotReturnAttribute = default;
}

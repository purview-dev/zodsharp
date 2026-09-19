namespace ZodSharp.AspNetCore.SourceGenerators.Helpers;

/// <summary>
/// Type identities used by the <see cref="ErrorTypeGenerator"/> when emitting source.
/// </summary>
static class TypeLibrary
{
	public static class AspNetCore
	{
		public static readonly TypeIdentity ErrorType = new("ErrorType", "ZodSharp.AspNetCore");

		public static readonly TypeIdentity ErrorTypeAttribute = new("ErrorTypeAttribute", "ZodSharp.AspNetCore");
	}

	public static class Core
	{
		public static readonly TypeIdentity ValidationError = new("ValidationError", "ZodSharp.Core");

		public static readonly TypeIdentity ZodException = new("ZodException", "ZodSharp.Core");
	}

	public static readonly TypeIdentity DoesNotReturnAttribute = new(
		"DoesNotReturnAttribute",
		"System.Diagnostics.CodeAnalysis"
	);
}

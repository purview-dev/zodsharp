namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Where the ZodSharp resolver is placed in <c>ValidationOptions.Resolvers</c>.
/// </summary>
public enum ZodResolverPlacement
{
	/// <summary>
	/// First, ahead of the Microsoft.Extensions.Validation generated (DataAnnotations) resolvers, so a type with
	/// a ZodSharp validator is always validated by ZodSharp.
	/// </summary>
	First,

	/// <summary>
	/// After the generated resolvers and before the runtime parameter resolvers they append, so a type the
	/// Microsoft.Extensions.Validation generator knows is validated with DataAnnotations and ZodSharp handles
	/// the rest.
	/// </summary>
	AfterGenerated,
}

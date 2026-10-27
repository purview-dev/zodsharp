using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Places the ZodSharp resolver in <see cref="ValidationOptions.Resolvers"/>.
/// </summary>
/// <remarks>
/// Runs as a post-configure step, after every <c>AddValidation</c> configuration. The
/// Microsoft.Extensions.Validation generator's interceptor inserts its generated resolver at index 0 inside its
/// own configure step, so inserting afterwards is what keeps ZodSharp first regardless of whether the
/// application calls <c>AddValidation()</c> before, after, or not at all.
/// </remarks>
sealed class ZodValidationOptionsSetup(ZodValidatableInfoResolver resolver) : IPostConfigureOptions<ValidationOptions>
{
	const string RuntimeParameterResolverName = "RuntimeValidatableParameterInfoResolver";

	public void PostConfigure(string? name, ValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var resolvers = options.Resolvers;
		if (!resolvers.Contains(resolver))
		{
			if (resolver.Options.Placement == ZodResolverPlacement.AfterGenerated)
				resolvers.Insert(FindAfterGeneratedIndex(resolvers), resolver);
			else
				resolvers.Insert(0, resolver);
		}

		resolver.AttachTo(options);
	}

	static int FindAfterGeneratedIndex(IList<IValidatableInfoResolver> resolvers)
	{
		// The generator inserts its type resolvers at the front and appends its runtime parameter resolvers;
		// "after generated" is therefore just ahead of the first runtime parameter resolver.
		for (var i = 0; i < resolvers.Count; i++)
		{
			// The generated classes are file-local, so their runtime names carry a file-scoped prefix.
			if (resolvers[i].GetType().Name.EndsWith(RuntimeParameterResolverName, StringComparison.Ordinal))
				return i;
		}

		return resolvers.Count;
	}
}

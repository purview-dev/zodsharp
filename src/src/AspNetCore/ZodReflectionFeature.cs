#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The <c>ZodSharp.Reflection.IsSupported</c> feature switch, shared by every ZodSharp package, which guards the
/// reflection fallbacks that walk a model the library was not told about (here: walking an assembly reference graph for generated validators).
/// </summary>
/// <remarks>
/// The property is a feature guard for <see cref="RequiresUnreferencedCodeAttribute"/>: the trim analyzer accepts the
/// reflection it guards, and ILLink and ILC treat it as <see langword="false"/> when trimming or compiling ahead of time,
/// so the fallback is removed rather than failing unpredictably on trimmed metadata. An untrimmed application keeps it
/// unless it sets the switch to <see langword="false"/>.
/// </remarks>
static class ZodReflectionFeature
{
	/// <summary>The feature switch name.</summary>
	public const string SwitchName = "ZodSharp.Reflection.IsSupported";

	/// <summary>Gets whether the reflection fallbacks may run.</summary>
	[FeatureSwitchDefinition(SwitchName)]
	[FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL4000:FeatureGuard",
		Justification = "A RequiresUnreferencedCode feature guard cannot be derived from another guard: none exists. ILLink and ILC "
			+ "treat this guard as false when trimming, which the trim verification harness proves (no IL2026 from the "
			+ "guarded calls)."
	)]
	public static bool IsSupported => !AppContext.TryGetSwitch(SwitchName, out var enabled) || enabled;
}
#else
namespace ZodSharp.AspNetCore;

/// <summary>The <c>ZodSharp.Reflection.IsSupported</c> switch; see the .NET 9+ declaration.</summary>
static class ZodReflectionFeature
{
	public const string SwitchName = "ZodSharp.Reflection.IsSupported";

	public static bool IsSupported => !AppContext.TryGetSwitch(SwitchName, out var enabled) || enabled;
}
#endif

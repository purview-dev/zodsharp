using System.Diagnostics.CodeAnalysis;

namespace ZodSharp.Core;

/// <summary>
/// The <c>ZodSharp.Reflection.IsSupported</c> feature switch, which guards the reflection fallbacks that read members
/// of a type the library was not told about (for example a discriminator on a POCO whose type was not supplied to
/// <c>ZodDiscriminatedUnionBuilder.Option&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// The property is a feature guard for <see cref="RequiresUnreferencedCodeAttribute"/>: the trim analyzer accepts the
/// reflection it guards, and ILLink and ILC treat it as <see langword="false"/> when trimming or compiling ahead of
/// time, so the fallback is removed instead of failing unpredictably on trimmed metadata. An untrimmed application
/// keeps it unless it sets the switch to <see langword="false"/>.
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
			+ "guarded calls, and the fallback is observed disabled in the published binary)."
	)]
	public static bool IsSupported => !AppContext.TryGetSwitch(SwitchName, out var enabled) || enabled;
}

#if NET11_0_OR_GREATER
namespace ZodSharp.AspNetCore.ClientValidation;

/// <summary>
/// The trimming and Native AOT requirement of static-SSR client validation.
/// </summary>
/// <remarks>
/// Static SSR client validation needs Razor components (<c>AddRazorComponents</c>), which ASP.NET Core annotates
/// <c>[RequiresUnreferencedCode]</c>/<c>[RequiresDynamicCode]</c>, and it reads the rule attributes on form model
/// properties by reflection, which a trimmer cannot follow. The requirement is declared rather than suppressed, so a
/// trimmed or AOT application is warned at the call site.
/// </remarks>
static class ZodClientValidationTrimming
{
	public const string Message =
		"Static-SSR client validation reflects over form model properties and [ZodRule] attributes, and requires "
		+ "Razor components (AddRazorComponents), which are not trim- or Native AOT-compatible. Client rules only "
		+ "improve the user experience and the server still validates; in a trimmed or AOT application omit it, or "
		+ "implement IClientValidationRuleProvider on the attribute using ZodClientValidationRules.For(IZodRule).";
}
#endif

using ZodSharp.Core;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Configures how ZodSharp participates in Microsoft.Extensions.Validation.
/// </summary>
public sealed class ZodValidationOptions
{
	/// <summary>
	/// Gets or sets where the ZodSharp resolver is placed relative to the generated DataAnnotations resolvers.
	/// Defaults to <see cref="ZodResolverPlacement.First"/>.
	/// </summary>
	public ZodResolverPlacement Placement { get; set; } = ZodResolverPlacement.First;

	/// <summary>
	/// Gets or sets whether a type that has a ZodSharp validator is also validated by the next resolver that
	/// knows it (usually the generated DataAnnotations resolver), ZodSharp first, with identical
	/// <c>(Path, ErrorMessage)</c> pairs reported once. Defaults to <see langword="false"/>: generated ZodSharp
	/// validators already apply DataAnnotations, so running both duplicates every message.
	/// </summary>
	public bool AlsoRunDataAnnotations { get; set; }

	/// <summary>
	/// Gets or sets how member names are rendered in <c>ValidationError.Path</c>. <see langword="null"/> (the
	/// default) renders C# member names, which is what Microsoft.Extensions.Validation itself produces, so a
	/// ZodSharp type and a DataAnnotations type in the same API produce the same key shape. Set, for example,
	/// <c>ZodPathNamer.FromFunc(JsonNamingPolicy.CamelCase.ConvertName)</c> for JSON-shaped keys. Blazor ignores
	/// <c>Path</c>, so this never affects field placement.
	/// </summary>
	public IZodPathNamer? PathNamer { get; set; }

	/// <summary>
	/// Gets or sets the formatter that produces <c>ValidationError.ErrorMessage</c>. When <see langword="null"/>
	/// (the default), a <see cref="StringLocalizerZodMessageFormatter"/> is used whenever the validation's
	/// service provider resolves an <c>IStringLocalizerFactory</c> (the same trigger Microsoft.Extensions.Validation
	/// uses), and the error's own message otherwise.
	/// </summary>
	public IZodMessageFormatter? MessageFormatter { get; set; }

	/// <summary>
	/// Gets explicit, trim-safe validator sources, consulted before <see cref="IZodSchemaFactory"/>.
	/// </summary>
	public IList<IZodValidatableInfoSource> Sources { get; } = [];
}

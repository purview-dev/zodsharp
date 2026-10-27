using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// Configures the Blazor-specific behaviour of ZodSharp: <see cref="ZodValidator"/> and the
/// <c>EditContext</c> extensions.
/// </summary>
public sealed class ZodBlazorOptions
{
	/// <summary>
	/// Gets or sets the formatter that renders the messages <see cref="ZodValidator"/> places on fields. When
	/// <see langword="null"/> (the default), the bridge's <c>ZodValidationOptions.MessageFormatter</c> is used, then a
	/// <c>StringLocalizerZodMessageFormatter</c> when an <c>IStringLocalizerFactory</c> is registered
	/// (<c>AddLocalization()</c>), and otherwise each error's own message.
	/// </summary>
	public IZodMessageFormatter? MessageFormatter { get; set; }

	/// <summary>
	/// Gets or sets whether <see cref="ZodValidator"/> logs a one-time warning when it shares a form with
	/// <c>DataAnnotationsValidator</c>. Defaults to <see langword="true"/>.
	/// </summary>
	public bool WarnOnValidatorCoexistence { get; set; } = true;
}

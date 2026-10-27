using System.Globalization;

namespace ZodSharp.Core;

/// <summary>
/// Renders the client-facing message of a <see cref="ValidationError"/> at a host boundary (ProblemDetails,
/// Microsoft.Extensions.Validation, Blazor, options failures). This is the localization hook: validation itself
/// stays culture-independent, and a host re-renders each message in the request culture.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must never throw and must fall back to <see cref="ValidationError.Message"/> when they have
/// nothing better, so a missing resource can never turn a validation failure into an exception.
/// </para>
/// <para>
/// <see cref="ValidationError.Code"/>, <see cref="ValidationError.Category"/>, paths and parameter values are
/// invariant data and are never localized; only the message is.
/// </para>
/// </remarks>
public interface IZodMessageFormatter
{
	/// <summary>
	/// Returns the message for <paramref name="validationError"/> in <paramref name="culture"/>.
	/// </summary>
	/// <param name="validationError">The error.</param>
	/// <param name="culture">The culture to render in, normally <see cref="CultureInfo.CurrentUICulture"/>.</param>
	/// <returns>The message.</returns>
	string Format(in ValidationError validationError, CultureInfo culture);
}

/// <summary>
/// The default <see cref="IZodMessageFormatter"/>: re-formats <see cref="ValidationError.MessageFormat"/> in the
/// requested culture when every placeholder resolves, and otherwise returns <see cref="ValidationError.Message"/>
/// unchanged. It has no dependency on <c>Microsoft.Extensions.Localization</c>.
/// </summary>
public sealed class DefaultZodMessageFormatter : IZodMessageFormatter
{
	/// <summary>
	/// Gets the shared instance.
	/// </summary>
	public static DefaultZodMessageFormatter Instance { get; } = new();

	/// <inheritdoc/>
	public string Format(in ValidationError validationError, CultureInfo culture)
	{
		ArgumentNullException.ThrowIfNull(culture);

		return
			validationError.MessageFormat is { Length: > 0 } format
			&& ZodMessageFormat.TryFormatComplete(format, validationError, out var message, culture)
			? message
			: validationError.Message ?? string.Empty;
	}
}

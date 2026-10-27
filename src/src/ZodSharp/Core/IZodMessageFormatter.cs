using System.Globalization;

namespace ZodSharp.Core;

/// <summary>
/// Produces the user-facing message for a <see cref="ValidationError"/> at a host boundary (ProblemDetails,
/// Microsoft.Extensions.Validation, Blazor), so messages can be localized without validation itself depending
/// on a culture or a localization framework.
/// </summary>
public interface IZodMessageFormatter
{
	/// <summary>
	/// Formats <paramref name="error"/> for <paramref name="culture"/>.
	/// </summary>
	/// <param name="error">The error.</param>
	/// <param name="culture">The culture to format for, usually <see cref="CultureInfo.CurrentUICulture"/>.</param>
	/// <returns>The message.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Naming",
		"CA1716:Identifiers should not match keywords",
		Justification = "'error' names a ValidationError throughout the public API; VB's Error keyword is not a concern."
	)]
	string Format(in ValidationError error, CultureInfo culture);
}

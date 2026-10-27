using System.Text;
using ZodSharp.Core;

namespace ZodSharp.Options;

/// <summary>
/// The settings recorded when an options type is registered for ZodSharp validation through an
/// <c>OptionsBuilder&lt;T&gt;</c>, keyed by options type and name.
/// </summary>
/// <param name="OptionsType">The options type.</param>
/// <param name="Name">The options name.</param>
/// <param name="ConfigurationSection">The configuration section the options are bound from, if known.</param>
/// <param name="MissingValidatorBehavior">How to behave when no validator is registered.</param>
/// <param name="NameFilter">When set, only this options name is validated.</param>
sealed record ZodOptionsValidationRegistration(
	Type OptionsType,
	string Name,
	string? ConfigurationSection,
	MissingValidatorBehavior MissingValidatorBehavior,
	string? NameFilter
);

/// <summary>
/// Renders options validation failures: <c>{name}: {location}: {message} ({code})</c>.
/// </summary>
static class ZodOptionsFailureFormatter
{
	public static string Format(string? optionsName, string? configurationSection, in ValidationError error)
	{
		StringBuilder builder = new();
		if (!string.IsNullOrEmpty(optionsName))
			builder.Append(optionsName).Append(": ");

		string location;
		if (!string.IsNullOrEmpty(configurationSection))
		{
			location = error.FullPath.IsRoot
				? configurationSection
				: configurationSection + ":" + error.FullPath.ToConfigurationKey();
		}
		else
		{
			location = error.FullPath.ToDottedString();
		}

		builder
			.Append(location.Length == 0 ? "(root)" : location)
			.Append(": ")
			.Append(error.Message)
			.Append(" (")
			.Append(error.Code)
			.Append(')');

		return builder.ToString();
	}
}

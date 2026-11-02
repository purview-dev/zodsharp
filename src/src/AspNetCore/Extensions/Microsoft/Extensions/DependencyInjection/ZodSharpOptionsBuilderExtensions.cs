using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ZodSharp.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configuration-aware options validation for ZodSharp.
/// </summary>
public static class ZodSharpOptionsBuilderExtensions
{
	const string BindRequirement =
		"Binds through the reflection-based configuration binder. Call BindConfiguration(section) (intercepted by the configuration binder source generator) and ValidateWithZod(section) instead.";

	/// <summary>
	/// Binds <typeparamref name="TOptions"/> to the configuration section <paramref name="configSectionPath"/> and
	/// validates it with its ZodSharp schema. Failures name the configuration key of the offending setting
	/// (<c>Smtp:Servers:0:Host: Host is required (missing_field)</c>), so it can be found in <c>appsettings.json</c>.
	/// </summary>
	/// <remarks>
	/// Binding uses the reflection-based configuration binder. A trimmed or Native AOT application should call
	/// <c>BindConfiguration(section)</c> itself (the configuration binder source generator intercepts that call)
	/// followed by <c>ValidateWithZod(section)</c>.
	/// </remarks>
	/// <typeparam name="TOptions">The options type.</typeparam>
	/// <param name="builder">The options builder.</param>
	/// <param name="configSectionPath">The configuration section path, for example <c>Smtp</c>.</param>
	/// <param name="validateOnStart">When <see langword="true"/>, the options are validated at startup.</param>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered.</param>
	/// <returns>The builder for chaining.</returns>
	[RequiresUnreferencedCode(BindRequirement)]
	[RequiresDynamicCode(BindRequirement)]
	public static OptionsBuilder<TOptions> BindAndValidateWithZod<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions
	>(
		this OptionsBuilder<TOptions> builder,
		string configSectionPath,
		bool validateOnStart = true,
		MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
	)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

		return builder
			.BindConfiguration(configSectionPath)
			.AddZodSchemaValidator(missingValidatorBehavior, validateOnStart, configSectionPath);
	}

	/// <summary>
	/// Binds <typeparamref name="TOptions"/> to <paramref name="section"/> and validates it with its ZodSharp
	/// schema; see <see cref="BindAndValidateWithZod{TOptions}(OptionsBuilder{TOptions}, string, bool, MissingValidatorBehavior)"/>.
	/// </summary>
	/// <typeparam name="TOptions">The options type.</typeparam>
	/// <param name="builder">The options builder.</param>
	/// <param name="section">The configuration section; its <see cref="IConfigurationSection.Path"/> names failures.</param>
	/// <param name="validateOnStart">When <see langword="true"/>, the options are validated at startup.</param>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered.</param>
	/// <returns>The builder for chaining.</returns>
	[RequiresUnreferencedCode(BindRequirement)]
	[RequiresDynamicCode(BindRequirement)]
	public static OptionsBuilder<TOptions> BindAndValidateWithZod<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions
	>(
		this OptionsBuilder<TOptions> builder,
		IConfigurationSection section,
		bool validateOnStart = true,
		MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
	)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(section);

		return builder.Bind(section).AddZodSchemaValidator(missingValidatorBehavior, validateOnStart, section.Path);
	}
}

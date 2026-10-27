using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using ZodSharp.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="ZodSchemaOptionsValidator{T}"/> instances with
/// <see cref="IServiceCollection"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class OptionsServiceCollectionExtensions
{
	extension<TOptions>(OptionsBuilder<TOptions> builder)
		where TOptions : class
	{
		/// <summary>
		/// Registers a <see cref="ZodSchemaOptionsValidator{T}"/> for the builder's options name that resolves the
		/// source-generated schema validator for <typeparamref name="TOptions"/> from the registered
		/// <see cref="IZodSchemaFactory"/>.
		/// </summary>
		/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="TOptions"/>.</param>
		/// <returns>The <see cref="OptionsBuilder{TOptions}"/> for chaining.</returns>
		/// <remarks>
		/// On .NET 11 the validator is registered through <c>OptionsBuilder&lt;T&gt;.Validate&lt;TValidator&gt;()</c>,
		/// so the platform's named, asynchronous validation wrapping applies. On earlier frameworks it is
		/// registered as an <see cref="IValidateOptions{TOptions}"/> that validates only the builder's name.
		/// </remarks>
		/// <seealso cref="ServiceCollectionExtensions.AddZodSchemaOptionsValidator{T}(IServiceCollection, MissingValidatorBehavior)"/>
		public OptionsBuilder<TOptions> AddZodSchemaValidator(
			MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
		) => Register(builder, missingValidatorBehavior, configurationSection: null);
	}

	// A classic extension method rather than a second extension block: a block whose type parameter carries
	// [DynamicallyAccessedMembers] compiles to a different extension marker, and package validation then
	// reports the 2.4.0 overload above as removed (CP0002).

	/// <summary>
	/// Registers a <see cref="ZodSchemaOptionsValidator{T}"/> for the builder's options name, optionally
	/// validating at startup and recording the configuration section the options are bound from so failures
	/// name the setting (<c>Smtp:Servers:0:Host</c>).
	/// </summary>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="TOptions"/>.</param>
	/// <param name="validateOnStart">
	/// When <see langword="true"/>, calls <c>ValidateOnStart()</c>. On .NET 11 startup validation runs the
	/// asynchronous rules too.
	/// </param>
	/// <param name="configurationSection">
	/// The configuration section the options are bound from. It only affects failure messages; bind the
	/// section yourself (for example with <c>BindConfiguration</c>, which the configuration binder source
	/// generator can intercept in a Native AOT application).
	/// </param>
	/// <typeparam name="TOptions">The options type.</typeparam>
	/// <param name="builder">The options builder.</param>
	/// <returns>The <see cref="OptionsBuilder{TOptions}"/> for chaining.</returns>
	public static OptionsBuilder<TOptions> AddZodSchemaValidator<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions
	>(
		this OptionsBuilder<TOptions> builder,
		MissingValidatorBehavior missingValidatorBehavior,
		bool validateOnStart,
		string? configurationSection = null
	)
		where TOptions : class
	{
		Register(builder, missingValidatorBehavior, configurationSection);

		if (validateOnStart)
			builder.ValidateOnStart();

		return builder;
	}

	static OptionsBuilder<TOptions> Register<TOptions>(
		OptionsBuilder<TOptions> builder,
		MissingValidatorBehavior missingValidatorBehavior,
		string? configurationSection
	)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(builder);

		ZodOptionsValidationRegistration registration = new(
			typeof(TOptions),
			builder.Name,
			string.IsNullOrWhiteSpace(configurationSection) ? null : configurationSection,
			missingValidatorBehavior,
			builder.Name
		);
		builder.Services.AddSingleton(registration);

#if NET11_0_OR_GREATER
		builder.Validate<ZodSchemaOptionsValidator<TOptions>>();
#else
		builder.Services.AddSingleton<IValidateOptions<TOptions>>(sp => new ZodSchemaOptionsValidator<TOptions>(
			sp.GetRequiredService<IZodSchemaFactory>(),
			sp,
			registration
		));
#endif

		return builder;
	}
}

using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.Options;

/// <summary>
/// A generic <see cref="IValidateOptions{T}"/> adapter that resolves the source-generated schema
/// validator for <typeparamref name="T"/> through <see cref="IZodSchemaFactory"/> at runtime,
/// allowing a single hand-written class to validate any registered options type.
/// </summary>
/// <typeparam name="T">The options type to validate.</typeparam>
/// <remarks>
/// <para>
/// Each failure is rendered as <c>{name}: {location}: {message} ({code})</c>. The location is the
/// configuration key (<c>Smtp:Servers:0:Host</c>) when the options were registered with a configuration
/// section, and the dotted path (<c>Servers[0].Host</c>) otherwise; the name prefix is omitted for the default
/// options name. The full errors are available on the returned <see cref="ZodValidateOptionsResult"/>.
/// </para>
/// <para>
/// On .NET 11 the validator also implements <c>IAsyncValidateOptions&lt;T&gt;</c>. The platform calls
/// <see cref="ValidateAsync"/> only from <c>ValidateOnStart()</c> (through <c>IAsyncStartupValidator</c>), so
/// asynchronous and service-aware rules are checked at startup; synchronous options access
/// (<c>IOptions&lt;T&gt;.Value</c>, monitor reloads, snapshots) runs <see cref="Validate"/>, which checks the
/// synchronous rules only. That is the platform's own contract.
/// </para>
/// </remarks>
public sealed class ZodSchemaOptionsValidator<T> : IValidateOptions<T>
#if NET11_0_OR_GREATER
		, IAsyncValidateOptions<T>
#endif
	where T : class
{
	readonly IZodSchemaFactory _factory;
	readonly MissingValidatorBehavior _missingValidatorBehavior;
	readonly IServiceProvider? _services;
	readonly ZodOptionsValidationRegistration? _registration;

	/// <summary>
	/// Initializes a new instance with the schema factory used to resolve validators.
	/// </summary>
	/// <param name="factory">The schema factory.</param>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="T"/>.</param>
	public ZodSchemaOptionsValidator(
		IZodSchemaFactory factory,
		MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
	)
		: this(factory, services: null, missingValidatorBehavior) { }

	/// <summary>
	/// Initializes a new instance with the schema factory used to resolve validators and the services passed to
	/// asynchronous, service-aware rules. Settings recorded by
	/// <c>OptionsBuilder&lt;T&gt;.AddZodSchemaValidator(...)</c> for the validated options name (the missing
	/// validator behaviour and the configuration section) take precedence over
	/// <paramref name="missingValidatorBehavior"/>.
	/// </summary>
	/// <param name="factory">The schema factory.</param>
	/// <param name="services">The services used for asynchronous validation; a scope is created per validation.</param>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="T"/>.</param>
	[ActivatorUtilitiesConstructor]
	public ZodSchemaOptionsValidator(
		IZodSchemaFactory factory,
		IServiceProvider? services,
		MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
	)
	{
		ArgumentNullException.ThrowIfNull(factory);

		_factory = factory;
		_services = services;
		_missingValidatorBehavior = missingValidatorBehavior;
	}

	internal ZodSchemaOptionsValidator(
		IZodSchemaFactory factory,
		IServiceProvider? services,
		ZodOptionsValidationRegistration registration
	)
		: this(factory, services, registration.MissingValidatorBehavior)
	{
		_registration = registration;
	}

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, T options)
	{
		var registration = ResolveRegistration(name);
		if (
			registration is { NameFilter: { } filter }
			&& !string.Equals(filter, name ?? string.Empty, StringComparison.Ordinal)
		)
			return ValidateOptionsResult.Skip;

		var validator = ResolveValidator(registration);
		if (validator is null)
			return ValidateOptionsResult.Success;

		var result = validator.Validate(options);
		return ToResult(name, registration, result);
	}

	/// <summary>
	/// Validates <paramref name="options"/> asynchronously, running asynchronous and service-aware rules. On
	/// .NET 11 this implements <c>IAsyncValidateOptions&lt;T&gt;</c> and is called by <c>ValidateOnStart()</c>.
	/// </summary>
	/// <param name="name">The options name.</param>
	/// <param name="options">The options instance.</param>
	/// <param name="cancellationToken">A token that cancels the validation.</param>
	/// <returns>The validation result.</returns>
	public async Task<ValidateOptionsResult> ValidateAsync(
		string? name,
		T options,
		CancellationToken cancellationToken = default
	)
	{
		var registration = ResolveRegistration(name);
		if (
			registration is { NameFilter: { } filter }
			&& !string.Equals(filter, name ?? string.Empty, StringComparison.Ordinal)
		)
			return ValidateOptionsResult.Skip;

		var validator = ResolveValidator(registration);
		if (validator is null)
			return ValidateOptionsResult.Success;

		if (_services?.GetService<IServiceScopeFactory>() is { } scopeFactory)
		{
			var scope = scopeFactory.CreateAsyncScope();
			await using (scope.ConfigureAwait(false))
			{
				var scoped = await validator
					.ValidateAsync(options, scope.ServiceProvider, cancellationToken)
					.ConfigureAwait(false);
				return ToResult(name, registration, scoped);
			}
		}

		var result = await validator.ValidateAsync(options, _services, cancellationToken).ConfigureAwait(false);
		return ToResult(name, registration, result);
	}

	IZodSchemaValidator<T>? ResolveValidator(ZodOptionsValidationRegistration? registration) =>
		(registration?.MissingValidatorBehavior ?? _missingValidatorBehavior) == MissingValidatorBehavior.Throw
			? _factory.ResolveRequired<T>()
			: _factory.Resolve<T>();

	ZodOptionsValidationRegistration? ResolveRegistration(string? name)
	{
		if (_registration is not null)
			return _registration;

		if (_services is null)
			return null;

		var effectiveName = name ?? Microsoft.Extensions.Options.Options.DefaultName;
		ZodOptionsValidationRegistration? match = null;
		foreach (var registration in _services.GetServices<ZodOptionsValidationRegistration>())
		{
			if (
				registration.OptionsType == typeof(T)
				&& string.Equals(registration.Name, effectiveName, StringComparison.Ordinal)
			)
			{
				match = registration;
			}
		}

		return match;
	}

	static ValidateOptionsResult ToResult(
		string? name,
		ZodOptionsValidationRegistration? registration,
		in ValidationResult<T> result
	)
	{
		if (result.IsSuccess)
			return ValidateOptionsResult.Success;

		var errors = result.Errors.IsDefault ? [] : result.Errors;
		return new ZodValidateOptionsResult(name, registration?.ConfigurationSection, errors);
	}
}

/// <summary>
/// A failed <see cref="ValidateOptionsResult"/> produced by <see cref="ZodSchemaOptionsValidator{T}"/> that keeps
/// the full <see cref="ValidationError"/>s alongside the path-bearing failure messages.
/// </summary>
public sealed class ZodValidateOptionsResult : ValidateOptionsResult
{
	internal ZodValidateOptionsResult(
		string? optionsName,
		string? configurationSection,
		ImmutableArray<ValidationError> errors
	)
	{
		OptionsName = optionsName;
		ConfigurationSection = configurationSection;
		Errors = errors;

		var failures = new string[errors.Length];
		for (var i = 0; i < failures.Length; i++)
			failures[i] = ZodOptionsFailureFormatter.Format(optionsName, configurationSection, errors[i]);

		Failed = true;
		Failures = failures;
		FailureMessage = string.Join("; ", failures);
	}

	/// <summary>
	/// Gets the options name that failed validation.
	/// </summary>
	public string? OptionsName { get; }

	/// <summary>
	/// Gets the configuration section the options were bound from, when it was recorded at registration.
	/// </summary>
	public string? ConfigurationSection { get; }

	/// <summary>
	/// Gets the full validation errors.
	/// </summary>
	public ImmutableArray<ValidationError> Errors { get; }
}

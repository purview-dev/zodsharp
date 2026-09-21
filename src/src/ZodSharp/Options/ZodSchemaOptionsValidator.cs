using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.Options;

/// <summary>
/// Defines how <see cref="ZodSchemaOptionsValidator{T}"/> behaves when no schema validator is
/// registered for the options type.
/// </summary>
public enum MissingValidatorBehavior
{
	/// <summary>
	/// Throws <see cref="InvalidOperationException"/> via <see cref="IZodSchemaFactory.ResolveRequired{T}"/>
	/// when no validator is registered for the options type.
	/// </summary>
	Throw,

	/// <summary>
	/// Treats an unregistered options type as valid, returning <see cref="ValidateOptionsResult.Success"/>.
	/// </summary>
	Ignore,
}

/// <summary>
/// A generic <see cref="IValidateOptions{T}"/> adapter that resolves the source-generated schema
/// validator for <typeparamref name="T"/> through <see cref="IZodSchemaFactory"/> at runtime,
/// allowing a single hand-written class to validate any registered options type.
/// </summary>
/// <typeparam name="T">The options type to validate.</typeparam>
public sealed class ZodSchemaOptionsValidator<T> : IValidateOptions<T>
	where T : class
{
	readonly IZodSchemaFactory _factory;
	readonly MissingValidatorBehavior _missingValidatorBehavior;

	/// <summary>Initializes a new instance with the schema factory used to resolve validators.</summary>
	/// <param name="factory">The schema factory.</param>
	/// <param name="missingValidatorBehavior">How to behave when no validator is registered for <typeparamref name="T"/>.</param>
	public ZodSchemaOptionsValidator(
		IZodSchemaFactory factory,
		MissingValidatorBehavior missingValidatorBehavior = MissingValidatorBehavior.Throw
	)
	{
		ArgumentNullException.ThrowIfNull(factory);

		_factory = factory;
		_missingValidatorBehavior = missingValidatorBehavior;
	}

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, T options)
	{
		var validator =
			_missingValidatorBehavior == MissingValidatorBehavior.Throw
				? _factory.ResolveRequired<T>()
				: _factory.Resolve<T>();

		if (validator is null)
			return ValidateOptionsResult.Success;

		var result = validator.Validate(options);
		return result.IsSuccess
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(result.Errors.Select(static error => error.Message));
	}
}

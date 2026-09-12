using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.Options;

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

	/// <summary>Initializes a new instance with the schema factory used to resolve validators.</summary>
	/// <param name="factory">The schema factory.</param>
	public ZodSchemaOptionsValidator(IZodSchemaFactory factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		_factory = factory;
	}

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, T options)
	{
		var validator = _factory.Resolve<T>();
		if (validator is null)
			return ValidateOptionsResult.Success;

		var result = validator.Validate(options);
		return result.IsSuccess
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(result.Errors.Select(static error => error.Message));
	}
}

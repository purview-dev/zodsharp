namespace ZodSharp.Core;

/// <summary>
/// Non-generic marker for all Zod schema validators resolved by the DI factory.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1040:Avoid empty interfaces",
	Justification = "Marker interface for non-generic DI resolution."
)]
public interface IZodSchemaValidator { }

/// <summary>
/// Type-safe validator contract usable from DI. Mirrors IZodSchema.Validate.
/// Also extends IZodSchema&lt;T&gt; so that generated *SchemaValidator adapters can
/// participate in the full schema composition API (.And(), .Or(), .Pipe(), etc.).
/// </summary>
/// <typeparam name="T">The validated value type.</typeparam>
public interface IZodSchemaValidator<T> : IZodSchemaValidator, IZodSchema<T>
{
	/// <summary>
	/// Validates <paramref name="value"/> and returns a result.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result containing the validated value or errors.</returns>
	new ValidationResult<T> Validate(T value);

	/// <summary>
	/// Validates the input value asynchronously and returns a validation result.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <param name="cancellationToken"></param>
	/// <returns>A task that completes with a validation result.</returns>
	new ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default);

	/// <summary>
	/// Validates the input value asynchronously with access to the caller's services, so service-aware rules
	/// (uniqueness checks, lookups) can run. Hosts pass the request or component service provider.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <param name="services">The caller's services, or <see langword="null"/> when none are available.</param>
	/// <param name="cancellationToken">A token that cancels the validation.</param>
	/// <returns>A task that completes with a validation result.</returns>
	ValueTask<ValidationResult<T>> ValidateAsync(
		T value,
		IServiceProvider? services,
		CancellationToken cancellationToken = default
	) => ValidateAsync(value, cancellationToken);

	/// <summary>
	/// Gets whether some rules only run asynchronously, so the synchronous <see cref="Validate"/> cannot give a
	/// complete answer. Hosts that can only validate synchronously throw rather than silently skip them.
	/// </summary>
	bool RequiresAsyncValidation => false;
}

namespace ZodSharp.Core;

/// <summary>
/// Resolves <see cref="IZodSchemaValidator{T}"/> instances by validated type, for DI scenarios.
/// </summary>
public interface IZodSchemaFactory
{
	/// <summary>Resolves the validator registered for <typeparamref name="T"/>.</summary>
	IZodSchemaValidator<T>? Resolve<T>();

	/// <summary>Resolves the validator registered for <typeparamref name="T"/> and throws if its doesn't exist.</summary>
	IZodSchemaValidator<T> ResolveRequired<T>();

	/// <summary>Validates <paramref name="value"/> using the registered validator for <typeparamref name="T"/>.</summary>
	ValidationResult<T> Validate<T>(T value);

	/// <summary>Registers a validator for <typeparamref name="T"/>, overwriting any existing registration.</summary>
	void Register<T>(IZodSchemaValidator<T> validator);

	/// <summary>Registers a non-generic validator instance for <paramref name="targetType"/>, overwriting any existing registration.</summary>
	void Register(Type targetType, IZodSchemaValidator validator);

	/// <summary>Registers only if no validator is already registered; returns false otherwise.</summary>
	bool TryRegister<T>(IZodSchemaValidator<T> validator);

	/// <summary>True if a validator is registered for <typeparamref name="T"/>.</summary>
	bool IsRegistered<T>();

	/// <summary>
	/// Resolves a type-erased view of the validator registered for <paramref name="type"/>, for hosts that only
	/// know the type at runtime. Registrations made through <see cref="Register{T}"/>, or with a validator that
	/// implements <see cref="IZodUntypedValidator"/> itself (generated validators do), are resolvable.
	/// </summary>
	/// <param name="type">The validated type.</param>
	/// <param name="validator">The validator, when one is registered.</param>
	/// <returns><see langword="true"/> when a validator is registered.</returns>
	bool TryGetUntypedValidator(Type type, out IZodUntypedValidator? validator)
	{
		validator = null;
		return false;
	}
}

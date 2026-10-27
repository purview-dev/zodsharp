using System.Collections.Immutable;

namespace ZodSharp.Core;

/// <summary>
/// A type-erased view of a registered validator, so hosts that only know a <see cref="Type"/> at runtime
/// (Microsoft.Extensions.Validation resolvers, Blazor, endpoint filters) can validate without
/// <c>MakeGenericType</c> or other reflection. Obtain one from
/// <see cref="IZodSchemaFactory.TryGetUntypedValidator(Type, out IZodUntypedValidator?)"/>.
/// </summary>
public interface IZodUntypedValidator
{
	/// <summary>
	/// Gets the validated type.
	/// </summary>
	Type ValidatedType { get; }

	/// <summary>
	/// Gets whether some rules only run asynchronously.
	/// </summary>
	bool RequiresAsyncValidation { get; }

	/// <summary>
	/// Validates <paramref name="value"/> synchronously.
	/// </summary>
	/// <param name="value">The value; must be assignable to <see cref="ValidatedType"/> or <see langword="null"/>.</param>
	/// <returns>The errors; empty when valid.</returns>
	ImmutableArray<ValidationError> Validate(object? value);

	/// <summary>
	/// Validates <paramref name="value"/> asynchronously.
	/// </summary>
	/// <param name="value">The value; must be assignable to <see cref="ValidatedType"/> or <see langword="null"/>.</param>
	/// <param name="services">The caller's services, if any.</param>
	/// <param name="cancellationToken">A token that cancels the validation.</param>
	/// <returns>The errors; empty when valid.</returns>
	ValueTask<ImmutableArray<ValidationError>> ValidateAsync(
		object? value,
		IServiceProvider? services,
		CancellationToken cancellationToken
	);

	/// <summary>
	/// Gets whether <paramref name="memberName"/> can be validated on its own.
	/// </summary>
	/// <param name="memberName">The C# member name.</param>
	/// <returns><see langword="true"/> when supported.</returns>
	bool IsValidatableMember(string memberName);

	/// <summary>
	/// Validates one member of <paramref name="value"/> asynchronously; see <see cref="IZodMemberValidator{T}"/>.
	/// </summary>
	/// <param name="value">The containing object.</param>
	/// <param name="memberName">The C# member name.</param>
	/// <param name="services">The caller's services, if any.</param>
	/// <param name="cancellationToken">A token that cancels the validation.</param>
	/// <returns>The member's errors; empty when valid or unsupported.</returns>
	ValueTask<ImmutableArray<ValidationError>> ValidateMemberAsync(
		object value,
		string memberName,
		IServiceProvider? services,
		CancellationToken cancellationToken
	);

	/// <summary>
	/// Resolves the object that owns the last member of <paramref name="path"/>; see
	/// <see cref="IZodMemberResolver{T}"/>.
	/// </summary>
	/// <param name="value">The root value.</param>
	/// <param name="path">The error path.</param>
	/// <param name="container">The owning object.</param>
	/// <param name="memberName">The owning member name.</param>
	/// <returns><see langword="true"/> when resolution is supported and the full path was followed.</returns>
	bool TryResolveMember(object value, ZodPath path, out object? container, out string memberName);
}

/// <summary>
/// Adapts an <see cref="IZodSchemaValidator{T}"/> to <see cref="IZodUntypedValidator"/>, delegating member
/// validation and member resolution when the validator supports them.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <param name="validator">The typed validator.</param>
public sealed class ZodUntypedValidator<T>(IZodSchemaValidator<T> validator) : IZodUntypedValidator
{
	/// <summary>
	/// Gets the wrapped validator.
	/// </summary>
	public IZodSchemaValidator<T> Inner { get; } = validator ?? throw new ArgumentNullException(nameof(validator));

	/// <inheritdoc/>
	public Type ValidatedType => typeof(T);

	/// <inheritdoc/>
	public bool RequiresAsyncValidation => Inner.RequiresAsyncValidation;

	/// <inheritdoc/>
	public ImmutableArray<ValidationError> Validate(object? value)
	{
		var result = Inner.Validate(Cast(value));
		return result.IsSuccess ? [] : result.Errors;
	}

	/// <inheritdoc/>
	public async ValueTask<ImmutableArray<ValidationError>> ValidateAsync(
		object? value,
		IServiceProvider? services,
		CancellationToken cancellationToken
	)
	{
		var pending = Inner.ValidateAsync(Cast(value), services, cancellationToken);
		var result = pending.IsCompletedSuccessfully ? pending.Result : await pending.ConfigureAwait(false);
		return result.IsSuccess ? [] : result.Errors;
	}

	/// <inheritdoc/>
	public bool IsValidatableMember(string memberName) =>
		Inner is IZodMemberValidator<T> members && members.IsValidatableMember(memberName);

	/// <inheritdoc/>
	public async ValueTask<ImmutableArray<ValidationError>> ValidateMemberAsync(
		object value,
		string memberName,
		IServiceProvider? services,
		CancellationToken cancellationToken
	)
	{
		if (Inner is not IZodMemberValidator<T> members)
			return [];

		var pending = members.ValidateMemberAsync(Cast(value), memberName, services, cancellationToken);
		var result = pending.IsCompletedSuccessfully ? pending.Result : await pending.ConfigureAwait(false);
		return result.IsSuccess ? [] : result.Errors;
	}

	/// <inheritdoc/>
	public bool TryResolveMember(object value, ZodPath path, out object? container, out string memberName)
	{
		if (Inner is IZodMemberResolver<T> resolver)
			return resolver.TryResolveMember(Cast(value), path, out container, out memberName);

		container = value;
		memberName = string.Empty;
		return false;
	}

	static T Cast(object? value) =>
		value is T typed ? typed
		: value is null ? default!
		: throw new ArgumentException(
			$"Value of type '{value.GetType().FullName}' cannot be validated as '{typeof(T).FullName}'.",
			nameof(value)
		);
}

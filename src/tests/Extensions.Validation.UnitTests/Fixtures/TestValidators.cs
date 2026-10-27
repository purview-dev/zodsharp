using ZodSharp.Core;

namespace ZodSharp.Extensions.Validation.Fixtures;

/// <summary>
/// Returns the errors a test scripts, so paths can be asserted exactly. Implements neither member validation
/// nor member resolution, like a runtime schema registered with <see cref="ZodSchemaValidator{T}"/>.
/// </summary>
sealed class ScriptedValidator<T>(Func<T, IEnumerable<ValidationError>> errors) : IZodSchemaValidator<T>
{
	public ScriptedValidator(params ValidationError[] errors)
		: this(_ => errors) { }

	public bool RequiresAsync { get; init; }

	public bool RequiresAsyncValidation => RequiresAsync;

	public ValidationResult<T> Validate(T value)
	{
		ValidationError[] produced = [.. errors(value)];
		return produced.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(produced);
	}

	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		new(Validate(value));
}

/// <summary>
/// Completes asynchronously and only gives a complete answer asynchronously.
/// </summary>
sealed class AsyncOnlyValidator<T>(params ValidationError[] errors) : IZodSchemaValidator<T>
{
	public IServiceProvider? ReceivedServices { get; private set; }

	public bool RequiresAsyncValidation => true;

	public ValidationResult<T> Validate(T value) => ValidationResult<T>.Success(value);

	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		ValidateAsync(value, null, cancellationToken);

	public async ValueTask<ValidationResult<T>> ValidateAsync(
		T value,
		IServiceProvider? services,
		CancellationToken cancellationToken = default
	)
	{
		ReceivedServices = services;
		await Task.Yield();
		return errors.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(errors);
	}
}

/// <summary>
/// Supports member validation and reflection-free member resolution, like a generated validator.
/// </summary>
sealed class MemberAwareValidator<T>(
	Func<T, IEnumerable<ValidationError>> errors,
	Func<T, string, IEnumerable<ValidationError>> memberErrors,
	Func<T, ZodPath, (object? Container, string Member)?> resolve
) : IZodSchemaValidator<T>, IZodMemberValidator<T>, IZodMemberResolver<T>
{
	public HashSet<string> ValidatableMembers { get; } = [with(StringComparer.Ordinal)];

	public ValidationResult<T> Validate(T value)
	{
		ValidationError[] produced = [.. errors(value)];
		return produced.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(produced);
	}

	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		new(Validate(value));

	public bool IsValidatableMember(string memberName) => ValidatableMembers.Contains(memberName);

	public ValidationResult<T> ValidateMember(T value, string memberName)
	{
		ValidationError[] produced = [.. memberErrors(value, memberName)];
		return produced.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(produced);
	}

	public bool TryResolveMember(T value, ZodPath path, out object? container, out string memberName)
	{
		if (resolve(value, path) is { } resolved)
		{
			container = resolved.Container;
			memberName = resolved.Member;
			return true;
		}

		container = value;
		memberName = string.Empty;
		return false;
	}
}

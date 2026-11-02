using ZodSharp.Core;

namespace ZodSharp.AspNetCore.Fixtures;

/// <summary>
/// A validator whose errors come from a delegate, for tests that need specific paths or parameters.
/// </summary>
public sealed class DelegateValidator<T>(Func<T, IEnumerable<ValidationError>> check) : IZodSchemaValidator<T>
{
	public int Calls { get; private set; }

	public ValidationResult<T> Validate(T value)
	{
		Calls++;
		ValidationError[] errors = [.. check(value)];
		return errors.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(errors);
	}

	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		ValueTask.FromResult(Validate(value));
}

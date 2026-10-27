using ZodSharp.Core;

namespace ZodSharp.Blazor.Fixtures;

/// <summary>
/// A scriptable validator. Member validation is supported only when <see cref="MemberErrors"/> is set, like a
/// generated validator; member resolution is never offered, so field placement goes through the reflection
/// fallback (as for a runtime schema).
/// </summary>
sealed class FakeValidator<T> : IZodSchemaValidator<T>, IZodMemberValidator<T>
{
	public Func<T, IEnumerable<ValidationError>> Errors { get; init; } = static _ => [];

	/// <summary>
	/// The errors of one member; <see langword="null"/> when member validation is not supported.
	/// </summary>
	public Func<T, string, IEnumerable<ValidationError>>? MemberErrors { get; init; }

	/// <summary>
	/// Awaited before every member validation completes, so tests can hold a validation pending.
	/// </summary>
	public Func<string, CancellationToken, Task>? MemberGate { get; init; }

	/// <summary>
	/// Awaited before every full validation completes.
	/// </summary>
	public Func<CancellationToken, Task>? Gate { get; init; }

	public bool RequiresAsync { get; init; }

	public int Calls { get; private set; }

	public List<string> MemberCalls { get; } = [];

	public List<CancellationToken> MemberTokens { get; } = [];

	public IServiceProvider? ReceivedServices { get; private set; }

	public bool RequiresAsyncValidation => RequiresAsync;

	public ValidationResult<T> Validate(T value)
	{
		Calls++;
		return ToResult(value, Errors(value));
	}

	public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
		ValidateAsync(value, null, cancellationToken);

	public async ValueTask<ValidationResult<T>> ValidateAsync(
		T value,
		IServiceProvider? services,
		CancellationToken cancellationToken = default
	)
	{
		Calls++;
		ReceivedServices = services;
		if (Gate is not null)
			await Gate(cancellationToken);

		return ToResult(value, Errors(value));
	}

	public bool IsValidatableMember(string memberName) => MemberErrors is not null;

	public ValidationResult<T> ValidateMember(T value, string memberName)
	{
		MemberCalls.Add(memberName);
		return ToResult(value, MemberErrors?.Invoke(value, memberName) ?? []);
	}

	async ValueTask<ValidationResult<T>> IZodMemberValidator<T>.ValidateMemberAsync(
		T value,
		string memberName,
		IServiceProvider? services,
		CancellationToken cancellationToken
	)
	{
		MemberCalls.Add(memberName);
		MemberTokens.Add(cancellationToken);
		ReceivedServices = services;
		if (MemberGate is not null)
			await MemberGate(memberName, cancellationToken);

		return ToResult(value, MemberErrors?.Invoke(value, memberName) ?? []);
	}

	static ValidationResult<T> ToResult(T value, IEnumerable<ValidationError> errors)
	{
		ValidationError[] produced = [.. errors];
		return produced.Length == 0 ? ValidationResult<T>.Success(value) : ValidationResult<T>.Failure(produced);
	}
}

static class Errors
{
	public static ValidationError At(string code, string message, params ZodPathSegment[] path) =>
		ValidationError.Create(code, message, ZodPath.From(path), category: code);

	public static ZodPathSegment M(string name) => ZodPathSegment.Member(name);

	public static ZodPathSegment I(int index) => ZodPathSegment.At(index);

	public static ZodPathSegment K(string key) => ZodPathSegment.Key(key);
}

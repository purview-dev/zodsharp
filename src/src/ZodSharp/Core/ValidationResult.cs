using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace ZodSharp.Core;

/// <summary>
/// Represents the result of a validation operation.
/// Uses struct to avoid allocations.
/// </summary>
/// <typeparam name="T">The type of the validated value</typeparam>
public readonly record struct ValidationResult<T>
{
	/// <summary>
	/// Indicates whether the validation was successful.
	/// </summary>
	[MemberNotNullWhen(true, nameof(Value))]
	public bool IsSuccess { get; }

	/// <summary>
	/// The validated value. Only valid when <see cref="IsSuccess"/> is <see langword="true"/>.
	/// </summary>
	public T? Value { get; }

	/// <summary>
	/// The validation errors. Only populated when <see cref="IsSuccess"/> is <see langword="false"/>.
	/// </summary>
	public ImmutableArray<ValidationError> Errors { get; }

	ValidationResult(bool isSuccess, T? value, ImmutableArray<ValidationError> errors)
	{
		IsSuccess = isSuccess;
		Value = value;
		Errors = errors;
	}

	/// <summary>
	/// Merges two <see cref="ValidationResult{T}"/>s together. Note the <see cref="Value"/>
	/// is not checked for anything other than <see langword="null" />, and the <paramref name="lhs"/> is favoured.
	/// </summary>
	/// <param name="lhs"></param>
	/// <param name="rhs"></param>
	/// <returns></returns>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static ValidationResult<T> Merge(ValidationResult<T> lhs, ValidationResult<T> rhs)
	{
		var isSuccess = lhs.IsSuccess && rhs.IsSuccess;
		var value = lhs.Value ?? rhs.Value ?? default;
		var errors = lhs.Errors.AddRange(rhs.Errors);

		return new(isSuccess, value, errors);
	}

	/// <summary>
	/// Throws a <see cref="ZodException"/> if validation failed.
	/// </summary>
	/// <returns>The validated value</returns>
	/// <exception cref="ZodException">Thrown when validation fails</exception>
	/// <seealso cref="ThrowOnError"/>
	[SuppressMessage("Design", "CA1024:Use properties where appropriate")]
	public T GetValueOrThrow() => IsSuccess ? Value : throw new ZodException(Errors);

	/// <summary>
	/// Throws a <see cref="ZodException"/> if validation failed.
	/// </summary>
	/// <returns>The validated value</returns>
	/// <exception cref="ZodException">Thrown when validation fails</exception>
	/// <seealso cref="GetValueOrThrow"/>
	public T ThrowOnError() => IsSuccess ? Value : throw new ZodException(Errors);

	/// <summary>
	/// Creates a successful validation result.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static ValidationResult<T> Success(T value) => new(true, value, []);

	/// <summary>
	/// Creates a failed validation result with a single error.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static ValidationResult<T> Failure(ValidationError error) => new(false, default, [error]);

	/// <summary>
	/// Creates a failed validation result with multiple errors.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static ValidationResult<T> Failure(ImmutableArray<ValidationError> errors) => new(false, default, errors);

	/// <summary>
	/// Creates a failed validation result with multiple errors.
	/// </summary>
	[SuppressMessage("Design", "CA1000:Do not declare static members on generic types")]
	public static ValidationResult<T> Failure(IEnumerable<ValidationError> errors) => new(false, default, [.. errors]);
}

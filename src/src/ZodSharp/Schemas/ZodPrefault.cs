using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema that substitutes a given value when the input is the default for
/// <typeparamref name="T"/> (e.g. <see langword="null"/> for reference types),
/// then validates the result. Equivalent to Zod's <c>.prefault(value)</c>
/// method. Unlike <see cref="ZodDefault{T}"/>, the substituted value still
/// runs through the inner schema.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ZodPrefault{T}"/> class.
/// </remarks>
/// <param name="innerSchema">The inner schema.</param>
/// <param name="prefaultValue">The value used when the input is default.</param>
public class ZodPrefault<T>(IZodSchema<T, T> innerSchema, T prefaultValue) : ZodType<T, T>, IAcceptsNull
{
	/// <inheritdoc/>
	public override bool IsOptional => true;

	/// <inheritdoc/>
	public override bool ProvidesValueOnMissing => true;

	/// <inheritdoc/>
	public ValidationResult<object> ValidateNull()
	{
		var result = innerSchema.Validate(prefaultValue);
		return result.IsSuccess
			? ValidationResult<object>.Success(result.Value)
			: ValidationResult<object>.Failure(result.Errors);
	}

	/// <summary>
	/// Validates the value, substituting the prefault value when the input is
	/// default for <typeparamref name="T"/>, then validating the result with the
	/// inner schema.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A validation result.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0370:Suppression is unnecessary")]
	protected override ValidationResult<T> ParseInternal(T value)
	{
		var effective = EqualityComparer<T>.Default.Equals(value, default!) ? prefaultValue : value;
		return innerSchema.Validate(effective);
	}
}

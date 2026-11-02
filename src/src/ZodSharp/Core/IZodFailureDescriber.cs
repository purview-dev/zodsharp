namespace ZodSharp.Core;

/// <summary>
/// The value-dependent part of a rule failure: the Zod issue code for <em>this</em> failure and any parameters
/// that depend on the failing value (for example the <see cref="ZodIssueParameters.Count"/> of a length rule).
/// </summary>
/// <param name="Category">The Zod issue code, overriding <see cref="IZodRule.Category"/>; <see langword="null"/> keeps it.</param>
/// <param name="Parameters">Value-dependent parameters, merged over <see cref="IZodRule.Parameters"/>.</param>
public readonly record struct ZodRuleFailure(
	string? Category = null,
	IReadOnlyDictionary<string, object?>? Parameters = null
)
{
	/// <summary>
	/// Gets the error code for <em>this</em> failure, overriding the rule's own code when the failure's direction
	/// decides it (for example <see cref="ZodIssueCodes.TooSmall"/> below a range and
	/// <see cref="ZodIssueCodes.TooBig"/> above it); <see langword="null"/> keeps the rule's code. It replaces only
	/// the rule's own code: a code supplied explicitly by the caller (an attribute's <c>Code</c>) still wins.
	/// </summary>
	public string? Code { get; init; }
}

/// <summary>
/// Implemented by a rule whose failure depends on the failing value, such as a range rule whose Zod issue code is
/// <see cref="ZodIssueCategories.TooSmall"/> or <see cref="ZodIssueCategories.TooBig"/> by direction.
/// <see cref="ZodRuleErrors"/> consults it when building the error.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IZodFailureDescriber<T>
{
	/// <summary>
	/// Describes the failure of <paramref name="value"/>. Only called after the rule rejected the value.
	/// </summary>
	/// <param name="value">The value that failed.</param>
	/// <returns>The value-dependent failure description.</returns>
	ZodRuleFailure DescribeFailure(in T value);
}

/// <summary>
/// The span counterpart of <see cref="IZodFailureDescriber{T}"/> for <see cref="IStringValidationRule"/>.
/// </summary>
public interface IZodStringFailureDescriber
{
	/// <summary>
	/// Describes the failure of <paramref name="value"/>. Only called after the rule rejected the value.
	/// </summary>
	/// <param name="value">The value that failed.</param>
	/// <returns>The value-dependent failure description.</returns>
	ZodRuleFailure DescribeFailure(ReadOnlySpan<char> value);
}

using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Schemas;

/// <summary>
/// Schema for <see cref="DateTimeOffset"/> validation.
/// </summary>
/// <remarks>
/// This is the fluent counterpart to a generated <c>[ZodSchema]</c> property. Its JSON Schema mapping is an
/// RFC 3339 <c>date-time</c> string, and <see cref="NonSentinel"/> exports the offset-aware sentinel pattern.
/// </remarks>
public class ZodDateTimeOffset : ZodType<DateTimeOffset>
{
	/// <summary>
	/// Parses and validates a <see cref="DateTimeOffset"/> value.
	/// </summary>
	/// <param name="value">The value to validate.</param>
	/// <returns>A successful validation result.</returns>
	protected override ValidationResult<DateTimeOffset> ParseInternal(DateTimeOffset value) =>
		ValidationResult<DateTimeOffset>.Success(value);

	/// <summary>
	/// Adds a non-sentinel validation that rejects <see cref="DateTimeOffset.MinValue"/> and
	/// <see cref="DateTimeOffset.MaxValue"/>.
	/// </summary>
	/// <param name="message">Optional error message.</param>
	/// <param name="code">Optional error code override.</param>
	/// <returns>This schema for method chaining.</returns>
	public override ZodDateTimeOffset NonSentinel(string? message = null, string? code = null)
	{
		AddRule(new NonSentinelRule<DateTimeOffset>(message, code));
		return this;
	}
}

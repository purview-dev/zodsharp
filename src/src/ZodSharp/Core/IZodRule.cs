namespace ZodSharp.Core;

/// <summary>
/// Implemented by validation rules that describe their own error identity. When a mapped rule implements
/// this interface the generator prefers the rule's <see cref="Code"/>/<see cref="Origin"/> over the values
/// declared on the mapped attribute, so a single attribute (for example <c>[NotEmpty]</c>) can produce a
/// different error code per annotated member.
/// </summary>
/// <remarks>
/// <para>
/// Rules are constructed in the generated code, so the identity can depend on the rule's constructor
/// arguments. The interface may be implemented explicitly; the generated code casts to
/// <see cref="IZodRule"/> when reading the values.
/// </para>
/// <para>
/// This interface is the only path for a constructor argument to reach the reported identity: a rule that
/// accepts a <c>code</c> or <c>origin</c> parameter without implementing it is reported as <c>ZODSGEN039</c>,
/// because the value would be supplied by the generated validation and then never read. See
/// <see cref="IZodRuleAttribute"/> for the attribute-side counterpart.
/// </para>
/// <para>
/// The structured members (<see cref="Category"/>, <see cref="Lower"/>, <see cref="Upper"/>,
/// <see cref="MessageFormat"/>, <see cref="Parameters"/>) are copied onto the reported
/// <see cref="ValidationError"/> by <see cref="ZodRuleErrors"/>, so runtime schemas and generated validators
/// report the same structure for the same rule.
/// </para>
/// </remarks>
public interface IZodRule
{
	/// <summary>
	/// Gets the effective error code for the constructed rule, or <see langword="null"/> to defer to the
	/// attribute-mapped code (and then to <c>"validation_failed"</c>). A rule that accepts a <c>code</c>
	/// override typically returns it here and from its <see cref="IValidationRule{T}.Code"/> /
	/// <see cref="IStringValidationRule.Code"/> implementation, so every route reports the same value.
	/// </summary>
	string? Code { get; }

	/// <summary>
	/// Gets the structured <see cref="ValidationError.Origin"/> reported when the rule fails, or
	/// <see langword="null"/> to fall back to the attribute-mapped origin.
	/// </summary>
	string? Origin { get; }

	/// <summary>
	/// Gets the Zod issue code reported as <see cref="ValidationError.Category"/> (see
	/// <see cref="ZodIssueCategories"/>), or <see langword="null"/> when the rule has no Zod equivalent.
	/// </summary>
	string? Category => null;

	/// <summary>
	/// Gets the lower bound the rule enforces, if any.
	/// </summary>
	ErrorBound? Lower => null;

	/// <summary>
	/// Gets the upper bound the rule enforces, if any.
	/// </summary>
	ErrorBound? Upper => null;

	/// <summary>
	/// Gets the unformatted message template, with named placeholders, that a host may re-format (for example
	/// to localize it); <see langword="null"/> when the message is not template-based.
	/// </summary>
	string? MessageFormat => null;

	/// <summary>
	/// Gets the named values reported in <see cref="ValidationError.Parameters"/>, used to format
	/// <see cref="MessageFormat"/>.
	/// </summary>
	IReadOnlyDictionary<string, object?>? Parameters => null;
}

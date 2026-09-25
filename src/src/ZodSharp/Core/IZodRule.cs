namespace ZodSharp.Core;

/// <summary>
/// Implemented by validation rules that describe their own error identity. When a mapped rule implements
/// this interface the generator prefers the rule's <see cref="Code"/>/<see cref="Origin"/> over the values
/// declared on the mapped attribute, so a single attribute (for example <c>[NotEmpty]</c>) can produce a
/// different error code per annotated member.
/// </summary>
/// <remarks>
/// Rules are constructed in the generated code, so the identity can depend on the rule's constructor
/// arguments. The interface may be implemented explicitly; the generated code casts to
/// <see cref="IZodRule"/> when reading the values.
/// </remarks>
public interface IZodRule
{
	/// <summary>
	/// Gets the error code reported when the rule fails, or <see langword="null"/> to fall back to the
	/// attribute-mapped code (and then to <c>"validation_failed"</c>).
	/// </summary>
	string? Code { get; }

	/// <summary>
	/// Gets the structured <see cref="ValidationError.Origin"/> reported when the rule fails, or
	/// <see langword="null"/> to fall back to the attribute-mapped origin.
	/// </summary>
	string? Origin { get; }
}

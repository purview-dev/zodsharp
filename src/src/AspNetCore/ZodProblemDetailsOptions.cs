using System.Collections.Immutable;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Configuration for <see cref="ZodExceptionHandler"/> and the ProblemDetails mapping extensions.
/// </summary>
public sealed class ZodProblemDetailsOptions
{
	/// <summary>
	/// The <see cref="ErrorTypeRegistry"/> used to resolve error codes to <see cref="ErrorType"/>s.
	/// Defaults to <see cref="ErrorTypeRegistry.Default"/>.
	/// </summary>
	public ErrorTypeRegistry Registry { get; set; } = ErrorTypeRegistry.Default;

	/// <summary>
	/// When <c>true</c>, an error's message is formatted from the matched <see cref="ErrorType.MessageFormat"/>
	/// using the error's <see cref="Core.ValidationError.Parameters"/>. Defaults to <c>true</c>.
	/// </summary>
	public bool FormatMessages { get; set; } = true;

	/// <summary>
	/// An optional escape hatch that takes complete control of the response status code. When set, its
	/// result overrides the highest matched <see cref="ErrorType.HttpStatus"/>.
	/// </summary>
	public Func<ImmutableArray<Core.ValidationError>, int>? StatusCodeSelector { get; set; }

	/// <summary>
	/// Registers an <see cref="ErrorType"/> with the configured <see cref="Registry"/>.
	/// </summary>
	public ZodProblemDetailsOptions MapErrorType(ErrorType errorType)
	{
		Registry.Register(errorType);
		return this;
	}
}

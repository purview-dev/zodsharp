#if NET11_0_OR_GREATER
using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The full-fidelity ZodSharp errors reported during one request by the Microsoft.Extensions.Validation bridge.
/// Registered scoped by <c>AddZodSharpProblemDetails()</c>, so the bridge reaches it through
/// <c>ValidateContext.ServiceProvider</c> (the request services), and the ProblemDetails customization uses it
/// to rebuild the built-in pipeline's response with the ZodSharp contract.
/// </summary>
public sealed class ZodRequestValidationReport : IZodValidationReportSink
{
	readonly Lock _lock = new();
	ImmutableArray<ValidationError> _errors = [];

	/// <summary>
	/// Gets the errors reported so far, in the order they were reported. Paths are canonical: they already
	/// include the validation path prefix they were reported under.
	/// </summary>
	public ImmutableArray<ValidationError> Errors => _errors;

	/// <summary>
	/// Gets whether any error was reported.
	/// </summary>
	public bool HasErrors => !_errors.IsEmpty;

	/// <inheritdoc/>
	public void Add(Type validatedType, string pathPrefix, ImmutableArray<ValidationError> errors)
	{
		if (errors.IsDefaultOrEmpty)
			return;

		lock (_lock)
			_errors = _errors.AddRange(errors);
	}
}
#endif

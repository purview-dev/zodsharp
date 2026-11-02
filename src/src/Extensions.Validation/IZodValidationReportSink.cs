using System.Collections.Immutable;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Receives the full-fidelity ZodSharp errors of every validation the bridge performs. When
/// <c>ValidateContext.ServiceProvider</c> resolves one, the bridge appends to it in addition to the
/// <c>ValidateContext.ZodReport</c> side channel.
/// </summary>
/// <remarks>
/// Register it with a lifetime that equals one validation run - in ASP.NET Core, scoped (one request). Do not
/// register it in Blazor Server, where the scope is the circuit and errors would accumulate across every
/// validation on the page. This package never registers a sink itself.
/// </remarks>
public interface IZodValidationReportSink
{
	/// <summary>
	/// Records the errors of one validated value.
	/// </summary>
	/// <param name="validatedType">The validated type.</param>
	/// <param name="pathPrefix">The <c>ValidateContext.CurrentValidationPath</c> the value was validated at.</param>
	/// <param name="errors">The errors, with canonical paths that already include <paramref name="pathPrefix"/>.</param>
	void Add(Type validatedType, string pathPrefix, ImmutableArray<ZodValidationError> errors);
}

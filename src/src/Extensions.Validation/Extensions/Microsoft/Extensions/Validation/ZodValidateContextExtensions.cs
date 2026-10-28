using System.Runtime.CompilerServices;
using ZodSharp.Extensions.Validation;

namespace Microsoft.Extensions.Validation;

/// <summary>
/// ZodSharp extensions for <see cref="ValidateContext"/>.
/// </summary>
public static class ZodValidateContextExtensions
{
	static readonly ConditionalWeakTable<ValidateContext, ZodValidationReport> Reports = [];

	extension(ValidateContext context)
	{
		/// <summary>
		/// Gets the full-fidelity ZodSharp errors reported against this context, or <see langword="null"/> when
		/// ZodSharp reported nothing. The report is held weakly, so it lives exactly as long as the context.
		/// </summary>
		/// <remarks>
		/// Microsoft.Extensions.Validation validates members that complete asynchronously on cloned contexts
		/// and merges only its own errors back. When ZodSharp runs nested under a DataAnnotations type whose
		/// sibling members are asynchronous, its report can therefore be attached to a clone; register an
		/// <see cref="IZodValidationReportSink"/> where that matters. When ZodSharp validates the root value
		/// (minimal API parameters, Blazor forms of ZodSharp models), the report is always on the caller's context.
		/// </remarks>
		public ZodValidationReport? ZodReport => Reports.TryGetValue(context, out var report) ? report : null;
	}

	internal static ZodValidationReport GetOrCreateZodReport(ValidateContext context) =>
		Reports.GetValue(context, static _ => new());
}

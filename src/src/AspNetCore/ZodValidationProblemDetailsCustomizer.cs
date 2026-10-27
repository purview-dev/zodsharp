#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Composes the ZodSharp enrichment into <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/>, so a
/// validation response written by the built-in Microsoft.Extensions.Validation pipeline carries the full ZodSharp
/// contract.
/// </summary>
/// <remarks>
/// A post-configure step, so it runs after every <c>Configure</c> of <see cref="ProblemDetailsOptions"/> - including
/// <c>AddProblemDetails(o =&gt; o.CustomizeProblemDetails = ...)</c> calls made before or after
/// <c>AddZodSharpProblemDetails()</c>. The callback already present is kept and runs after the ZodSharp one, so the
/// application can still change the result.
/// </remarks>
sealed class ZodValidationProblemDetailsCustomizer : IPostConfigureOptions<ProblemDetailsOptions>
{
	public void PostConfigure(string? name, ProblemDetailsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var existing = options.CustomizeProblemDetails;
		options.CustomizeProblemDetails = existing is null
			? Customize
			: context =>
			{
				Customize(context);
				existing(context);
			};
	}

	internal static void Customize(ProblemDetailsContext context)
	{
		if (context.ProblemDetails is not HttpValidationProblemDetails problem)
			return;

		var httpContext = context.HttpContext;
		var services = httpContext.RequestServices;
		if (services is null)
			return;

		var writer = ZodProblemDetailsWriter.GetOrCreate(services);
		if (services.GetService<ZodRequestValidationReport>() is { HasErrors: true } report)
		{
			writer.Apply(problem, report.Errors.AsSpan(), httpContext);
			if (problem.Status is { } status)
				httpContext.Response.StatusCode = status;

			return;
		}

		// No ZodSharp errors (a DataAnnotations-only failure, or a payload ZodSharp already wrote): leave it alone
		// apart from the optional issues projection, which ZodSharp's own payloads already carry.
		writer.ProjectExternalIssues(problem);
	}
}
#endif

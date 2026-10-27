#if NET11_0_OR_GREATER
using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The full-fidelity ZodSharp errors reported during one HTTP request by the Microsoft.Extensions.Validation
/// bridge. It lives in the request's <see cref="HttpContext.Features"/>, so it is created and discarded with the
/// request, and the ProblemDetails customization uses it to rebuild the built-in pipeline's response with the
/// ZodSharp contract.
/// </summary>
public sealed class ZodRequestValidationReport
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

	/// <summary>
	/// Gets the report for <paramref name="httpContext"/>, or <see langword="null"/> when nothing was reported.
	/// </summary>
	/// <param name="httpContext">The request.</param>
	/// <returns>The report, if any.</returns>
	public static ZodRequestValidationReport? Get(HttpContext httpContext)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		return httpContext.Features.Get<ZodRequestValidationReport>();
	}

	/// <summary>
	/// Appends errors to the report.
	/// </summary>
	/// <param name="errors">The errors.</param>
	public void Add(ImmutableArray<ValidationError> errors)
	{
		if (errors.IsDefaultOrEmpty)
			return;

		lock (_lock)
			_errors = _errors.AddRange(errors);
	}

	internal static ZodRequestValidationReport GetOrCreate(HttpContext httpContext)
	{
		var report = httpContext.Features.Get<ZodRequestValidationReport>();
		if (report is null)
		{
			report = new();
			httpContext.Features.Set(report);
		}

		return report;
	}
}

/// <summary>
/// The bridge's report sink for HTTP requests. Errors are stored on the current request rather than in the DI
/// scope: on Blazor Server a DI scope is the whole circuit, so a scoped collector would grow for as long as the
/// user stays connected. Outside an HTTP request that is still being handled - an interactive Blazor circuit (a
/// WebSocket or other long-lived connection whose response has started), WebAssembly, a background service - the
/// write is discarded, because nothing will turn it into a response.
/// </summary>
sealed class HttpRequestValidationReportSink(IHttpContextAccessor httpContextAccessor) : IZodValidationReportSink
{
	public void Add(Type validatedType, string pathPrefix, ImmutableArray<ValidationError> errors)
	{
		if (errors.IsDefaultOrEmpty)
			return;

		var httpContext = httpContextAccessor.HttpContext;
		if (httpContext is null || httpContext.Response.HasStarted || httpContext.WebSockets.IsWebSocketRequest)
			return;

		ZodRequestValidationReport.GetOrCreate(httpContext).Add(errors);
	}
}
#endif

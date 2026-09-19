using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ZodSharp.AspNetCore;

/// <summary>
/// An <see cref="IExceptionHandler"/> that converts a thrown <see cref="Core.ZodException"/>
/// into a standard <see cref="HttpValidationProblemDetails"/> response,
/// resolving <see cref="ErrorType"/>s from the configured <see cref="ZodProblemDetailsOptions.Registry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Register with <c>services.AddZodSharpProblemDetails()</c> and ensure
/// <c>app.UseExceptionHandler()</c> is present in the pipeline, otherwise the handler is never invoked.
/// </para>
/// </remarks>
public sealed class ZodExceptionHandler : IExceptionHandler
{
	static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

	readonly ZodProblemDetailsOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodExceptionHandler"/> class.
	/// </summary>
	public ZodExceptionHandler(IOptions<ZodProblemDetailsOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		if (exception is not Core.ZodException zodException)
			return false;

		var defaultStatusCode =
			_options.StatusCodeSelector?.Invoke(zodException.Errors) ?? StatusCodes.Status400BadRequest;

		var problem = zodException.ToHttpValidationProblemDetails(_options.Registry, defaultStatusCode);
		problem.Extensions["traceId"] = httpContext.TraceIdentifier;

		httpContext.Response.StatusCode = problem.Status!.Value;
		await httpContext.Response.WriteAsJsonAsync(
			problem,
			s_jsonOptions,
			"application/problem+json",
			cancellationToken
		);

		return true;
	}
}

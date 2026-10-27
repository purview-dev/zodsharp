using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// An <see cref="IExceptionHandler"/> that converts a thrown <see cref="ZodException"/> into the ZodSharp
/// ProblemDetails contract, written by <see cref="ZodProblemDetailsWriter"/> - the same body the endpoint filter
/// and <see cref="ZodTypedResults"/> produce.
/// </summary>
/// <remarks>
/// <para>
/// Register with <c>services.AddZodSharpProblemDetails()</c> and ensure
/// <c>app.UseExceptionHandler()</c> is present in the pipeline, otherwise the handler is never invoked.
/// </para>
/// </remarks>
public sealed class ZodExceptionHandler : IExceptionHandler
{
	readonly ZodProblemDetailsWriter _writer;

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodExceptionHandler"/> class with a writer built from
	/// <paramref name="options"/>.
	/// </summary>
	public ZodExceptionHandler(IOptions<ZodProblemDetailsOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_writer = new ZodProblemDetailsWriter(options);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ZodExceptionHandler"/> class with the registered writer.
	/// </summary>
	/// <param name="options">The options; unused when <paramref name="writer"/> is supplied.</param>
	/// <param name="writer">The shared ProblemDetails writer.</param>
	public ZodExceptionHandler(IOptions<ZodProblemDetailsOptions> options, ZodProblemDetailsWriter writer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(writer);
		_writer = writer;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryHandleAsync(
		HttpContext httpContext,
		Exception exception,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(httpContext);

		if (exception is not ZodException zodException)
			return false;

		var problem = _writer.Create(zodException.Errors, httpContext);
		await _writer.WriteAsync(httpContext, problem, exception).ConfigureAwait(false);

		return true;
	}
}

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

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
/// <para>
/// A <see cref="BadHttpRequestException"/> or <see cref="JsonException"/> whose inner exceptions carry a
/// <see cref="ZodException"/> - a request body rejected by a strict value object converter while it was deserialized -
/// is handled too: the errors are located under the member that held the value, with member names mapped from the
/// endpoint's body contract (see <see cref="ZodJsonExceptions"/>), so the body matches what validating the bound model
/// reports. Minimal APIs only throw on a bad request in Development unless
/// <see cref="ZodProblemDetailsOptions.HandleBindingFailures"/> is set (on .NET 11 binding writes a validation problem
/// instead, which <c>AddZodSharpProblemDetails</c> rewrites the same way). Any other bad request is left to the next
/// handler, unless <see cref="ZodProblemDetailsOptions.HandleBindingFailures"/> is set, when it is answered with its
/// own status code and a standard ProblemDetails body.
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

		ImmutableArray<ValidationError> errors;
		if (exception is ZodException zodException)
			errors = zodException.Errors;
		else if (
			exception is BadHttpRequestException or JsonException
			&& ZodJsonExceptions.TryGetErrors(exception, ResolveBodyTypeInfo(httpContext), out var rerooted)
		)
			errors = rerooted;
		else if (exception is BadHttpRequestException badRequest && _writer.Options.HandleBindingFailures)
		{
			// HandleBindingFailures made binding throw instead of answering; answer as binding would have, plus a body.
			await ZodProblemDetailsWriter
				.WriteStatusAsync(httpContext, badRequest.StatusCode, exception)
				.ConfigureAwait(false);
			return true;
		}
		else
			return false;

		var problem = _writer.Create(errors, httpContext);
		await _writer.WriteAsync(httpContext, problem, exception).ConfigureAwait(false);

		return true;
	}

	/// <summary>
	/// The contract of the endpoint's body, from its <see cref="IAcceptsMetadata"/> and the minimal-API JSON options,
	/// so the JSON names in a deserialization failure's path map back to member names. The exception handler middleware
	/// clears the endpoint before handlers run, so it is read from <see cref="IExceptionHandlerFeature"/> first.
	/// </summary>
	internal static JsonTypeInfo? ResolveBodyTypeInfo(HttpContext httpContext)
	{
		var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();
		if (endpoint?.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType is not { } requestType)
			return null;

		var options = httpContext.RequestServices?.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
		if (options is null)
			return null;

		try
		{
			return options.TryGetTypeInfo(requestType, out var typeInfo) ? typeInfo : null;
		}
		catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentException)
		{
			// The errors are still reported, under the JSON names.
			return null;
		}
	}
}

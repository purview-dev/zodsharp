#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public class HttpRequestValidationReportSinkTests
{
	static readonly System.Collections.Immutable.ImmutableArray<ValidationError> Errors =
	[
		ValidationError.Create("too_small", "Too small", ZodPath.Member("Quantity")),
	];

	[Test]
	public async Task Add_GivenActiveRequest_StoresErrorsOnTheRequest()
	{
		// Arrange
		DefaultHttpContext httpContext = new();
		HttpRequestValidationReportSink sink = new(new HttpContextAccessor { HttpContext = httpContext });

		// Act
		sink.Add(typeof(object), string.Empty, Errors);
		sink.Add(typeof(object), string.Empty, Errors);

		// Assert
		await Assert.That(ZodRequestValidationReport.Get(httpContext)!.Errors.Length).IsEqualTo(2);
	}

	[Test]
	public async Task Add_GivenNoHttpContext_DiscardsErrors()
	{
		// Arrange - an interactive Blazor circuit, WebAssembly or a background service has no current request.
		HttpRequestValidationReportSink sink = new(new HttpContextAccessor());

		// Act
		sink.Add(typeof(object), string.Empty, Errors);

		// Assert - nothing to observe beyond not throwing and not retaining anything.
		await Assert.That(new HttpContextAccessor().HttpContext).IsNull();
	}

	[Test]
	public async Task Add_GivenResponseAlreadyStarted_DiscardsErrors()
	{
		// Arrange - a long-lived connection (a Blazor Server circuit) whose response has started.
		DefaultHttpContext httpContext = new();
		httpContext.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
		HttpRequestValidationReportSink sink = new(new HttpContextAccessor { HttpContext = httpContext });

		// Act
		sink.Add(typeof(object), string.Empty, Errors);

		// Assert
		await Assert.That(ZodRequestValidationReport.Get(httpContext)).IsNull();
	}

	[Test]
	public async Task Get_GivenSeparateRequests_DoesNotShareErrors()
	{
		// Arrange
		DefaultHttpContext first = new();
		DefaultHttpContext second = new();
		HttpContextAccessor accessor = new();
		HttpRequestValidationReportSink sink = new(accessor);

		// Act
		accessor.HttpContext = first;
		sink.Add(typeof(object), string.Empty, Errors);
		accessor.HttpContext = second;

		// Assert
		await Assert.That(ZodRequestValidationReport.Get(first)!.HasErrors).IsTrue();
		await Assert.That(ZodRequestValidationReport.Get(second)).IsNull();
	}

	sealed class StartedResponseFeature : HttpResponseFeature
	{
		public override bool HasStarted => true;
	}
}
#endif

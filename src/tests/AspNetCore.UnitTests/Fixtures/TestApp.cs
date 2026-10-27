using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ZodSharp.AspNetCore.Fixtures;

/// <summary>
/// An in-memory minimal-API host for integration tests.
/// </summary>
public sealed partial class TestApp : IAsyncDisposable
{
	TestApp(WebApplication app)
	{
		App = app;
		Client = app.GetTestClient();
	}

	public WebApplication App { get; }

	public HttpClient Client { get; }

	public static async Task<TestApp> StartAsync(
		Action<WebApplicationBuilder> configure,
		Action<WebApplication> map,
		CancellationToken cancellationToken
	)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		builder.Logging.ClearProviders();
		configure(builder);

		var app = builder.Build();
		map(app);
		await app.StartAsync(cancellationToken);

		return new TestApp(app);
	}

	public static Task<TestApp> StartAsync(
		Action<IServiceCollection> services,
		Action<WebApplication> map,
		CancellationToken cancellationToken
	) => StartAsync(builder => services(builder.Services), map, cancellationToken);

	/// <summary>
	/// Replaces the per-request trace id so bodies from different requests can be compared byte for byte.
	/// </summary>
	public static string NormalizeTraceId(string json) => TraceIdPattern().Replace(json, "\"traceId\":\"*\"");

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await App.DisposeAsync();
	}

	[GeneratedRegex("\"traceId\":\"[^\"]*\"")]
	private static partial Regex TraceIdPattern();
}

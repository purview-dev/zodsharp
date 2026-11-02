using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// Starts the sample in memory: the same <see cref="SampleApp"/> registration and pipeline <c>Program.cs</c> runs,
/// on a <see cref="TestServer"/> instead of Kestrel.
/// </summary>
sealed class SampleAppHost : IAsyncDisposable
{
	SampleAppHost(WebApplication app)
	{
		App = app;
		Client = app.GetTestClient();
	}

	public WebApplication App { get; }

	public HttpClient Client { get; }

	public static async Task<SampleAppHost> StartAsync(
		CancellationToken cancellationToken,
		IDictionary<string, string?>? settings = null
	)
	{
		var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
		builder.WebHost.UseTestServer();
		builder.Logging.ClearProviders();

		// The sample's appsettings.json lives beside the sample, not the test assembly, so the validated options
		// are supplied here; a test can override them to exercise startup validation.
		builder.Configuration.AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				["Sample:ApiName"] = "Smoke test",
				["Sample:SupportEmail"] = "support@example.com",
				["Sample:MaxLinesPerOrder"] = "20",
			}
		);
		if (settings is not null)
			builder.Configuration.AddInMemoryCollection(settings);

		SampleApp.ConfigureServices(builder);

		var app = builder.Build();
		SampleApp.Configure(app);

		try
		{
			await app.StartAsync(cancellationToken);
		}
		catch
		{
			await app.DisposeAsync();
			throw;
		}

		return new SampleAppHost(app);
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await App.DisposeAsync();
	}
}

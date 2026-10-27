using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZodSharp.Blazor.Ssr;

/// <summary>
/// Hosts the test assembly's Razor components in-process with static server-side rendering, the way a Blazor Web
/// App serves a form that is not interactive.
/// </summary>
sealed class StaticSsrHost : IAsyncDisposable
{
	readonly WebApplication _app;

	StaticSsrHost(WebApplication app, HttpClient client)
	{
		_app = app;
		Client = client;
	}

	public HttpClient Client { get; }

	public IServiceProvider Services => _app.Services;

	public static async Task<StaticSsrHost> StartAsync(
		Action<IServiceCollection> configure,
		ILoggerProvider? logger = null,
		CancellationToken cancellationToken = default
	)
	{
		var builder = WebApplication.CreateBuilder(
			new WebApplicationOptions
			{
				EnvironmentName = Environments.Development,
				ApplicationName = typeof(SsrApp).Assembly.GetName().Name,
			}
		);
		builder.WebHost.UseTestServer();
		if (logger is not null)
			builder.Logging.AddProvider(logger);

		configure(builder.Services);

		var app = builder.Build();
		app.UseAntiforgery();
		app.MapRazorComponents<SsrApp>().DisableAntiforgery();
		await app.StartAsync(cancellationToken);
		return new(app, app.GetTestClient());
	}

	public async Task<IHtmlDocument> GetAsync(string path, CancellationToken cancellationToken)
	{
		using var response = await Client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
		response.EnsureSuccessStatusCode();
		return await ParseAsync(response, cancellationToken);
	}

	public async Task<IHtmlDocument> PostAsync(
		string path,
		IEnumerable<KeyValuePair<string, string>> fields,
		CancellationToken cancellationToken
	)
	{
		using FormUrlEncodedContent content = new(fields);
		using var response = await Client.PostAsync(new Uri(path, UriKind.Relative), content, cancellationToken);
		return await ParseAsync(response, cancellationToken);
	}

	static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var html = await response.Content.ReadAsStringAsync(cancellationToken);
		return await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.DisposeAsync();
	}
}

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Builds an in-memory minimal API application and returns its OpenAPI document as served at
/// <c>/openapi/v1.json</c>, so tests assert the serialized output a client actually sees.
/// </summary>
static class OpenApiTestHost
{
	public static async Task<JsonObject> GetDocumentAsync(
		OpenApiSpecVersion version,
		Action<WebApplication> map,
		CancellationToken cancellationToken,
		Action<ZodOpenApiOptions>? configure = null,
		Action<IServiceCollection>? services = null,
		bool addZodSharp = true,
		string environment = "Production"
	)
	{
		var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = environment });
		builder.WebHost.UseTestServer();

		// The first AddZodSharp wins, so a test's own registration (a runtime schema, say) runs first.
		services?.Invoke(builder.Services);
		builder.Services.AddZodSharp(options => options.ScanAssemblies.Add(typeof(OpenApiTestHost).Assembly));
		builder.Services.AddOpenApi(options =>
		{
			options.OpenApiVersion = version;
			if (addZodSharp)
				options.AddZodSharp(configure);
		});

		await using var app = builder.Build();
		app.MapOpenApi();
		map(app);

		await app.StartAsync(cancellationToken);
		try
		{
			using var client = app.GetTestClient();
			var json = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), cancellationToken);
			return JsonNode.Parse(json)!.AsObject();
		}
		finally
		{
			await app.StopAsync(cancellationToken);
		}
	}

	public static JsonObject Component(this JsonObject document, string name) =>
		document["components"]!["schemas"]![name]!.AsObject();

	public static JsonObject Property(this JsonObject schema, string name) => schema["properties"]![name]!.AsObject();

	public static IEnumerable<OpenApiSpecVersion> Versions() =>
		[OpenApiSpecVersion.OpenApi3_1, OpenApiSpecVersion.OpenApi3_2];
}

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ZodSharp.AspNetCore.OpenApi.Fixtures;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// The fixture API every mapping test reads: one endpoint per fixture type. The documents are built once per
/// OpenAPI version, with and without the ZodSharp transformers, so each test compares the platform's output
/// with ZodSharp's.
/// </summary>
static class FixtureApi
{
	static readonly ConcurrentDictionary<
		(OpenApiSpecVersion Version, bool ZodSharp),
		Lazy<Task<JsonObject>>
	> Documents = new();

	public static Task<JsonObject> GetAsync(OpenApiSpecVersion version, bool zodSharp = true) =>
		Documents
			.GetOrAdd((version, zodSharp), key => new Lazy<Task<JsonObject>>(() =>
				OpenApiTestHost.GetDocumentAsync(
					key.Version,
					Map,
					CancellationToken.None,
					services: RegisterRuntimeSchemas,
					addZodSharp: key.ZodSharp
				)
			))
			.Value;

	public static void RegisterRuntimeSchemas(IServiceCollection services) =>
		services.AddZodSharp(options =>
		{
			options.ScanAssemblies.Add(typeof(FixtureApi).Assembly);
			options.ConfigureFactory = factory => factory.Register<Percentage>(new PercentageSchema());
		});

	public static void Map(WebApplication app)
	{
		app.MapPost("/strings", (StringRulesRequest request) => Results.Ok());
		app.MapPost("/numbers", (NumberRulesRequest request) => Results.Ok());
		app.MapPost("/shapes", (ShapesRequest request) => Results.Ok());
		app.MapPost("/conflict", (ConflictRequest request) => Results.Ok()).ProducesValidationProblem();
		app.MapGet("/search", ([AsParameters] SearchQuery query) => Results.Ok());
		app.MapPost("/plain", (PlainRequest request) => Results.Ok());
		app.MapPost("/polymorphic", (ShapeBase request) => Results.Ok());
		app.MapPost("/percentage", (Percentage request) => Results.Ok());
		app.MapPost("/pets", (PetUnion request) => Results.Ok());
	}
}

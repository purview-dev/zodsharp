using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ZodSharp.AspNetCore.OpenApi.Fixtures;

namespace ZodSharp.AspNetCore.OpenApi;

partial class ZodOpenApiSchemaTransformerTests
{
	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenDefaultEnumSerialization_DescribesEnumsAsNumbers(
		OpenApiSpecVersion version,
		CancellationToken cancellationToken
	)
	{
		// Arrange / Act
		var document = await GetEnumsDocumentAsync(version, configureJson: null, cancellationToken);

		// Assert
		await Assert.That(EnumOf(document, "Shade")).IsEqualTo("[0,1]");
		await Assert.That(EnumOf(document, "Tone")).IsEqualTo("""["Warm","Cool"]""");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenGlobalStringEnumConverter_DescribesEnumsByMemberName(
		OpenApiSpecVersion version,
		CancellationToken cancellationToken
	)
	{
		// Arrange / Act
		var document = await GetEnumsDocumentAsync(
			version,
			static options => options.Converters.Add(new JsonStringEnumConverter()),
			cancellationToken
		);

		// Assert
		await Assert.That(EnumOf(document, "Shade")).IsEqualTo("""["Light","Dark"]""");
		await Assert.That(EnumOf(document, "Tone")).IsEqualTo("""["Warm","Cool"]""");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenGlobalConverterWithNamingPolicy_DescribesEnumsAsTheyAreWritten(
		OpenApiSpecVersion version,
		CancellationToken cancellationToken
	)
	{
		// Arrange / Act
		var document = await GetEnumsDocumentAsync(
			version,
			static options => options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)),
			cancellationToken
		);

		// Assert
		await Assert.That(EnumOf(document, "Shade")).IsEqualTo("""["light","dark"]""");
	}

	static Task<JsonObject> GetEnumsDocumentAsync(
		OpenApiSpecVersion version,
		Action<JsonSerializerOptions>? configureJson,
		CancellationToken cancellationToken
	) =>
		OpenApiTestHost.GetDocumentAsync(
			version,
			static app => app.MapPost("/enums", (EnumsRequest request) => Results.Ok()),
			cancellationToken,
			services: services =>
			{
				FixtureApi.RegisterRuntimeSchemas(services);
				if (configureJson is not null)
					services.ConfigureHttpJsonOptions(options => configureJson(options.SerializerOptions));
			}
		);

	static string? EnumOf(JsonObject document, string component) =>
		document.Component(component)["enum"]?.ToJsonString();
}

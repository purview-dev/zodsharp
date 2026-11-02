using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ZodSharp.AspNetCore.OpenApi.Fixtures;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore.OpenApi;

public class ZodOpenApiOperationTransformerTests
{
	const string ProblemDetailsReference = "#/components/schemas/ZodValidationProblemDetails";

	public static IEnumerable<OpenApiSpecVersion> Versions() => OpenApiTestHost.Versions();

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task ValidationIssueSchema_GivenCodeAndCategory_DescribesEachAsWhatTheWriterPutsThere(
		OpenApiSpecVersion version
	)
	{
		// Arrange
		var document = await FixtureApi.GetAsync(version);

		// Act
		var properties = document.Component("ZodValidationIssue")["properties"]!;
		var code = (string?)properties["code"]!["description"];
		var category = (string?)properties["category"]!["description"];

		// Assert — ValidationIssue.Code is ZodSharp's own code; Category carries the Zod (v4) issue code.
		await Assert.That(code).IsNotNull().And.Contains("ZodSharp error code");
		await Assert.That(code!).DoesNotContain("Zod-compatible");
		await Assert.That(category).IsNotNull().And.Contains("Zod issue code");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenZodSharpBody_DescribesValidationProblemResponse(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		await Assert.That(Responses(platform, "/strings", "post")["400"]).IsNull();
		await Assert
			.That(ProblemSchemaReference(zodSharp, "/strings", "post", "400"))
			.IsEqualTo(ProblemDetailsReference);
		await Assert.That(zodSharp.Component("ZodValidationProblemDetails")["properties"]!["issues"]).IsNotNull();
		await Assert.That(zodSharp.Component("ZodValidationIssue")["properties"]!["segments"]).IsNotNull();
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenAsParametersContainer_DescribesValidationProblemResponse(
		OpenApiSpecVersion version
	)
	{
		// Act
		var document = await FixtureApi.GetAsync(version);

		// Assert
		await Assert.That(ProblemSchemaReference(document, "/search", "get", "400")).IsEqualTo(ProblemDetailsReference);
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenUnionOfZodSharpCases_DescribesValidationProblemResponse(
		OpenApiSpecVersion version
	)
	{
		// Act
		var document = await FixtureApi.GetAsync(version);

		// Assert
		await Assert.That(ProblemSchemaReference(document, "/pets", "post", "400")).IsEqualTo(ProblemDetailsReference);
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenProducesValidationProblem_MergesIntoDeclaredResponse(
		OpenApiSpecVersion version
	)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert - one 400, the developer's description kept, the schema replaced by the ZodSharp contract.
		var declared = Responses(platform, "/conflict", "post")["400"]!;
		var merged = Responses(zodSharp, "/conflict", "post")["400"]!;
		await Assert
			.That(ProblemSchemaReference(platform, "/conflict", "post", "400"))
			.IsEqualTo("#/components/schemas/HttpValidationProblemDetails");
		await Assert.That(merged["description"]!.ToJsonString()).IsEqualTo(declared["description"]!.ToJsonString());
		await Assert
			.That(ProblemSchemaReference(zodSharp, "/conflict", "post", "400"))
			.IsEqualTo(ProblemDetailsReference);
		await Assert.That(merged["content"]!.AsObject().Count).IsEqualTo(1);
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenUnknownParameterTypes_AddsNoResponse(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		await Assert
			.That(Responses(zodSharp, "/plain", "post").ToJsonString())
			.IsEqualTo(Responses(platform, "/plain", "post").ToJsonString());
	}

	[Test]
	public async Task TransformAsync_GivenNoValidatedEndpoint_AddsNoErrorContractComponents(
		CancellationToken cancellationToken
	)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/plain", (PlainRequest request) => Results.Ok()),
			cancellationToken
		);

		// Assert
		var schemas = document["components"]!["schemas"]!.AsObject();
		await Assert.That(schemas.ContainsKey("ZodValidationProblemDetails")).IsFalse();
		await Assert.That(schemas.ContainsKey("ZodValidationIssue")).IsFalse();
	}

	[Test]
	public async Task TransformAsync_GivenWithZodSharpValidationFilter_DescribesValidationProblemResponse(
		CancellationToken cancellationToken
	)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app =>
				app.MapPost("/filtered", (NumberRulesRequest request) => Results.Ok())
					.WithZodSharpValidation<NumberRulesRequest>(),
			cancellationToken
		);

		// Assert
		await Assert
			.That(ProblemSchemaReference(document, "/filtered", "post", "400"))
			.IsEqualTo(ProblemDetailsReference);
	}

	[Test]
	public async Task TransformAsync_GivenRegistered422ErrorType_DescribesUnprocessableEntityResponse(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(new ErrorType("order_locked", HttpStatus: StatusCodes.Status422UnprocessableEntity));

		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/strings", (StringRulesRequest request) => Results.Ok()),
			cancellationToken,
			services: services => services.AddZodSharpProblemDetails(options => options.Registry = registry)
		);

		// Assert
		await Assert
			.That(ProblemSchemaReference(document, "/strings", "post", "400"))
			.IsEqualTo(ProblemDetailsReference);
		await Assert
			.That(ProblemSchemaReference(document, "/strings", "post", "422"))
			.IsEqualTo(ProblemDetailsReference);
	}

	[Test]
	public async Task TransformAsync_GivenNo422ErrorType_DescribesBadRequestOnly(CancellationToken cancellationToken)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/strings", (StringRulesRequest request) => Results.Ok()),
			cancellationToken,
			services: services =>
				services.AddZodSharpProblemDetails(options => options.Registry = new ErrorTypeRegistry())
		);

		// Assert
		await Assert.That(Responses(document, "/strings", "post")["422"]).IsNull();
	}

	[Test]
	public async Task TransformAsync_GivenDescribeValidationProblemsDisabled_AddsNoResponse(
		CancellationToken cancellationToken
	)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/strings", (StringRulesRequest request) => Results.Ok()),
			cancellationToken,
			configure: options => options.DescribeValidationProblems = false
		);

		// Assert
		await Assert.That(Responses(document, "/strings", "post")["400"]).IsNull();
	}

	static JsonObject Responses(JsonObject document, string path, string method) =>
		document["paths"]![path]![method]!["responses"]!.AsObject();

	static string? ProblemSchemaReference(JsonObject document, string path, string method, string status) =>
		Responses(document, path, method)[status]
			?["content"]?["application/problem+json"]?["schema"]?["$ref"]?.GetValue<string>();
}

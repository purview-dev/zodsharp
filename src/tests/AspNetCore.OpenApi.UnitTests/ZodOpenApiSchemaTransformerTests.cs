using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ZodSharp.AspNetCore.OpenApi.Fixtures;

namespace ZodSharp.AspNetCore.OpenApi;

public partial class ZodOpenApiSchemaTransformerTests
{
	public static IEnumerable<OpenApiSpecVersion> Versions() => OpenApiTestHost.Versions();

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenUnknownType_LeavesPlatformSchemaUnchanged(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		await Assert
			.That(zodSharp.Component("PlainRequest").ToJsonString())
			.IsEqualTo(platform.Component("PlainRequest").ToJsonString());
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenZodSharpRequiredMembers_ListsReferenceMembersOnly(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert - non-nullable reference members and [RequiredZod] members are required; value-type members
		// (an absent value deserializes to its default) and nullable members are not.
		await Assert.That(platform.Component("ShapesRequest")["required"]).IsNull();
		await Assert
			.That(RequiredOf(zodSharp, "ShapesRequest"))
			.IsEquivalentTo(["home", "others", "reference", "scores", "tags", "title"]);
		await Assert.That(RequiredOf(zodSharp, "StringRulesRequest")).IsEquivalentTo(["name"]);
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenDataAnnotationThatDiffers_ZodSharpWins(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		var platformDifferent = platform.Component("ConflictRequest").Property("different");
		var zodSharpDifferent = zodSharp.Component("ConflictRequest").Property("different");
		await Assert.That(platformDifferent["maximum"]!.ToJsonString()).IsEqualTo("100");
		await Assert.That(zodSharpDifferent["maximum"]!.ToJsonString()).IsEqualTo("50");
		await Assert.That(zodSharpDifferent["minimum"]!.ToJsonString()).IsEqualTo("1");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenDataAnnotationThatAgrees_LeavesKeywordUntouched(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		await Assert
			.That(zodSharp.Component("ConflictRequest").Property("same").ToJsonString())
			.IsEqualTo(platform.Component("ConflictRequest").Property("same").ToJsonString());
	}

	[Test]
	public async Task TransformAsync_GivenConflictInDevelopment_LogsOnlyTheOverriddenKeyword(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using CapturingLoggerProvider logs = new();

		// Act
		await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/conflict", (ConflictRequest request) => Results.Ok()),
			cancellationToken,
			services: services =>
				services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(logs)),
			environment: "Development"
		);

		// Assert
		var overrides = logs
			.Messages.Where(static message => message.Contains("ZodSharp overrode", StringComparison.Ordinal))
			.ToList();
		await Assert.That(overrides).Count().IsEqualTo(1);
		await Assert.That(overrides[0]).Contains("'maximum'");
		await Assert.That(overrides[0]).Contains("ConflictRequest.different");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenPolymorphicZodSharpType_DescribesCasesWithOneOf(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		var platformShape = platform.Component("ShapeBase");
		var zodSharpShape = zodSharp.Component("ShapeBase");
		await Assert.That(platformShape["anyOf"]).IsNotNull();
		await Assert.That(zodSharpShape["anyOf"]).IsNull();
		await Assert.That(zodSharpShape["oneOf"]!.ToJsonString()).IsEqualTo(platformShape["anyOf"]!.ToJsonString());
		await Assert
			.That(zodSharpShape["discriminator"]!.ToJsonString())
			.IsEqualTo(platformShape["discriminator"]!.ToJsonString());
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenPolymorphicZodSharpType_KeepsCaseReferencesResolvable(
		OpenApiSpecVersion version
	)
	{
		// Act
		var document = await FixtureApi.GetAsync(version);

		// Assert - the cases keep the platform's polymorphic component names, which the discriminator maps to.
		var schemas = document["components"]!["schemas"]!.AsObject();
		var references = document.Component("ShapeBase")["oneOf"]!
			.AsArray()
			.Select(static item => item!["$ref"]!.GetValue<string>()["#/components/schemas/".Length..])
			.ToList();
		await Assert.That(references).IsEquivalentTo(["ShapeBaseCircleShape", "ShapeBaseSquareShape"]);
		await Assert.That(references.All(schemas.ContainsKey)).IsTrue();
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenCSharpUnion_KeepsAnyOfAndConstrainsCases(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert - the union is not restructured; ZodSharp's constraints land inside each case schema.
		await Assert
			.That(zodSharp.Component("PetUnion").ToJsonString())
			.IsEqualTo(platform.Component("PetUnion").ToJsonString());
		await Assert.That(platform.Component("CatCase").Property("name")["minLength"]).IsNull();
		await Assert.That(zodSharp.Component("CatCase").Property("name")["minLength"]!.ToJsonString()).IsEqualTo("2");
		await Assert.That(zodSharp.Component("DogCase").Property("barks")["minimum"]!.ToJsonString()).IsEqualTo("0");
	}

	[Test]
	public async Task TransformAsync_GivenDiscriminatedUnionsAsOneOfDisabled_KeepsAnyOf(
		CancellationToken cancellationToken
	)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/polymorphic", (ShapeBase request) => Results.Ok()),
			cancellationToken,
			configure: options => options.DiscriminatedUnionsAsOneOf = false
		);

		// Assert
		await Assert.That(document.Component("ShapeBase")["anyOf"]).IsNotNull();
		await Assert.That(document.Component("ShapeBase")["oneOf"]).IsNull();
	}

	[Test]
	public async Task TransformAsync_GivenVendorExtensionsDisabled_OmitsZodExtensions(
		CancellationToken cancellationToken
	)
	{
		// Act
		var document = await OpenApiTestHost.GetDocumentAsync(
			OpenApiSpecVersion.OpenApi3_2,
			app => app.MapPost("/strings", (StringRulesRequest request) => Results.Ok()),
			cancellationToken,
			configure: options => options.EmitVendorExtensions = false
		);

		// Assert - the keywords stay; only the x-zod-* descriptions go.
		var schema = document.Component("StringRulesRequest");
		var extensions = schema["properties"]!
			.AsObject()
			.SelectMany(static property => property.Value!.AsObject().Select(static keyword => keyword.Key))
			.Where(static keyword => keyword.StartsWith("x-zod", StringComparison.Ordinal))
			.ToList();
		await Assert.That(extensions).IsEmpty();
		await Assert.That(schema.Property("prefixed")["pattern"]!.GetValue<string>()).IsEqualTo("^ab");
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task TransformAsync_GivenAsParametersMembers_ConstrainsQueryParameters(OpenApiSpecVersion version)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);

		// Act
		var zodSharp = await FixtureApi.GetAsync(version);

		// Assert
		var platformTerm = Parameter(platform, "/search", "Term");
		var zodSharpTerm = Parameter(zodSharp, "/search", "Term");
		var zodSharpTake = Parameter(zodSharp, "/search", "Take");
		await Assert.That(platformTerm["minLength"]).IsNull();
		await Assert.That(zodSharpTerm["minLength"]!.ToJsonString()).IsEqualTo("3");
		await Assert.That(zodSharpTake["maximum"]!.ToJsonString()).IsEqualTo("50");
	}

	static List<string> RequiredOf(JsonObject document, string component) =>
		[.. document.Component(component)["required"]!.AsArray().Select(static node => node!.GetValue<string>())];

	static JsonObject Parameter(JsonObject document, string path, string name) =>
		document["paths"]![path]!["get"]!["parameters"]!
			.AsArray()
			.Single(parameter =>
				string.Equals(parameter!["name"]!.GetValue<string>(), name, StringComparison.Ordinal)
			)!["schema"]!.AsObject();

	sealed class CapturingLoggerProvider : ILoggerProvider
	{
		readonly ConcurrentQueue<string> _messages = new();

		public IReadOnlyCollection<string> Messages => _messages;

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

		public void Dispose() { }

		sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter
			) => messages.Enqueue(formatter(state, exception));
		}
	}
}

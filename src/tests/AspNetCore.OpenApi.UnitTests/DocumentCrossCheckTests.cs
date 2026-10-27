using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.OpenApi;
using ZodSharp.AspNetCore.OpenApi.Fixtures;
using JsonSchemaDocument = Json.Schema.JsonSchema;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Proves the document does not lie: a JSON Schema validator evaluates fixture payloads against the schemas
/// the document emits, and its verdict must match ZodSharp's for every payload ZodSharp accepts and every
/// payload it rejects for a reason JSON Schema can express.
/// </summary>
public class DocumentCrossCheckTests
{
	static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

	/// <summary>A payload, the component it targets, and whether ZodSharp accepts it.</summary>
	public sealed record Payload(string Component, string Json, bool Accepted, string Reason)
	{
		public override string ToString() => $"{Component}: {Reason}";
	}

	static readonly Payload[] Payloads =
	[
		new(
			"StringRulesRequest",
			"""
			{
				"name": "ab", "email": "a@example.com", "legacyEmail": "b@example.com", "website": "https://example.com",
				"slug": "abc", "prefixed": "abc", "suffixed": "xyz", "containing": "amidb", "bracketed": "abxyz",
				"identifier": "6f1c2d3e-4a5b-4c6d-8e7f-0123456789ab", "addressV4": "192.168.0.1", "addressV6": "::1",
				"network": "10.0.0.0/8", "timestamp": "2024-01-01T10:00:00Z", "day": "2024-01-31", "timeOfDay": "10:30",
				"period": "P1D", "payload": "aGVsbG8=", "colour": "red", "username": "bob", "described": "x"
			}
			""",
			Accepted: true,
			"every constraint satisfied"
		),
		new("StringRulesRequest", """{ "name": "ab" }""", Accepted: true, "optional members absent"),
		new("StringRulesRequest", """{ "name": "a" }""", Accepted: false, "minLength"),
		new("StringRulesRequest", """{ "name": "abcdefghijk" }""", Accepted: false, "maxLength"),
		new("StringRulesRequest", """{ }""", Accepted: false, "required member absent"),
		new("StringRulesRequest", """{ "name": "ab", "email": "not-an-email" }""", Accepted: false, "format: email"),
		new("StringRulesRequest", """{ "name": "ab", "legacyEmail": "nope" }""", Accepted: false, "[EmailAddress]"),
		new("StringRulesRequest", """{ "name": "ab", "slug": "ABC" }""", Accepted: false, "pattern"),
		new("StringRulesRequest", """{ "name": "ab", "prefixed": "xab" }""", Accepted: false, "startsWith"),
		new("StringRulesRequest", """{ "name": "ab", "suffixed": "yzx" }""", Accepted: false, "endsWith"),
		new("StringRulesRequest", """{ "name": "ab", "containing": "nope" }""", Accepted: false, "includes"),
		new("StringRulesRequest", """{ "name": "ab", "bracketed": "abx" }""", Accepted: false, "allOf patterns"),
		new(
			"StringRulesRequest",
			"""{ "name": "ab", "identifier": "6f1c2d3e-4a5b-1c6d-8e7f-0123456789ab" }""",
			Accepted: false,
			"uuid version"
		),
		new("StringRulesRequest", """{ "name": "ab", "addressV4": "::1" }""", Accepted: false, "format: ipv4"),
		new("StringRulesRequest", """{ "name": "ab", "addressV6": "10.0.0.1" }""", Accepted: false, "format: ipv6"),
		new(
			"StringRulesRequest",
			"""{ "name": "ab", "timestamp": "2024-01-01T10:00:00+01:00" }""",
			Accepted: false,
			"UTC date-time"
		),
		new("StringRulesRequest", """{ "name": "ab", "day": "2024-13-01" }""", Accepted: false, "format: date"),
		new("StringRulesRequest", """{ "name": "ab", "timeOfDay": "25:00" }""", Accepted: false, "time pattern"),
		new("StringRulesRequest", """{ "name": "ab", "colour": "blue" }""", Accepted: false, "enum"),
		new("StringRulesRequest", """{ "name": "ab", "username": "admin" }""", Accepted: false, "not enum"),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 50.5, "bounded": 5, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: true,
			"every constraint satisfied"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 0, "ratio": 50.5, "bounded": 5, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"minimum"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 11, "ratio": 50.5, "bounded": 5, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"maximum"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 0, "bounded": 5, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"exclusiveMinimum"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 100, "bounded": 5, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"exclusiveMaximum"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 50.5, "bounded": 7, "step": 10, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"maximum (LessThanOrEqual)"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 50.5, "bounded": 5, "step": 7, "whole": 3, "safe": 7 }""",
			Accepted: false,
			"multipleOf"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 50.5, "bounded": 5, "step": 10, "whole": 3.5, "safe": 7 }""",
			Accepted: false,
			"integer"
		),
		new(
			"NumberRulesRequest",
			"""{ "count": 5, "ratio": 50.5, "bounded": 5, "step": 10, "whole": 3, "safe": 30000000000 }""",
			Accepted: false,
			"safe integer"
		),
		new(
			"ShapesRequest",
			"""
			{
				"tags": ["a"], "scores": { "x": 1 }, "reference": "r", "title": "t", "createdAt": "2024-01-01T00:00:00Z",
				"birthday": "2024-01-01", "elapsed": "00:00:01", "home": { "street": "Main" },
				"others": [{ "street": "Side", "postCode": "12345" }]
			}
			""",
			Accepted: true,
			"every constraint satisfied"
		),
		new(
			"ShapesRequest",
			"""{ "tags": [], "scores": {}, "reference": "r", "title": "t", "home": { "street": "Main" }, "others": [] }""",
			Accepted: false,
			"minItems"
		),
		new(
			"ShapesRequest",
			"""{ "tags": ["a","b","c","d"], "scores": {}, "reference": "r", "title": "t", "home": { "street": "Main" }, "others": [] }""",
			Accepted: false,
			"maxItems"
		),
		new(
			"ShapesRequest",
			"""{ "tags": ["a"], "scores": { "a": 1, "b": 2, "c": 3 }, "reference": "r", "title": "t", "home": { "street": "Main" }, "others": [] }""",
			Accepted: false,
			"maxProperties"
		),
		new(
			"ShapesRequest",
			"""{ "tags": ["a"], "scores": {}, "reference": null, "title": "t", "home": { "street": "Main" }, "others": [] }""",
			Accepted: false,
			"null where ZodSharp requires a value"
		),
		new(
			"ShapesRequest",
			"""{ "tags": ["a"], "scores": {}, "reference": "r", "title": "t", "home": { "street": "" }, "others": [] }""",
			Accepted: false,
			"nested minLength"
		),
		new(
			"ShapesRequest",
			"""{ "tags": ["a"], "scores": {}, "reference": "r", "title": "t", "home": { "street": "Main" }, "others": [{ "street": "S", "postCode": "abc" }] }""",
			Accepted: false,
			"nested collection pattern"
		),
	];

	public static IEnumerable<Func<(OpenApiSpecVersion, Payload)>> Cases()
	{
		foreach (var version in OpenApiTestHost.Versions())
		{
			foreach (var payload in Payloads)
				yield return () => (version, payload);
		}
	}

	[Test]
	[MethodDataSource(nameof(Cases))]
	public async Task Document_GivenFixturePayload_AgreesWithZodSharp(OpenApiSpecVersion version, Payload payload)
	{
		// Arrange
		var document = await FixtureApi.GetAsync(version);
		var schema = BuildSchema(document, payload.Component);
		using var instance = JsonDocument.Parse(payload.Json);

		// Act
		var zodSharpAccepts = ValidateWithZodSharp(payload);
		var schemaAccepts = schema
			.Evaluate(instance.RootElement, new EvaluationOptions { RequireFormatValidation = true })
			.IsValid;

		// Assert
		await Assert.That(zodSharpAccepts).IsEqualTo(payload.Accepted);
		await Assert.That(schemaAccepts).IsEqualTo(payload.Accepted);
	}

	[Test]
	[MethodDataSource(nameof(Versions))]
	public async Task Document_GivenRuleWithoutJsonSchemaKeyword_DescribesItWithVendorExtension(
		OpenApiSpecVersion version
	)
	{
		// Arrange - ZodSharp rejects these, but JSON Schema has no keyword for a JWT or a CIDR block, so the
		// schema accepts them. The document says so instead of staying silent.
		Payload jwt = new("StringRulesRequest", """{ "name": "ab", "token": "x" }""", Accepted: false, "jwt");
		Payload cidr = new(
			"StringRulesRequest",
			"""{ "name": "ab", "network": "10.0.0.1" }""",
			Accepted: false,
			"cidr"
		);
		var document = await FixtureApi.GetAsync(version);

		// Act
		var jwtAccepted = ValidateWithZodSharp(jwt);
		var cidrAccepted = ValidateWithZodSharp(cidr);

		// Assert
		var properties = document.Component("StringRulesRequest");
		await Assert.That(jwtAccepted).IsFalse();
		await Assert.That(cidrAccepted).IsFalse();
		await Assert.That(properties.Property("token")["x-zodsharp-unsupported"]).IsNotNull();
		await Assert.That(properties.Property("network")["x-zodsharp-unsupported"]).IsNotNull();
	}

	public static IEnumerable<OpenApiSpecVersion> Versions() => OpenApiTestHost.Versions();

	static bool ValidateWithZodSharp(Payload payload) =>
		payload.Component switch
		{
			"StringRulesRequest" => StringRulesRequestSchema
				.Validate(JsonSerializer.Deserialize<StringRulesRequest>(payload.Json, Web)!)
				.IsSuccess,
			"NumberRulesRequest" => NumberRulesRequestSchema
				.Validate(JsonSerializer.Deserialize<NumberRulesRequest>(payload.Json, Web)!)
				.IsSuccess,
			"ShapesRequest" => ShapesRequestSchema
				.Validate(JsonSerializer.Deserialize<ShapesRequest>(payload.Json, Web)!)
				.IsSuccess,
			_ => throw new ArgumentOutOfRangeException(nameof(payload), payload.Component, "Unknown component."),
		};

	/// <summary>
	/// Turns the document's component schemas into one JSON Schema 2020-12 document: the components become
	/// <c>$defs</c>, and the root references the component under test.
	/// </summary>
	static JsonSchemaDocument BuildSchema(JsonObject document, string component)
	{
		var components = document["components"]!["schemas"]!
			.ToJsonString()
			.Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal);

		JsonObject root = new()
		{
			["$schema"] = "https://json-schema.org/draft/2020-12/schema",
			["$ref"] = $"#/$defs/{component}",
			["$defs"] = JsonNode.Parse(components),
		};

		// The schema keeps referring to the element it was built from, so it must outlive any JsonDocument.
		using var json = JsonDocument.Parse(root.ToJsonString());
		var element = json.RootElement.Clone();
		return JsonSchemaDocument.Build(
			element,
			new BuildOptions { SchemaRegistry = new SchemaRegistry() },
			new Uri($"https://zodsharp.test/{Guid.NewGuid():N}")
		);
	}
}

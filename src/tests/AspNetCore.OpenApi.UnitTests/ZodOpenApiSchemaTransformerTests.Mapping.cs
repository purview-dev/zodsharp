using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// One row per keyword mapping: what the platform emits on its own, and what the document says once
/// ZodSharp's transformers run, for both OpenAPI 3.1 and 3.2.
/// </summary>
public partial class ZodOpenApiSchemaTransformerTests
{
	/// <summary>A keyword mapping row. <c>null</c> means the keyword is absent.</summary>
	public sealed record MappingRow(
		string Rule,
		string Component,
		string? Property,
		string Keyword,
		string? Platform,
		string? ZodSharp
	)
	{
		public override string ToString() => $"{Rule}: {Component}.{Property}.{Keyword}";
	}

	static readonly MappingRow[] Rows =
	[
		new("MinLength (string)", "StringRulesRequest", "name", "minLength", null, "2"),
		new("MaxLength (string)", "StringRulesRequest", "name", "maxLength", null, "10"),
		new("MinLength (collection)", "ShapesRequest", "tags", "minItems", "1", "1"),
		new("MaxLength (collection)", "ShapesRequest", "tags", "maxItems", "3", "3"),
		new("MaxLength (map)", "ShapesRequest", "scores", "maxProperties", null, "2"),
		new("MinValue", "NumberRulesRequest", "count", "minimum", null, "1"),
		new("MaxValue", "NumberRulesRequest", "count", "maximum", null, "10"),
		new("GreaterThanOrEqual", "NumberRulesRequest", "bounded", "minimum", null, "5"),
		new("LessThanOrEqual", "NumberRulesRequest", "bounded", "maximum", null, "6"),
		new("GreaterThan", "NumberRulesRequest", "ratio", "exclusiveMinimum", null, "0"),
		new("LessThan", "NumberRulesRequest", "ratio", "exclusiveMaximum", null, "100"),
		new("MultipleOf", "NumberRulesRequest", "step", "multipleOf", null, "5"),
		new("Int", "NumberRulesRequest", "whole", "type", """["number","string"]""", """["integer","string"]"""),
		new("SafeInteger", "NumberRulesRequest", "safe", "type", """["number","string"]""", """["integer","string"]"""),
		new("SafeInteger", "NumberRulesRequest", "safe", "minimum", null, "-2147483648"),
		new("SafeInteger", "NumberRulesRequest", "safe", "maximum", null, "2147483647"),
		new("Regex", "StringRulesRequest", "slug", "pattern", null, "\"^[a-z]+$\""),
		new(
			"Several patterns",
			"StringRulesRequest",
			"bracketed",
			"allOf",
			null,
			"""[{"pattern":"^ab"},{"pattern":"yz$"}]"""
		),
		new("Several patterns", "StringRulesRequest", "bracketed", "pattern", null, null),
		new("StartsWith", "StringRulesRequest", "prefixed", "pattern", null, "\"^ab\""),
		new("StartsWith", "StringRulesRequest", "prefixed", "x-zod-startsWith", null, "\"ab\""),
		new("EndsWith", "StringRulesRequest", "suffixed", "pattern", null, "\"yz$\""),
		new("EndsWith", "StringRulesRequest", "suffixed", "x-zod-endsWith", null, "\"yz\""),
		new("Includes", "StringRulesRequest", "containing", "pattern", null, "\"mid\""),
		new("Includes", "StringRulesRequest", "containing", "x-zod-includes", null, "\"mid\""),
		new("Email", "StringRulesRequest", "email", "format", null, "\"email\""),
		new("EmailAddress", "StringRulesRequest", "legacyEmail", "format", null, "\"email\""),
		new("Url", "StringRulesRequest", "website", "format", null, "\"uri\""),
		new("Uuid(version)", "StringRulesRequest", "identifier", "format", null, "\"uuid\""),
		new("Uuid(version)", "StringRulesRequest", "identifier", "x-zod-uuidVersion", null, "4"),
		new("IPAddress(v4)", "StringRulesRequest", "addressV4", "format", null, "\"ipv4\""),
		new("IPAddress(v6)", "StringRulesRequest", "addressV6", "format", null, "\"ipv6\""),
		new("Cidr", "StringRulesRequest", "network", "x-zod-cidr", null, "\"any\""),
		new("Cidr", "StringRulesRequest", "network", "x-zodsharp-unsupported", null, """["CidrRule"]"""),
		new("DatetimeString", "StringRulesRequest", "timestamp", "format", null, "\"date-time\""),
		new("DateString", "StringRulesRequest", "day", "format", null, "\"date\""),
		new("TimeString", "StringRulesRequest", "timeOfDay", "x-zod-format", null, "\"time\""),
		new("Duration", "StringRulesRequest", "period", "format", null, "\"duration\""),
		new("Base64", "StringRulesRequest", "payload", "contentEncoding", null, "\"base64\""),
		new("Jwt", "StringRulesRequest", "token", "x-zod-jwt", null, "true"),
		new("AllowedValues", "StringRulesRequest", "colour", "enum", null, """["red","green"]"""),
		new("DeniedValues", "StringRulesRequest", "username", "not", null, """{"enum":["admin"]}"""),
		new("Enum", "Shade", null, "enum", null, "[0,1]"),
		new(
			"Display(Description)",
			"StringRulesRequest",
			"described",
			"description",
			null,
			"\"The display description.\""
		),
		new("Nullable", "ShapesRequest", "nickname", "type", """["null","string"]""", """["null","string"]"""),
		new(
			"RequiredZod on a nullable member",
			"ShapesRequest",
			"reference",
			"type",
			"""["null","string"]""",
			"\"string\""
		),
		new("DateTime", "ShapesRequest", "createdAt", "format", "\"date-time\"", "\"date-time\""),
		new("DateOnly", "ShapesRequest", "birthday", "format", "\"date\"", "\"date\""),
		// System.Text.Json writes a TimeSpan as "c" (d.hh:mm:ss), not an ISO 8601 duration.
		new("TimeSpan", "ShapesRequest", "elapsed", "format", null, null),
		new("Nested", "Address", "street", "minLength", null, "1"),
		new("Nested", "Address", "postCode", "pattern", null, "\"^[0-9]{5}$\""),
		new("Runtime schema (IZodSchemaFactory)", "Percentage", null, "maximum", null, "100"),
	];

	public static IEnumerable<Func<(OpenApiSpecVersion, MappingRow)>> MappingRows()
	{
		foreach (var version in OpenApiTestHost.Versions())
		{
			foreach (var row in Rows)
				yield return () => (version, row);
		}
	}

	[Test]
	[MethodDataSource(nameof(MappingRows))]
	public async Task TransformAsync_GivenMappedRule_DescribesWhatZodSharpEnforces(
		OpenApiSpecVersion version,
		MappingRow row
	)
	{
		// Arrange
		var platform = await FixtureApi.GetAsync(version, zodSharp: false);
		var zodSharp = await FixtureApi.GetAsync(version);

		// Act
		var platformValue = Keyword(platform, row);
		var zodSharpValue = Keyword(zodSharp, row);

		// Assert
		await Assert.That(Render(platformValue)).IsEqualTo(Render(Parse(row.Platform)));
		await Assert.That(Render(zodSharpValue)).IsEqualTo(Render(Parse(row.ZodSharp)));
	}

	static JsonNode? Keyword(JsonObject document, MappingRow row)
	{
		var schema = document.Component(row.Component);
		if (row.Property is not null)
			schema = schema.Property(row.Property);

		return schema[row.Keyword];
	}

	static JsonNode? Parse(string? json) => json is null ? null : JsonNode.Parse(json);

	static string Render(JsonNode? node) => node?.ToJsonString() ?? "<absent>";
}

using Microsoft.OpenApi;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Builds the component schemas of the ZodSharp validation error contract.
/// </summary>
/// <remarks>
/// <c>ZodValidationProblemDetails</c> is RFC 9457 <c>HttpValidationProblemDetails</c> (<c>type</c>, <c>title</c>,
/// <c>status</c>, <c>detail</c>, <c>instance</c>, <c>errors</c>) plus the structured <c>issues</c> and the
/// <c>traceId</c>. <c>ZodValidationIssue</c> is one structured issue: the dotted <c>path</c>, its
/// <c>segments</c> (member names and indexes), the Zod <c>code</c>, its <c>category</c> and <c>origin</c>, the
/// bounds of a size issue, the <c>message</c>, per-issue <c>parameters</c> and the case errors of a union.
/// </remarks>
static class ZodValidationProblemSchemas
{
	public static void Register(OpenApiDocument document)
	{
		document.Components ??= new OpenApiComponents();
		document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

		document.Components.Schemas.TryAdd(ZodOpenApiOptions.ValidationIssueSchemaId, CreateIssue(document));
		document.Components.Schemas.TryAdd(
			ZodOpenApiOptions.ValidationProblemDetailsSchemaId,
			CreateProblemDetails(document)
		);
	}

	public static OpenApiSchemaReference ProblemDetailsReference(OpenApiDocument document) =>
		new(ZodOpenApiOptions.ValidationProblemDetailsSchemaId, document);

	static OpenApiSchema CreateProblemDetails(OpenApiDocument document) =>
		new()
		{
			Type = JsonSchemaType.Object,
			Description =
				"An RFC 9457 validation problem (HttpValidationProblemDetails) with ZodSharp's structured issues.",
			Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
			{
				["type"] = Nullable(JsonSchemaType.String),
				["title"] = Nullable(JsonSchemaType.String),
				["status"] = Nullable(JsonSchemaType.Integer, "int32"),
				["detail"] = Nullable(JsonSchemaType.String),
				["instance"] = Nullable(JsonSchemaType.String),
				["errors"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Object,
					Description = "The messages for each failing path, keyed by the dotted path.",
					AdditionalProperties = new OpenApiSchema
					{
						Type = JsonSchemaType.Array,
						Items = new OpenApiSchema { Type = JsonSchemaType.String },
					},
				},
				["issues"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Array,
					Description = "The structured validation issues.",
					Items = new OpenApiSchemaReference(ZodOpenApiOptions.ValidationIssueSchemaId, document),
				},
				["traceId"] = Nullable(JsonSchemaType.String),
			},
		};

	static OpenApiSchema CreateIssue(OpenApiDocument document) =>
		new()
		{
			Type = JsonSchemaType.Object,
			Description = "A structured ZodSharp validation issue.",
			Required = new HashSet<string>(StringComparer.Ordinal) { "path", "code", "message" },
			Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
			{
				["path"] = new OpenApiSchema
				{
					Type = JsonSchemaType.String,
					Description = "The dotted path of the failing value, for example 'Items[0].Quantity'.",
				},
				["segments"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Array,
					Description = "The path as segments: member names (strings) and collection indexes (integers).",
					Items = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Integer },
				},
				["code"] = new OpenApiSchema
				{
					Type = JsonSchemaType.String,
					Description = "The Zod-compatible issue code, for example 'too_small'.",
				},
				["category"] = Nullable(JsonSchemaType.String),
				["origin"] = Nullable(JsonSchemaType.String),
				["minimum"] = Bound("The lower bound of a size or range issue."),
				["maximum"] = Bound("The upper bound of a size or range issue."),
				["inclusive"] = Nullable(JsonSchemaType.Boolean),
				["message"] = new OpenApiSchema { Type = JsonSchemaType.String },
				["parameters"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Object | JsonSchemaType.Null,
					Description = "The error type's parameters for this issue.",
				},
				["unionErrors"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Array | JsonSchemaType.Null,
					Description = "For a union, the issues each case reported.",
					Items = new OpenApiSchema
					{
						Type = JsonSchemaType.Array,
						Items = new OpenApiSchemaReference(ZodOpenApiOptions.ValidationIssueSchemaId, document),
					},
				},
			},
		};

	static OpenApiSchema Nullable(JsonSchemaType type, string? format = null) =>
		new() { Type = type | JsonSchemaType.Null, Format = format };

	static OpenApiSchema Bound(string description) =>
		new()
		{
			Description = description,
			AnyOf =
			[
				new OpenApiSchema { Type = JsonSchemaType.Number },
				new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
				new OpenApiSchema { Type = JsonSchemaType.Null },
			],
		};
}

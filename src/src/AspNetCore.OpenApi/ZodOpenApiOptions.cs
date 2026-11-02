namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Options for the ZodSharp OpenAPI transformers registered by
/// <c>OpenApiOptions.AddZodSharp(Action&lt;ZodOpenApiOptions&gt;?)</c>.
/// </summary>
public sealed class ZodOpenApiOptions
{
	/// <summary>
	/// The component name of the ZodSharp validation ProblemDetails schema.
	/// </summary>
	public const string ValidationProblemDetailsSchemaId = "ZodValidationProblemDetails";

	/// <summary>
	/// The component name of the ZodSharp validation issue schema.
	/// </summary>
	public const string ValidationIssueSchemaId = "ZodValidationIssue";

	/// <summary>
	/// The media type validation problem responses are described with.
	/// </summary>
	public const string ProblemJsonContentType = "application/problem+json";

	/// <summary>
	/// Gets or sets whether rules that have no JSON Schema keyword, or that a keyword only approximates, are
	/// described with <c>x-zod-*</c> vendor extensions (and unrepresentable rules listed under
	/// <c>x-zodsharp-unsupported</c>). Defaults to <see langword="true"/>.
	/// </summary>
	public bool EmitVendorExtensions { get; set; } = true;

	/// <summary>
	/// Gets or sets whether discriminated unions are described with <c>oneOf</c> and a <c>discriminator</c>
	/// rather than <c>anyOf</c>. A discriminated union matches exactly one case, so <c>oneOf</c> is the more
	/// precise description and client generators produce better code from it. Defaults to
	/// <see langword="true"/>.
	/// </summary>
	public bool DiscriminatedUnionsAsOneOf { get; set; } = true;

	/// <summary>
	/// Gets or sets whether operations whose parameters ZodSharp validates list the validation problem
	/// responses (<c>400</c>, and <c>422</c> when a registered <c>ErrorType</c> uses it) with the
	/// <see cref="ValidationProblemDetailsSchemaId"/> schema. Defaults to <see langword="true"/>.
	/// </summary>
	public bool DescribeValidationProblems { get; set; } = true;
}

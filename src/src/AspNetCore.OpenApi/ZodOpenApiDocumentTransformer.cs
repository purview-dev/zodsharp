using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Adds the shared <c>ZodValidationProblemDetails</c> and <c>ZodValidationIssue</c> component schemas to a
/// document whose operations reference them.
/// </summary>
/// <remarks>
/// A document with no ZodSharp-validated operation is left unchanged, so adding the integration to an API
/// that does not use ZodSharp validation does not add unused components.
/// </remarks>
/// <param name="options">The transformer options.</param>
public sealed class ZodOpenApiDocumentTransformer(ZodOpenApiOptions options) : IOpenApiDocumentTransformer
{
	readonly ZodOpenApiOptions _options = options ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc/>
	public Task TransformAsync(
		OpenApiDocument document,
		OpenApiDocumentTransformerContext context,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);

		if (_options.DescribeValidationProblems && ReferencesValidationProblem(document))
			ZodValidationProblemSchemas.Register(document);

		return Task.CompletedTask;
	}

	static bool ReferencesValidationProblem(OpenApiDocument document)
	{
		if (document.Paths is null)
			return false;

		foreach (var path in document.Paths.Values)
		{
			if (path.Operations is null)
				continue;

			foreach (var operation in path.Operations.Values)
			{
				if (operation.Responses is null)
					continue;

				foreach (var response in operation.Responses.Values)
				{
					if (response.Content is null)
						continue;

					foreach (var mediaType in response.Content.Values)
					{
						if (
							mediaType.Schema is OpenApiSchemaReference { Reference.Id: { } id }
							&& string.Equals(id, ZodOpenApiOptions.ValidationProblemDetailsSchemaId, StringComparison.Ordinal)
						)
						{
							return true;
						}
					}
				}
			}
		}

		return false;
	}
}

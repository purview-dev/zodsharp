using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// Lists the validation problem responses of every operation that binds a ZodSharp-validated parameter:
/// <c>400</c>, plus <c>422</c> when an <see cref="Core.ErrorType"/> registered in the
/// <see cref="ErrorTypeRegistry"/> uses it, each as <c>application/problem+json</c> with the
/// <c>ZodValidationProblemDetails</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// A parameter is ZodSharp-validated when its type (or, for an <c>[AsParameters]</c> member, its container
/// type) has a validator registered in <see cref="Core.IZodSchemaFactory"/> or a generated
/// <c>[ZodSchema]</c> validator. Services and special parameters (<c>HttpContext</c>,
/// <c>CancellationToken</c>, …) are skipped.
/// </para>
/// <para>
/// A response the endpoint already declares — typically <c>ProducesValidationProblem()</c> — is merged into
/// rather than duplicated: its description is kept and its <c>application/problem+json</c> schema is replaced
/// with the ZodSharp contract.
/// </para>
/// </remarks>
/// <param name="options">The transformer options.</param>
public sealed class ZodOpenApiOperationTransformer(ZodOpenApiOptions options) : IOpenApiOperationTransformer
{
	readonly ZodOpenApiOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	readonly ZodJsonSchemaSource _source = new();

	/// <inheritdoc/>
	public Task TransformAsync(
		OpenApiOperation operation,
		OpenApiOperationTransformerContext context,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(context);

		if (!_options.DescribeValidationProblems || !IsValidated(context.Description, context.ApplicationServices))
			return Task.CompletedTask;

		var document = context.Document;
		operation.Responses ??= [];

		foreach (var status in GetStatusCodes(context.ApplicationServices))
			DescribeResponse(operation.Responses, status, document);

		if (document is not null)
			ZodValidationProblemSchemas.Register(document);

		return Task.CompletedTask;
	}

	static void DescribeResponse(OpenApiResponses responses, int status, OpenApiDocument? document)
	{
		var key = status.ToString(CultureInfo.InvariantCulture);
		OpenApiResponse response;

		if (responses.TryGetValue(key, out var existing))
		{
			// A response component reference is the developer's own shared definition; leave it alone.
			if (existing is not OpenApiResponse declared)
				return;

			response = declared;
		}
		else
		{
			response = new OpenApiResponse { Description = ReasonPhrases.GetReasonPhrase(status) };
			responses[key] = response;
		}

		response.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase);
		OpenApiSchemaReference schema = new(ZodOpenApiOptions.ValidationProblemDetailsSchemaId, document);

		if (
			response.Content.TryGetValue(ZodOpenApiOptions.ProblemJsonContentType, out var mediaType)
			&& mediaType is OpenApiMediaType declaredMediaType
		)
		{
			declaredMediaType.Schema = schema;
		}
		else
		{
			response.Content[ZodOpenApiOptions.ProblemJsonContentType] = new OpenApiMediaType { Schema = schema };
		}
	}

	static IEnumerable<int> GetStatusCodes(IServiceProvider services)
	{
		yield return StatusCodes.Status400BadRequest;

		// Which error types an endpoint's rules can raise is not known statically, so 422 is listed only when
		// the registry the ProblemDetails mapping uses contains an error type that maps to it.
		var registry = services.GetService<IOptions<ZodProblemDetailsOptions>>()?.Value.Registry ?? ErrorTypeRegistry.Default;
		if (registry.All.Any(static errorType => errorType.HttpStatus == StatusCodes.Status422UnprocessableEntity))
			yield return StatusCodes.Status422UnprocessableEntity;
	}

	bool IsValidated(ApiDescription description, IServiceProvider services)
	{
		foreach (var parameter in description.ParameterDescriptions)
		{
			if (parameter.Source == BindingSource.Services || parameter.Source == BindingSource.Special)
				continue;

			if (
				parameter.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } container }
				&& _source.IsKnown(container, services)
			)
			{
				return true;
			}

			var type = parameter.Type;
			if (type is null)
				continue;

			type = Nullable.GetUnderlyingType(type) ?? type;
			if (_source.IsKnown(type, services))
				return true;
		}

		return false;
	}
}

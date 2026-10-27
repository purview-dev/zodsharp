using ZodSharp.AspNetCore.OpenApi;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Registers the ZodSharp OpenAPI transformers.
/// </summary>
public static class ZodSharpOpenApiOptionsExtensions
{
	extension(OpenApiOptions options)
	{
		/// <summary>
		/// Describes what ZodSharp enforces in the OpenAPI document: the constraints and formats of
		/// ZodSharp-validated types (<see cref="ZodOpenApiSchemaTransformer"/>), the validation problem responses
		/// of the operations that bind them (<see cref="ZodOpenApiOperationTransformer"/>), and the shared error
		/// contract schemas (<see cref="ZodOpenApiDocumentTransformer"/>).
		/// </summary>
		/// <param name="configure">Configures the transformers.</param>
		/// <returns>The options, for chaining.</returns>
		/// <example>
		/// <code>
		/// builder.Services.AddOpenApi(options => options.AddZodSharp());
		/// </code>
		/// </example>
		public OpenApiOptions AddZodSharp(Action<ZodOpenApiOptions>? configure = null)
		{
			ArgumentNullException.ThrowIfNull(options);

			ZodOpenApiOptions zodOptions = new();
			configure?.Invoke(zodOptions);

			options.AddSchemaTransformer(new ZodOpenApiSchemaTransformer(zodOptions));
			options.AddOperationTransformer(new ZodOpenApiOperationTransformer(zodOptions));
			options.AddDocumentTransformer(new ZodOpenApiDocumentTransformer(zodOptions));

			return options;
		}
	}
}

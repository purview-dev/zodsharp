using Microsoft.AspNetCore.Http;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// ZodSharp validation for any endpoint convention builder: a single endpoint, a route group, or a controller
/// action convention.
/// </summary>
public static class ZodSharpEndpointConventionBuilderExtensions
{
	/// <summary>
	/// Validates every bound parameter that has a validator registered with the <see cref="IZodSchemaFactory"/>,
	/// returning the ZodSharp ProblemDetails contract when validation fails. Applies to every endpoint of a route
	/// group.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Parameters are selected when the endpoint is built. Services (<c>[FromServices]</c>, keyed services, and
	/// anything the container can resolve), <c>HttpContext</c>, <c>HttpRequest</c>, <c>HttpResponse</c>,
	/// <c>ClaimsPrincipal</c>, <c>CancellationToken</c>, form files, streams and parameters marked
	/// <c>[SkipValidation]</c> are skipped; <c>[FromBody]</c>, <c>[FromRoute]</c>, <c>[FromQuery]</c>,
	/// <c>[FromHeader]</c> and <c>[FromForm]</c> force a parameter to be considered.
	/// </para>
	/// <para>
	/// Error keys follow Microsoft.Extensions.Validation: a complex parameter's errors are not prefixed
	/// (<c>Name</c>, <c>Address.Street</c>), a sequence parameter's are (<c>items[0].Name</c>), and an error about
	/// the value itself is keyed by the parameter name.
	/// </para>
	/// <para>
	/// The endpoint also advertises a <c>400</c> <c>application/problem+json</c> response, so OpenAPI documents
	/// show the error contract without extra annotations.
	/// </para>
	/// </remarks>
	/// <typeparam name="TBuilder">The builder type.</typeparam>
	/// <param name="builder">The endpoint convention builder.</param>
	/// <param name="configure">Optional configuration.</param>
	/// <returns>The builder for chaining.</returns>
	public static TBuilder WithZodSharpValidation<TBuilder>(
		this TBuilder builder,
		Action<ZodEndpointValidationOptions>? configure = null
	)
		where TBuilder : IEndpointConventionBuilder
	{
		ArgumentNullException.ThrowIfNull(builder);

		ZodEndpointValidationOptions options = new();
		configure?.Invoke(options);

		builder.AddEndpointFilterFactory(
			ZodEndpointValidationFilterFactory.Create(options, requiredType: null, requiredTypeResolver: null)
		);
		AddValidationMetadata(builder, options);
		return builder;
	}

	internal static void AddValidationMetadata<TBuilder>(TBuilder builder, ZodEndpointValidationOptions options)
		where TBuilder : IEndpointConventionBuilder =>
		builder.WithMetadata(
			new ProducesResponseTypeMetadata(
				options.ProducesStatusCode,
				typeof(HttpValidationProblemDetails),
				["application/problem+json"]
			)
		);
}

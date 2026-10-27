using Microsoft.AspNetCore.Http;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Minimal API integration for ZodSharp validation.
/// </summary>
public static class ZodSharpRouteHandlerBuilderExtensions
{
	/// <summary>
	/// Validates every bound argument assignable to <typeparamref name="T"/> against the registered
	/// <see cref="IZodSchemaFactory"/>, returning the ZodSharp ProblemDetails contract when validation fails.
	/// </summary>
	/// <remarks>
	/// The validator is resolved once, when the endpoint is built; building the endpoint fails with a message
	/// naming the handler and parameter when no <see cref="IZodSchemaFactory"/> or no validator for
	/// <typeparamref name="T"/> is registered. Use <c>WithZodSharpValidation()</c> to validate every parameter that
	/// has a validator, on an endpoint or a whole route group.
	/// </remarks>
	/// <typeparam name="T">The request type to validate.</typeparam>
	/// <param name="builder">The route handler builder.</param>
	/// <returns>The builder for chaining.</returns>
	public static RouteHandlerBuilder WithZodSharpValidation<T>(this RouteHandlerBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		ZodEndpointValidationOptions options = new();
		builder.AddEndpointFilterFactory(
			ZodEndpointValidationFilterFactory.Create(options, typeof(T), ResolveTyped<T>)
		);
		ZodSharpEndpointConventionBuilderExtensions.AddValidationMetadata(builder, options);
		return builder;
	}

	static ZodUntypedValidator<T>? ResolveTyped<T>(IZodSchemaFactory factory) =>
		factory.Resolve<T>() is { } validator ? new ZodUntypedValidator<T>(validator) : null;
}

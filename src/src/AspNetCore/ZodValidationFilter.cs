using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// An endpoint filter that validates every bound argument of type <typeparamref name="T"/> against the
/// registered <see cref="IZodSchemaFactory"/>. When validation fails the request is short-circuited with the
/// ZodSharp ProblemDetails contract, written by <see cref="ZodProblemDetailsWriter"/>.
/// </summary>
/// <typeparam name="T">The request type to validate.</typeparam>
/// <remarks>
/// <c>WithZodSharpValidation&lt;T&gt;()</c> no longer uses this class: it installs a filter factory that resolves
/// the validator once, when the endpoint is built, and fails then if none is registered. This class remains for
/// code that adds it directly with <c>AddEndpointFilter&lt;ZodValidationFilter&lt;T&gt;&gt;()</c>.
/// </remarks>
public sealed class ZodValidationFilter<T> : IEndpointFilter
{
	/// <inheritdoc/>
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		var httpContext = context.HttpContext;

#if NET11_0_OR_GREATER
		// A binding failure already set the 400; let ASP.NET Core's response stand.
		if (httpContext.Response.StatusCode == StatusCodes.Status400BadRequest)
			return await next(context).ConfigureAwait(false);
#endif

		var factory =
			httpContext.RequestServices.GetService<IZodSchemaFactory>()
			?? throw new InvalidOperationException(
				"No IZodSchemaFactory is registered. Call AddZodSharp (or AddZodSharpFactory) before applying WithZodSharpValidation."
			);

		IZodSchemaValidator<T>? validator = null;
		List<ValidationError>? errors = null;

		foreach (var argument in context.Arguments)
		{
			if (argument is not T value)
				continue;

			validator ??=
				factory.Resolve<T>()
				?? throw new InvalidOperationException(
					$"No Zod schema validator is registered for type '{typeof(T).FullName}'."
				);

			var result = await validator
				.ValidateAsync(value, httpContext.RequestServices, httpContext.RequestAborted)
				.ConfigureAwait(false);
			if (result.IsSuccess)
				continue;

			errors ??= [];
			errors.AddRange(result.Errors);
		}

		if (errors is null)
			return await next(context).ConfigureAwait(false);

		var writer = ZodProblemDetailsWriter.GetOrCreate(httpContext.RequestServices);
		return new ZodProblemDetailsResult(writer, writer.Create(errors, httpContext));
	}
}

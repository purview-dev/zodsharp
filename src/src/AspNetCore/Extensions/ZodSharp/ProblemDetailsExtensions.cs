using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// Converts ZodSharp validation results into ASP.NET Core ProblemDetails payloads. Every overload delegates to
/// <see cref="ZodProblemDetailsWriter"/>, so the body matches the endpoint filter and the exception handler.
/// </summary>
public static class ProblemDetailsExtensions
{
	extension<T>(ValidationResult<T> result)
	{
		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />, without resolving
		/// <see cref="ErrorType"/>s.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			EnsureFailed(result);
			return CreateWriter(registry: null, lookup: null, formatMessages: false, statusCode).Create(result.Errors);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			ArgumentNullException.ThrowIfNull(registry);
			EnsureFailed(result);
			return CreateWriter(registry, lookup: null, formatMessages: true, statusCode).Create(result.Errors);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup to derive the status code, title, and messages.
		/// </summary>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			ArgumentNullException.ThrowIfNull(lookup);
			EnsureFailed(result);
			return CreateWriter(registry: null, lookup, formatMessages: true, statusCode).Create(result.Errors);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" /> with the supplied
		/// options.
		/// </summary>
		/// <param name="options">The ProblemDetails options.</param>
		/// <param name="httpContext">The request, for the trace id and JSON naming; optional.</param>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(
			ZodProblemDetailsOptions options,
			HttpContext? httpContext = null
		)
		{
			ArgumentNullException.ThrowIfNull(options);
			EnsureFailed(result);
			return new ZodProblemDetailsWriter(options).Create(result.Errors, httpContext);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="HttpValidationProblemDetails" /> with the writer
		/// (and therefore the options) registered in the request services.
		/// </summary>
		/// <param name="httpContext">The request.</param>
		public HttpValidationProblemDetails ToHttpValidationProblemDetails(HttpContext httpContext)
		{
			ArgumentNullException.ThrowIfNull(httpContext);
			EnsureFailed(result);
			return ZodProblemDetailsWriter.GetOrCreate(httpContext.RequestServices).Create(result.Errors, httpContext);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(int statusCode = StatusCodes.Status400BadRequest)
		{
			var details = result.ToHttpValidationProblemDetails(statusCode);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s from the supplied registry.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			ErrorTypeRegistry registry,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = result.ToHttpValidationProblemDetails(registry, statusCode);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into <see cref="ValidationProblemDetails" />, resolving
		/// <see cref="ErrorType"/>s through the supplied lookup.
		/// </summary>
		public ValidationProblemDetails ToValidationProblemDetails(
			Func<string, ErrorType?> lookup,
			int statusCode = StatusCodes.Status400BadRequest
		)
		{
			var details = result.ToHttpValidationProblemDetails(lookup, statusCode);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into MVC <see cref="ValidationProblemDetails" /> with the supplied
		/// options. Every extension is copied.
		/// </summary>
		/// <param name="options">The ProblemDetails options.</param>
		/// <param name="httpContext">The request, for the trace id and JSON naming; optional.</param>
		public ValidationProblemDetails ToValidationProblemDetails(
			ZodProblemDetailsOptions options,
			HttpContext? httpContext = null
		)
		{
			var details = result.ToHttpValidationProblemDetails(options, httpContext);
			return ToValidationProblemDetails(details);
		}

		/// <summary>
		/// Converts a failed validation result into a minimal-API <see cref="IResult"/> that renders the ZodSharp
		/// ProblemDetails contract, without resolving <see cref="ErrorType"/>s. Prefer
		/// the <c>ToValidationProblem(HttpContext)</c> overload, which applies the registered options.
		/// </summary>
		/// <returns>An <see cref="IResult"/> suitable for returning from a minimal-API handler.</returns>
		public IResult ToValidationProblem()
		{
			var details = result.ToHttpValidationProblemDetails();
			return TypedResults.Problem(details);
		}

		/// <summary>
		/// Converts a failed validation result into a minimal-API <see cref="IResult"/> written by the
		/// <see cref="ZodProblemDetailsWriter"/> registered in the request services, so it applies the configured
		/// <see cref="ZodProblemDetailsOptions"/> and goes through the ProblemDetails service exactly like the
		/// endpoint filter and the exception handler.
		/// </summary>
		/// <param name="httpContext">The request.</param>
		/// <returns>An <see cref="IResult"/> suitable for returning from a minimal-API handler.</returns>
		public IResult ToValidationProblem(HttpContext httpContext) => ZodTypedResults.Problem(result, httpContext);
	}

	static void EnsureFailed<T>(ValidationResult<T> result)
	{
		if (result.IsSuccess)
			throw new InvalidOperationException("Cannot create ProblemDetails from a successful validation result.");
	}

	internal static ZodProblemDetailsWriter CreateWriter(
		ErrorTypeRegistry? registry,
		Func<string, ErrorType?>? lookup,
		bool formatMessages,
		int statusCode
	) =>
		new(
			new ZodProblemDetailsOptions
			{
				Registry = registry ?? EmptyRegistry,
				ErrorTypeResolver = lookup,
				FormatMessages = formatMessages,
				DefaultStatusCode = statusCode,
			}
		);

	static readonly ErrorTypeRegistry EmptyRegistry = new();

	internal static ValidationProblemDetails ToValidationProblemDetails(HttpValidationProblemDetails details)
	{
		ValidationProblemDetails copy = new(details.Errors)
		{
			Title = details.Title,
			Status = details.Status,
			Type = details.Type,
			Detail = details.Detail,
			Instance = details.Instance,
		};

		foreach (var pair in details.Extensions)
			copy.Extensions[pair.Key] = pair.Value;

		return copy;
	}
}

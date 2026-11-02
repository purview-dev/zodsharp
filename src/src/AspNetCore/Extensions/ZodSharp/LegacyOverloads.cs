namespace ZodSharp;

/// <summary>
/// The deprecation messages of the 2.x ProblemDetails conversion overloads. They take no
/// <see cref="ZodProblemDetailsOptions"/> and no request, so they cannot see the options an application
/// configures (<c>AddZodSharpProblemDetails</c>) and their output can differ from the endpoint filter, the exception
/// handler and <see cref="ZodTypedResults"/>. Their behaviour is unchanged; they are kept for source and
/// binary compatibility.
/// </summary>
static class LegacyOverloads
{
	const string Ignores =
		"Ignores the application's ZodProblemDetailsOptions, so the body can differ from the endpoint filter and the "
		+ "exception handler. Use the HttpContext overload in a request; outside one, pass ";

	public const string StatusCodeMessage =
		Ignores
		+ "new ZodProblemDetailsOptions { Registry = new(), FormatMessages = false, DefaultStatusCode = statusCode } "
		+ "for the same output.";

	public const string RegistryMessage =
		Ignores
		+ "new ZodProblemDetailsOptions { Registry = registry, DefaultStatusCode = statusCode } for the same output.";

	public const string LookupMessage =
		Ignores
		+ "new ZodProblemDetailsOptions { ErrorTypeResolver = lookup, DefaultStatusCode = statusCode } for the same output.";

	public const string ResultMessage =
		"Ignores the application's ZodProblemDetailsOptions and the status they compute. Use "
		+ "ToValidationProblem(HttpContext), which applies the registered options.";
}

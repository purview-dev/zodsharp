using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Examples.Blazor.Client;

/// <summary>
/// What happened to a registration.
/// </summary>
enum RegistrationOutcome
{
	/// <summary>The server registered the customer, and its response validated.</summary>
	Registered,

	/// <summary>The request was invalid, so nothing was sent.</summary>
	RejectedBeforeSending,

	/// <summary>The server answered with a validation problem.</summary>
	RejectedByServer,

	/// <summary>The server answered successfully, but its body did not validate.</summary>
	InvalidResponse,
}

/// <summary>
/// The result of <see cref="CustomerAPIClient.RegisterAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Customer">The registered customer, when <see cref="RegistrationOutcome.Registered"/>.</param>
/// <param name="Errors">The errors, placed onto the form with <c>EditContext.AddZodErrors</c>.</param>
/// <param name="Status">The HTTP status of a server rejection.</param>
sealed record CustomerRegistration(
	RegistrationOutcome Outcome,
	CustomerRegistered? Customer,
	ImmutableArray<ValidationError> Errors,
	int? Status = null
);

/// <summary>
/// The typed client for <c>POST /api/customers</c>, built on Purview.ZodSharp.Http. One class serves every render mode:
/// in WebAssembly it posts from the browser; for the Interactive Server page <c>IHttpClientFactory</c> creates it on the
/// server, where it calls the API service-to-service. The <paramref name="services"/> it validates with are the
/// caller's - the browser's, or the circuit's - so the service-aware email-uniqueness rule resolves the
/// <see cref="ICustomerEmailRegistry"/> that fits where it runs.
/// </summary>
/// <param name="http">The client for the sample's API.</param>
/// <param name="services">The services service-aware rules resolve from.</param>
sealed class CustomerAPIClient(HttpClient http, IServiceProvider services)
{
	/// <summary>
	/// The registration endpoint, relative to the API's base address.
	/// </summary>
	public static readonly Uri RegisterUri = new("api/customers", UriKind.Relative);

	static readonly RegisterCustomerRequestSchemaValidator RequestValidator = new();
	static readonly CustomerRegisteredSchemaValidator ResponseValidator = new();

	/// <summary>
	/// Registers a customer.
	/// </summary>
	/// <param name="request">The registration.</param>
	/// <param name="validateBeforeSending">
	/// <see langword="true"/> (the default) to validate before posting, so an invalid registration is never sent;
	/// <see langword="false"/> to post it as it is and let the server reject it.
	/// </param>
	/// <param name="cancellationToken">Cancels the validation and the request.</param>
	/// <returns>The outcome, with the errors to show.</returns>
	public async Task<CustomerRegistration> RegisterAsync(
		RegisterCustomerRequest request,
		bool validateBeforeSending = true,
		CancellationToken cancellationToken = default
	)
	{
		HttpResponseMessage response;
		if (validateBeforeSending)
		{
			// Validated with the caller's services, so the async uniqueness rule runs too: a malformed or taken address
			// never leaves the client. This holds for any caller, not only a form that has already validated.
			var sent = await http.PostAsJsonValidatedAsync(
				RegisterUri,
				request,
				RequestValidator,
				ClientJsonContext.Default.RegisterCustomerRequest,
				services,
				cancellationToken
			);
			if (!sent.IsSuccess)
				return new(RegistrationOutcome.RejectedBeforeSending, null, sent.Errors);

			response = sent.Value;
		}
		else
		{
			response = await http.PostAsJsonAsync(
				RegisterUri,
				request,
				ClientJsonContext.Default.RegisterCustomerRequest,
				cancellationToken
			);
		}

		using (response)
		{
			// The server validates every request; its ProblemDetails use the same codes and paths as the client.
			if (await response.TryReadZodValidationProblemAsync(cancellationToken) is { } problem)
				return new(RegistrationOutcome.RejectedByServer, null, problem.ToValidationErrors(), problem.Status);

			response.EnsureSuccessStatusCode();

			// Validated as it is read: a malformed response is an error, not a half-filled value.
			var registered = await response.Content.ReadFromJsonAndValidateAsync(
				ResponseValidator,
				ClientJsonContext.Default.CustomerRegistered,
				cancellationToken
			);

			return registered.IsSuccess
				? new(RegistrationOutcome.Registered, registered.Value, [])
				: new(RegistrationOutcome.InvalidResponse, null, registered.Errors);
		}
	}
}

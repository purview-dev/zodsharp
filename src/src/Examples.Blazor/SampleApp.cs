using Microsoft.AspNetCore.Localization;
using ZodSharp.Examples.Blazor.Client;
using ZodSharp.Examples.Blazor.Components;

namespace ZodSharp.Examples.Blazor;

/// <summary>
/// The Blazor sample's service registration, request pipeline and endpoints.
/// </summary>
static class SampleApp
{
	/// <summary>
	/// The path of the culture switch endpoint.
	/// </summary>
	public const string CulturePath = "/culture/set";

	/// <summary>
	/// Registers Razor components in every render mode, ZodSharp for Blazor forms and static-SSR client validation,
	/// the ProblemDetails contract for the API and the sample's localization.
	/// </summary>
	/// <param name="services">The services.</param>
	public static void ConfigureServices(IServiceCollection services)
	{
		// A Blazor Web App with Auto render mode available: interactive server, interactive WebAssembly and static SSR.
		services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents();

		// Client-side rules for ZodSharp rule attributes ([StartsWith], [UUID], ...) on statically rendered forms,
		// beside ASP.NET Core's DataAnnotations rules. Either order with AddRazorComponents works. It is
		// [RequiresUnreferencedCode], like AddRazorComponents itself; this server is not trimmed.
		services.AddZodSharpClientValidation();

		// The same two calls as the client Program.cs: the generated, trim-safe registration of the Examples.Shared
		// [ZodSchema] models, then ZodSharp for Blazor forms (which also makes ZodSharp the first
		// Microsoft.Extensions.Validation resolver, so the minimal API below is validated by the built-in pipeline).
		services.AddZodSharpZodSharpExamplesSharedSchemas();
		services.AddZodSharpBlazor();

		// One ProblemDetails contract (errors + issues + traceId) for the API, which the WebAssembly form maps back
		// onto its fields with AddZodProblemDetails.
		services.AddZodSharpProblemDetails(static options => options.MergeParametersIntoExtensions = false);
		services.ConfigureHttpJsonOptions(static options =>
			options.SerializerOptions.TypeInfoResolverChain.Insert(0, ClientJsonContext.Default)
		);

		// The store the service-aware async email-uniqueness rule on RegisterCustomerRequest resolves on the server.
		services.AddSingleton<ICustomerEmailRegistry, InMemoryCustomerEmailRegistry>();

		SampleLocalization.AddSampleLocalization(services);
	}

	/// <summary>
	/// Configures the request pipeline, the API and the Razor components.
	/// </summary>
	/// <param name="app">The application.</param>
	public static void Configure(WebApplication app)
	{
		if (!app.Environment.IsDevelopment())
			app.UseExceptionHandler();

		// The culture comes from the cookie the culture switch writes, then Accept-Language.
		app.UseRequestLocalization(static options =>
			options
				.SetDefaultCulture(SampleLocalization.SupportedCultures[0])
				.AddSupportedCultures(SampleLocalization.SupportedCultures)
				.AddSupportedUICultures(SampleLocalization.SupportedCultures)
		);

		app.UseAntiforgery();
		app.MapStaticAssets();

		MapEndpoints(app);

		app.MapRazorComponents<App>()
			.AddInteractiveServerRenderMode()
			.AddInteractiveWebAssemblyRenderMode()
			.AddAdditionalAssemblies(typeof(SampleMessages).Assembly);
	}

	static void MapEndpoints(WebApplication app)
	{
		var customers = app.MapGroup("/api/customers").WithTags("Customers");

		// Validated by the .NET 11 pipeline with the generated ZodSharp validator, including the async uniqueness
		// rule - nothing on the endpoint asks for it.
		customers.MapPost("/", static (RegisterCustomerRequest customer, ICustomerEmailRegistry registry) =>
		{
			registry.TryRegister(customer.Email!);
			return TypedResults.Created($"/api/customers/{customer.Email}", new CustomerRegistered(customer.Email!));
		});

		// What the browser's HttpCustomerEmailRegistry asks while the user types.
		customers
			.MapGet(
				"/registered",
				static async (string email, ICustomerEmailRegistry registry, CancellationToken cancellationToken) =>
					TypedResults.Ok(new EmailStatus(await registry.IsRegisteredAsync(email, cancellationToken)))
			)
			.DisableValidation();

		// The culture switch: remembers the choice in the localization cookie and returns to the page.
		app.MapGet(CulturePath, static (string culture, string? redirectUri, HttpContext context) =>
		{
			if (Array.IndexOf(SampleLocalization.SupportedCultures, culture) >= 0)
			{
				context.Response.Cookies.Append(
					CookieRequestCultureProvider.DefaultCookieName,
					CookieRequestCultureProvider.MakeCookieValue(new(culture))
				);
			}

			return TypedResults.LocalRedirect(IsLocal(redirectUri) ? redirectUri! : "/");
		});
	}

	static bool IsLocal(string? uri) =>
		uri is ['/', ..]
		&& !uri.StartsWith("//", StringComparison.Ordinal)
		&& !uri.StartsWith("/\\", StringComparison.Ordinal);
}

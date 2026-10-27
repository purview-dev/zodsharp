using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Localization;
using ZodSharp.AspNetCore;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;
using ZodSharp.Options;

namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// The sample's service registration and endpoints, shared by <c>Program.cs</c> and the in-memory smoke tests.
/// </summary>
static class SampleApp
{
	/// <summary>
	/// Registers ZodSharp, the .NET 11 validation pipeline, ProblemDetails, OpenAPI, localization and the validated
	/// options.
	/// </summary>
	/// <param name="builder">The application builder.</param>
	public static void ConfigureServices(WebApplicationBuilder builder)
	{
		var services = builder.Services;

		// Request and response types are serialized through source-generated metadata, as Native AOT requires.
		services.ConfigureHttpJsonOptions(static options =>
			options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default)
		);

		// The IZodSchemaFactory, populated without assembly scanning: the generated AddZodSharp{Assembly}Schemas()
		// extensions name every validator directly, which is what keeps registration trim- and AOT-safe.
		services.AddZodSharp();
		services.AddZodSharpZodSharpExamplesSharedSchemas();
		services.AddZodSharpZodSharpExamplesMinimalApiSchemas();

		// Mode A: ZodSharp becomes the first Microsoft.Extensions.Validation resolver, so the built-in .NET 11
		// pipeline validates every endpoint parameter of a ZodSharp-known type - no per-endpoint call.
		services.AddZodSharpValidation();

		// One ProblemDetails contract (errors + issues + traceId) on every path: the pipeline, the endpoint
		// filter, ZodException and manual results. Each issue carries its own parameters, so they are not also
		// merged into the top-level extensions (that merge is on by default only for compatibility).
		services.AddZodSharpProblemDetails(static options => options.MergeParametersIntoExtensions = false);

		// The OpenAPI document describes the constraints ZodSharp enforces and the ZodValidationProblemDetails
		// error contract of every validated operation.
		services.AddOpenApi(static options => options.AddZodSharp());

		// Validation messages in the request culture (Accept-Language: fr). Codes and paths never change.
		services.AddLocalization();
		services.AddSingleton<IZodMessageFormatter>(static sp => new StringLocalizerZodMessageFormatter(
			sp.GetRequiredService<IStringLocalizerFactory>(),
			sharedResourceType: typeof(ValidationMessages)
		));

		// The store the service-aware async email-uniqueness rule on RegisterCustomerRequest resolves.
		services.AddSingleton<ICustomerEmailRegistry, InMemoryCustomerEmailRegistry>();

		// Options validated with their generated schema - including the async rule - when the host starts.
		// BindAndValidateWithZod(SampleOptions.SectionName) does the same in one call, but it binds through the
		// reflection-based configuration binder ([RequiresUnreferencedCode]). Under Native AOT, bind with
		// BindConfiguration, which the configuration binder source generator intercepts, and then add the
		// ZodSharp validator naming the section, so failures read "Sample:ApiName: ...".
		services
			.AddOptions<SampleOptions>()
			.BindConfiguration(SampleOptions.SectionName)
			.AddZodSchemaValidator(
				MissingValidatorBehavior.Throw,
				validateOnStart: true,
				configurationSection: SampleOptions.SectionName
			);
	}

	/// <summary>
	/// Configures the request pipeline and maps the endpoints.
	/// </summary>
	/// <param name="app">The application.</param>
	public static void Configure(WebApplication app)
	{
		app.UseExceptionHandler();
		app.UseRequestLocalization(static options =>
			options.SetDefaultCulture("en").AddSupportedCultures("en", "fr").AddSupportedUICultures("en", "fr")
		);

		app.MapOpenApi();

		MapPipelineValidatedEndpoints(app);
		MapFilterValidatedEndpoints(app);
		MapManuallyValidatedEndpoints(app);
	}

	/// <summary>
	/// Mode A: route groups validated by the .NET 11 pipeline. Nothing on the group or the endpoints asks for
	/// validation; registering ZodSharp with the pipeline is enough.
	/// </summary>
	static void MapPipelineValidatedEndpoints(WebApplication app)
	{
		var orders = app.MapGroup("/orders").WithTags("Orders");
		orders.MapPost(
			"/",
			static (CreateOrderRequest order) =>
			{
				var accepted = OrderAccepted.For(order);
				return TypedResults.Created($"/orders/{accepted.Id}", accepted);
			}
		);

		var customers = app.MapGroup("/customers").WithTags("Customers");
		customers.MapPost(
			"/",
			static (RegisterCustomerRequest customer, ICustomerEmailRegistry registry) =>
			{
				// The async uniqueness rule has already run against the same registry.
				registry.TryRegister(customer.Email!);
				return TypedResults.Created($"/customers/{customer.Email}", new CustomerRegistered(customer.Email!));
			}
		);
	}

	/// <summary>
	/// Mode B: the ZodSharp endpoint filter, which works on every target framework. <c>DisableValidation()</c>
	/// takes the endpoint out of the built-in pipeline, so the filter is the one validating it.
	/// </summary>
	static void MapFilterValidatedEndpoints(WebApplication app) =>
		app.MapPost(
				"/filtered/orders",
				static (CreateOrderRequest order) =>
				{
					var accepted = OrderAccepted.For(order);
					return TypedResults.Created($"/orders/{accepted.Id}", accepted);
				}
			)
			.WithTags("Orders")
			.DisableValidation()
			.WithZodSharpValidation();

	/// <summary>
	/// Manual validation with the generated schema, returning the same contract through
	/// <see cref="ZodTypedResults.ValidationProblem{T}(in ValidationResult{T}, HttpContext)"/>.
	/// </summary>
	static void MapManuallyValidatedEndpoints(WebApplication app) =>
		app.MapPost(
				"/manual/orders",
				static Results<Created<OrderAccepted>, ValidationProblem> (
					CreateOrderRequest order,
					HttpContext context
				) =>
				{
					var result = CreateOrderRequestSchema.Validate(order);
					if (!result.IsSuccess)
						return ZodTypedResults.ValidationProblem(result, context);

					var accepted = OrderAccepted.For(order);
					return TypedResults.Created($"/orders/{accepted.Id}", accepted);
				}
			)
			.WithTags("Orders")
			.DisableValidation();
}

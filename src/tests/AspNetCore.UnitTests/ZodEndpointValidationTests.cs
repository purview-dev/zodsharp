using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;
#if !NET11_0_OR_GREATER
using System.Net.Http.Json;
#endif

namespace ZodSharp.AspNetCore;

public class ZodEndpointValidationTests
{
	static readonly OrderRequest ValidOrder = new() { Email = "a@b.c", Items = [new() { Quantity = 1 }] };
	static readonly OrderRequest InvalidOrder = new() { Email = "nope", Items = [new() { Quantity = 0 }] };

	static void AddServices(
		IServiceCollection services,
		IZodSchemaValidator<OrderRequest>? orderValidator = null,
		Action<ZodProblemDetailsOptions>? configure = null
	)
	{
		services.AddZodSharp(options =>
			options.ConfigureFactory = factory =>
			{
				factory.Register(orderValidator ?? new OrderRequestValidator());
				factory.Register(new CustomerQueryValidator());
			}
		);
		services.AddZodSharpProblemDetails(options =>
		{
			options.Registry = new ErrorTypeRegistry();
			configure?.Invoke(options);
		});
	}

	[Test]
	public async Task WithZodSharpValidation_GivenInvalidBody_ReturnsProblemDetailsContract(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync("/orders", InvalidOrder, cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
		await Assert
			.That(root.GetProperty("errors").GetProperty("Email")[0].GetString())
			.IsEqualTo("Email must be a valid email address");
		await Assert
			.That(root.GetProperty("errors").GetProperty("Items[0].Quantity")[0].GetString())
			.IsEqualTo("Quantity must be at least 1");
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(2);
		await Assert.That(root.TryGetProperty("traceId", out _)).IsTrue();
	}

	[Test]
	public async Task WithZodSharpValidation_GivenValidBody_InvokesHandler(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok("handled")).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync("/orders", ValidOrder, cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(await response.Content.ReadAsStringAsync(cancellationToken)).IsEqualTo("\"handled\"");
	}

	[Test]
	public async Task WithZodSharpValidation_GivenMappedErrorType_UsesRegistryStatusAndAddsTraceId(
		CancellationToken cancellationToken
	)
	{
		// Defect D6: the filter ignored ErrorTypeRegistry and ZodProblemDetailsOptions, always answered 400 and
		// never added a traceId.

		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
				AddServices(services, configure: options =>
					options.MapErrorType(
						new ErrorType("invalid_email", HttpStatus: StatusCodes.Status422UnprocessableEntity)
					)
				),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync("/orders", InvalidOrder, cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
		await Assert.That(document.RootElement.GetProperty("status").GetInt32()).IsEqualTo(422);
		await Assert.That(document.RootElement.GetProperty("traceId").GetString()).IsNotNull().And.IsNotEmpty();
	}

	[Test]
	public async Task WithZodSharpValidation_GivenSeveralValidatedArguments_ValidatesEveryArgument(
		CancellationToken cancellationToken
	)
	{
		// Defect D12: only the first matching argument was validated.

		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app =>
				app.MapPost("/orders", (OrderRequest order, [AsParameters] CustomerQuery customer) => TypedResults.Ok())
					.WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync("/orders", InvalidOrder, cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var errors = document.RootElement.GetProperty("errors");

		// Assert
		await Assert.That(errors.TryGetProperty("Email", out _)).IsTrue();
		await Assert.That(errors.GetProperty("Name")[0].GetString()).IsEqualTo("Name is required");
	}

	[Test]
	public async Task WithZodSharpValidationOfT_GivenSeveralArgumentsOfT_ValidatesEveryArgument(
		CancellationToken cancellationToken
	)
	{
		// Defect D12: only the first argument of type T was validated.

		// Arrange
		DelegateValidator<string> validator = new(static value =>
			value.Length >= 3 ? [] : [ValidationError.Create("too_small", "Too short.", ZodPath.Root)]
		);
		await using var app = await TestApp.StartAsync(
			services =>
			{
				services.AddZodSharpFactory(factory => factory.Register(validator));
				services.AddZodSharpProblemDetails();
			},
			app =>
				app.MapGet("/search", (string first, string second) => TypedResults.Ok())
					.WithZodSharpValidation<string>(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(
			new Uri("/search?first=ab&second=cd", UriKind.Relative),
			cancellationToken
		);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var errors = document.RootElement.GetProperty("errors");

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(errors.TryGetProperty("first", out _)).IsTrue();
		await Assert.That(errors.TryGetProperty("second", out _)).IsTrue();
		await Assert.That(validator.Calls).IsEqualTo(2);
	}

	[Test]
	public async Task WithZodSharpValidationOfT_GivenNoValidatorRegistered_FailsWhenTheEndpointIsBuilt(
		CancellationToken cancellationToken
	)
	{
		// Defect D12: a missing validator surfaced as a 500 on every request rather than once, at startup.

		// Arrange
		await using var app = await TestApp.StartAsync(
			services => services.AddZodSharp(),
			app =>
				app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok())
					.WithZodSharpValidation<OrderRequest>(),
			cancellationToken
		);

		// Act
		var exception = BuildEndpointsAndCatch(app.App);

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains("parameter 'order'");
		await Assert.That(exception.Message).Contains(typeof(OrderRequest).FullName!);
	}

	[Test]
	public async Task WithZodSharpValidation_GivenThrowIfNoValidatorAndUnregisteredComplexParameter_FailsWhenTheEndpointIsBuilt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => services.AddZodSharp(),
			app =>
				app.MapPost("/orders/{id}", (int id, OrderRequest order) => TypedResults.Ok())
					.WithZodSharpValidation(options => options.ThrowIfNoValidator = true),
			cancellationToken
		);

		// Act
		var exception = BuildEndpointsAndCatch(app.App);

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Message).Contains("parameter 'order'");
	}

	[Test]
	public async Task WithZodSharpValidation_GivenParametersWithoutValidators_SkipsThem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => services.AddZodSharp(),
			app =>
				app.MapPost("/orders/{id}", (int id, OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync("/orders/1", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	public async Task WithZodSharpValidation_OnRouteGroup_ValidatesEveryEndpointInTheGroup(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app =>
			{
				var group = app.MapGroup("/orders").WithZodSharpValidation();
				group.MapPost("/", (OrderRequest order) => TypedResults.Ok());
				group.MapPut("/{id}", (int id, OrderRequest order) => TypedResults.Ok());
			},
			cancellationToken
		);

		// Act
		using var post = await app.Client.PostAsJsonAsync("/orders/", InvalidOrder, cancellationToken);
		using var put = await app.Client.PutAsJsonAsync("/orders/7", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(post.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
	}

	[Test]
	public async Task WithZodSharpValidation_GivenServiceParameter_DoesNotValidateIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services);
				services.AddSingleton(InvalidOrder);
			},
			app =>
				app.MapGet("/orders/current", ([FromServices] OrderRequest current) => TypedResults.Ok())
					.WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(new Uri("/orders/current", UriKind.Relative), cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	public async Task WithZodSharpValidation_GivenSequenceParameter_PrefixesTheParameterName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		DelegateValidator<List<OrderLine>> linesValidator = new(static lines =>
			lines.Select(
				static (line, index) =>
					ValidationError.Create(
						"too_small",
						"Quantity must be at least 1",
						ZodPath.From(ZodPathSegment.At(index), ZodPathSegment.Member("Quantity"))
					)
			)
		);
		await using var app = await TestApp.StartAsync(
			services =>
			{
				services.AddZodSharpFactory(factory => factory.Register(linesValidator));
				services.AddZodSharpProblemDetails();
			},
			app => app.MapPost("/lines", (List<OrderLine> lines) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync(
			"/lines",
			new List<OrderLine> { new() },
			cancellationToken
		);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		// Assert
		await Assert
			.That(document.RootElement.GetProperty("errors").TryGetProperty("lines[0].Quantity", out _))
			.IsTrue();
	}

	[Test]
	public async Task WithZodSharpValidation_GivenGeneratedValidatorRegisteredByAssemblyScan_ValidatesTheParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				services.AddZodSharp();
				services.AddZodSharpAssembly(typeof(ValidatedRequest).Assembly);
			},
			app => app.MapPost("/requests", (ValidatedRequest request) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.PostAsJsonAsync(
			"/requests",
			new ValidatedRequest { Name = "a" },
			cancellationToken
		);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(document.RootElement.GetProperty("errors").TryGetProperty("Name", out _)).IsTrue();
	}

	[Test]
	public async Task WithZodSharpValidation_AddsValidationProblemResponseMetadata(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		var metadata = GetEndpoints(app.App)
			.SelectMany(static endpoint => endpoint.Metadata.OfType<IProducesResponseTypeMetadata>())
			.ToArray();

		// Assert
		await Assert
			.That(
				metadata.Any(static m =>
					m.StatusCode == StatusCodes.Status400BadRequest
					&& m.Type == typeof(HttpValidationProblemDetails)
					&& m.ContentTypes.Contains("application/problem+json")
				)
			)
			.IsTrue();
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task WithZodSharpValidation_GivenBindingFailureAndPassThrough_KeepsTheBindersResponse(
		CancellationToken cancellationToken
	)
	{
		// Defect D11: .NET 11 runs endpoint filters after a binding failure; validating the unbound arguments
		// replaced the binder's response.

		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app =>
				app.MapGet("/customers", (int page, [AsParameters] CustomerQuery customer) => TypedResults.Ok())
					.WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(
			new Uri("/customers?page=abc", UriKind.Relative),
			cancellationToken
		);
		var body = await response.Content.ReadAsStringAsync(cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(body).DoesNotContain("\"issues\"");
	}

	[Test]
	public async Task WithZodSharpValidation_GivenBindingFailureAndConvertToProblemDetails_WritesTheContract(
		CancellationToken cancellationToken
	)
	{
		// Defect D11: opt-in conversion of a binding failure into the ZodSharp contract.

		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app =>
				app.MapGet("/customers", (int page, [AsParameters] CustomerQuery customer) => TypedResults.Ok())
					.WithZodSharpValidation(options =>
						options.OnBindingFailure = ZodBindingFailureBehavior.ConvertToProblemDetails
					),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(
			new Uri("/customers?page=abc", UriKind.Relative),
			cancellationToken
		);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var issue = document.RootElement.GetProperty("issues")[0];

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(issue.GetProperty("code").GetString()).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(issue.GetProperty("category").GetString()).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(document.RootElement.GetProperty("errors").TryGetProperty("Name", out _)).IsFalse();
	}
#endif

	static Endpoint[] GetEndpoints(WebApplication app) =>
		[.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints)];

	static InvalidOperationException? BuildEndpointsAndCatch(WebApplication app)
	{
		try
		{
			_ = GetEndpoints(app);
			return null;
		}
		catch (InvalidOperationException ex)
		{
			return ex;
		}
	}
}

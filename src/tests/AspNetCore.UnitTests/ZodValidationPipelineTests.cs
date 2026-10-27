#if NET11_0_OR_GREATER
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Mode A: validation through ASP.NET Core's built-in Microsoft.Extensions.Validation pipeline
/// (<c>AddZodSharpValidation()</c>), enriched to the full ZodSharp contract by <c>AddZodSharpProblemDetails()</c>.
/// </summary>
public class ZodValidationPipelineTests
{
	static readonly OrderRequest ValidOrder = new() { Email = "a@b.c", Items = [new() { Quantity = 1 }] };
	static readonly OrderRequest InvalidOrder = new() { Email = "nope", Items = [new() { Quantity = 0 }] };

	public enum CustomizeRegistration
	{
		ConfiguredBefore,
		ConfiguredAfter,
		ProblemDetailsArgument,
	}

	static void AddServices(
		IServiceCollection services,
		IZodSchemaValidator<OrderRequest>? orderValidator = null,
		Action<ZodProblemDetailsOptions>? configure = null
	)
	{
		services.AddZodSharp(options =>
			options.ConfigureFactory = factory => factory.Register(orderValidator ?? new OrderRequestValidator())
		);
		services.AddZodSharpValidation();
		services.AddZodSharpProblemDetails(options =>
		{
			options.Registry = new ErrorTypeRegistry();
			configure?.Invoke(options);
		});
	}

	static async Task<(HttpStatusCode StatusCode, string? MediaType, JsonElement Root)> PostAsync(
		TestApp app,
		string path,
		object body,
		CancellationToken cancellationToken
	)
	{
		using var response = await app.Client.PostAsJsonAsync(path, body, cancellationToken);
		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		return (
			response.StatusCode,
			response.Content.Headers.ContentType?.MediaType,
			json.Length == 0 ? default : JsonElement.Parse(json)
		);
	}

	static Uri Relative(string path) => new(path, UriKind.Relative);

	[Test]
	public async Task Pipeline_GivenInvalidBodyWithoutAddProblemDetails_WritesZodSharpContract(
		CancellationToken cancellationToken
	)
	{
		// Arrange - AddProblemDetails() is never called by the application: AddZodSharpProblemDetails registers it,
		// which is what makes the CustomizeProblemDetails enrichment run.
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(mediaType).IsEqualTo("application/problem+json");
		await Assert.That(app.App.Services.GetService<IProblemDetailsService>()).IsNotNull();
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(2);
		await Assert.That(root.GetProperty("issues")[0].GetProperty("code").GetString()).IsEqualTo("invalid_email");
		await Assert.That(root.GetProperty("issues")[1].GetProperty("code").GetString()).IsEqualTo("too_small");
		await Assert.That(root.GetProperty("issues")[1].GetProperty("minimum").GetInt32()).IsEqualTo(1);
		await Assert.That(root.TryGetProperty("traceId", out _)).IsTrue();
	}

	[Test]
	public async Task Pipeline_GivenZodBodyAndDataAnnotationsQuery_MergesErrorsAndIssues(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services);
				services.AddValidation();
			},
			app => app.MapPost("/orders", (OrderRequest order, [Range(1, 10)] int page) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders?page=0", InvalidOrder, cancellationToken);
		var errors = root.GetProperty("errors");
		var issues = root.GetProperty("issues");

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(errors.GetProperty("Email")[0].GetString()).IsEqualTo("Email must be a valid email address");
		await Assert
			.That(errors.GetProperty("Items[0].Quantity")[0].GetString())
			.IsEqualTo("Quantity must be at least 1");
		await Assert.That(errors.TryGetProperty("page", out var page)).IsTrue();
		await Assert.That(issues.GetArrayLength()).IsEqualTo(3);
		await Assert.That(issues[0].GetProperty("code").GetString()).IsEqualTo("invalid_email");
		await Assert.That(issues[1].GetProperty("code").GetString()).IsEqualTo("too_small");
		await Assert
			.That(issues[2].GetProperty("code").GetString())
			.IsEqualTo(ZodProblemDetailsWriter.DataAnnotationsCode);
		await Assert.That(issues[2].GetProperty("category").GetString()).IsEqualTo(ZodIssueCategories.InvalidValue);
		await Assert.That(issues[2].GetProperty("path").GetString()).IsEqualTo("page");
		await Assert.That(issues[2].GetProperty("message").GetString()).IsEqualTo(page[0].GetString());
	}

	[Test]
	public async Task Pipeline_GivenDataAnnotationsOnlyFailure_ProjectsIssuesAndKeepsPayload(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services);
				services.AddValidation();
			},
			app => app.MapGet("/items", ([Range(1, 10)] int page) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(Relative("/items?page=0"), cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(root.GetProperty("title").GetString()).IsEqualTo(ZodProblemDetailsWriter.DefaultTitle);
		await Assert.That(root.GetProperty("errors").TryGetProperty("page", out _)).IsTrue();
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(1);
		await Assert
			.That(root.GetProperty("issues")[0].GetProperty("code").GetString())
			.IsEqualTo(ZodProblemDetailsWriter.DataAnnotationsCode);
	}

	[Test]
	public async Task Pipeline_GivenProjectDataAnnotationsIssuesOff_LeavesDataAnnotationsPayloadUntouched(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services, configure: static options => options.ProjectDataAnnotationsIssues = false);
				services.AddValidation();
			},
			app => app.MapGet("/items", ([Range(1, 10)] int page) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(Relative("/items?page=0"), cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(document.RootElement.GetProperty("errors").TryGetProperty("page", out _)).IsTrue();
		await Assert.That(document.RootElement.TryGetProperty("issues", out _)).IsFalse();
	}

	[Test]
	public async Task Pipeline_GivenRouteGroup_ValidatesEveryEndpointAutomatically(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app =>
			{
				var orders = app.MapGroup("/orders");
				orders.MapPost("/", (OrderRequest order) => TypedResults.Ok());
				orders.MapPut("/{id}", (int id, OrderRequest order) => TypedResults.Ok());
			},
			cancellationToken
		);

		// Act
		var (postStatus, _, postRoot) = await PostAsync(app, "/orders/", InvalidOrder, cancellationToken);
		using var put = await app.Client.PutAsJsonAsync("/orders/1", InvalidOrder, cancellationToken);
		using var valid = await app.Client.PostAsJsonAsync("/orders/", ValidOrder, cancellationToken);

		// Assert
		await Assert.That(postStatus).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(postRoot.GetProperty("issues").GetArrayLength()).IsEqualTo(2);
		await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(valid.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	public async Task Pipeline_GivenDisableValidation_DoesNotValidate(CancellationToken cancellationToken)
	{
		// Arrange
		OrderRequestValidator validator = new();
		await using var app = await TestApp.StartAsync(
			services => AddServices(services, validator),
			app =>
			{
				app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()).DisableValidation();
				app.MapGroup("/imports").DisableValidation().MapPost("/", (OrderRequest order) => TypedResults.Ok());
			},
			cancellationToken
		);

		// Act
		using var endpoint = await app.Client.PostAsJsonAsync("/orders", InvalidOrder, cancellationToken);
		using var group = await app.Client.PostAsJsonAsync("/imports/", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(endpoint.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(group.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(validator.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task Pipeline_GivenFromServicesParameter_DoesNotValidateIt(CancellationToken cancellationToken)
	{
		// Arrange
		OrderRequestValidator validator = new();
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services, validator);
				services.AddSingleton(InvalidOrder);
			},
			app => app.MapGet("/orders", ([FromServices] OrderRequest order) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		using var response = await app.Client.GetAsync(Relative("/orders"), cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(validator.Calls).IsEqualTo(0);
	}

	[Test]
	public async Task Pipeline_GivenErrorTypeWith422_ChangesStatus(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
				AddServices(services, configure: static options =>
					options.MapErrorType(
						new ErrorType("invalid_email", HttpStatus: StatusCodes.Status422UnprocessableEntity)
					)
				),
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.UnprocessableEntity);
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert
			.That(root.GetProperty("type").GetString())
			.IsEqualTo("https://tools.ietf.org/html/rfc9110#section-15.5.21");
	}

	[Test]
	public async Task Pipeline_GivenStatusCodeSelectorAndOverride_OverrideWinsLast(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(services => AddServices(services, configure: static options =>
				{
					options.MapErrorType(
						new ErrorType("invalid_email", HttpStatus: StatusCodes.Status422UnprocessableEntity)
					);
					options.StatusCodeSelector = static _ => StatusCodes.Status409Conflict;
					options.StatusCodeOverride = static (_, computed) =>
						computed == StatusCodes.Status409Conflict ? StatusCodes.Status418ImATeapot : computed;
				}), app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()), cancellationToken);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders", InvalidOrder, cancellationToken);

		// Assert - the 422 ErrorType is computed, the selector replaces it, and the override sees and replaces that.
		await Assert.That((int)status).IsEqualTo(StatusCodes.Status418ImATeapot);
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status418ImATeapot);
	}

	[Test]
	[Arguments(CustomizeRegistration.ConfiguredBefore)]
	[Arguments(CustomizeRegistration.ConfiguredAfter)]
	[Arguments(CustomizeRegistration.ProblemDetailsArgument)]
	public async Task Pipeline_GivenUserCustomizeProblemDetails_RunsAfterZodSharpAndCanModifyResult(
		CustomizeRegistration registration,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		static void UserCallback(ProblemDetailsContext context)
		{
			// Runs second: the ZodSharp issues are already there, and the user's change is the one that is written.
			context.ProblemDetails.Extensions["sawIssues"] = context.ProblemDetails.Extensions.ContainsKey("issues");
			context.ProblemDetails.Title = "Customized by the application";
			context.ProblemDetails.Status = StatusCodes.Status422UnprocessableEntity;
			context.HttpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
		}

		await using var app = await TestApp.StartAsync(
			services =>
			{
				if (registration == CustomizeRegistration.ConfiguredBefore)
					services.AddProblemDetails(static options => options.CustomizeProblemDetails = UserCallback);

				services.AddZodSharp(static options =>
					options.ConfigureFactory = static factory => factory.Register(new OrderRequestValidator())
				);
				services.AddZodSharpValidation();
				services.AddZodSharpProblemDetails(
					static options => options.Registry = new ErrorTypeRegistry(),
					registration == CustomizeRegistration.ProblemDetailsArgument
						? static options => options.CustomizeProblemDetails = UserCallback
						: null
				);

				if (registration == CustomizeRegistration.ConfiguredAfter)
					services.Configure<ProblemDetailsOptions>(static options =>
						options.CustomizeProblemDetails = UserCallback
					);
			},
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.UnprocessableEntity);
		await Assert.That(root.GetProperty("sawIssues").GetBoolean()).IsTrue();
		await Assert.That(root.GetProperty("title").GetString()).IsEqualTo("Customized by the application");
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(StatusCodes.Status422UnprocessableEntity);
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(2);
	}

	[Test]
	public async Task Pipeline_GivenFilterOnValidatedEndpoint_ValidatesOnceAndLogsAtDebug(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		OrderRequestValidator validator = new();
		using CapturingLoggerProvider logs = new();
		await using var app = await TestApp.StartAsync(
			builder =>
			{
				builder.Logging.AddProvider(logs);
				builder.Logging.SetMinimumLevel(LogLevel.Debug);
				AddServices(builder.Services, validator);
			},
			app => app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation(),
			cancellationToken
		);

		// Act
		using var invalid = await app.Client.PostAsJsonAsync("/orders", InvalidOrder, cancellationToken);
		var callsAfterInvalid = validator.Calls;
		using var valid = await app.Client.PostAsJsonAsync("/orders", ValidOrder, cancellationToken);
		var guardLogs = logs
			.Messages.Where(static message =>
				message.Message.Contains("WithZodSharpValidation", StringComparison.Ordinal)
			)
			.ToArray();

		// Assert
		await Assert.That(invalid.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(callsAfterInvalid).IsEqualTo(1);
		await Assert.That(valid.StatusCode).IsEqualTo(HttpStatusCode.OK);
		await Assert.That(validator.Calls).IsEqualTo(2);
		await Assert.That(guardLogs.Length).IsEqualTo(1);
		await Assert.That(guardLogs[0].Level).IsEqualTo(LogLevel.Debug);
		await Assert.That(guardLogs[0].Message).Contains("'order'");
	}

	[Test]
	public async Task Pipeline_GivenFilterOnEndpointWithValidationDisabled_FilterStillValidates(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		OrderRequestValidator validator = new();
		await using var app = await TestApp.StartAsync(
			services => AddServices(services, validator),
			app =>
				app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok())
					.WithZodSharpValidation()
					.DisableValidation(),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/orders", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(root.GetProperty("issues").GetArrayLength()).IsEqualTo(2);
		await Assert.That(validator.Calls).IsEqualTo(1);
	}

	[Test]
	public async Task Pipeline_GivenUnionBody_ValidatesActiveCaseWithZodSharp(CancellationToken cancellationToken)
	{
		// Arrange - the bridge claims parameters of ZodSharp-validated types only; a union parameter is claimed by
		// the runtime parameter resolver the application's own AddValidation() call adds, which then asks the
		// bridge for the union's type info.
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services);
				services.AddValidation();
			},
			app => app.MapPost("/shipments", (OrderOrReference body) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, mediaType, root) = await PostAsync(app, "/shipments", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(root.GetProperty("issues")[0].GetProperty("code").GetString()).IsEqualTo("invalid_email");
		// The active case is reported under the union's Value member, Microsoft.Extensions.Validation's convention.
		await Assert.That(root.GetProperty("issues")[0].GetProperty("path").GetString()).IsEqualTo("Value.Email");
		await Assert.That(root.GetProperty("errors").TryGetProperty("Value.Email", out _)).IsTrue();
		await Assert.That(root.GetProperty("errors").TryGetProperty("Value.Items[0].Quantity", out _)).IsTrue();
	}

	[Test]
	public async Task Pipeline_GivenUnionBodyWithoutApplicationAddValidation_StillValidatesActiveCase(
		CancellationToken cancellationToken
	)
	{
		// Arrange - only the ZodSharp registrations: the bridge must claim the union parameter itself.
		await using var app = await TestApp.StartAsync(
			services => AddServices(services),
			app => app.MapPost("/shipments", (OrderOrReference body) => TypedResults.Ok()),
			cancellationToken
		);

		// Act
		var (status, _, root) = await PostAsync(app, "/shipments", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(root.GetProperty("errors").TryGetProperty("Value.Email", out _)).IsTrue();
	}

	[Test]
	[Arguments("fr", "L'adresse e-mail doit être valide", "La quantité doit être au moins 1")]
	[Arguments("en", "Email must be a valid email address", "Quantity must be at least 1")]
	public async Task Pipeline_GivenAcceptLanguage_LocalizesMessagesButNotCodesOrPaths(
		string culture,
		string expectedEmail,
		string expectedQuantity,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				AddServices(services);
				services.AddLocalization();
				services.AddSingleton<IZodMessageFormatter>(static sp => new StringLocalizerZodMessageFormatter(
					sp.GetRequiredService<IStringLocalizerFactory>(),
					sharedResourceType: typeof(ValidationMessages)
				));
			},
			app =>
			{
				app.UseRequestLocalization(static options =>
					options.SetDefaultCulture("en").AddSupportedCultures("en", "fr").AddSupportedUICultures("en", "fr")
				);
				app.MapPost("/orders", (OrderRequest order) => TypedResults.Ok());
			},
			cancellationToken
		);
		using HttpRequestMessage request = new(HttpMethod.Post, "/orders")
		{
			Content = JsonContent.Create(InvalidOrder),
		};
		request.Headers.AcceptLanguage.ParseAdd(culture);

		// Act
		using var response = await app.Client.SendAsync(request, cancellationToken);
		using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
		var root = document.RootElement;
		var issues = root.GetProperty("issues");

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(root.GetProperty("errors").GetProperty("Email")[0].GetString()).IsEqualTo(expectedEmail);
		await Assert
			.That(root.GetProperty("errors").GetProperty("Items[0].Quantity")[0].GetString())
			.IsEqualTo(expectedQuantity);
		await Assert.That(issues[0].GetProperty("code").GetString()).IsEqualTo("invalid_email");
		await Assert.That(issues[0].GetProperty("path").GetString()).IsEqualTo("Email");
		await Assert.That(issues[0].GetProperty("message").GetString()).IsEqualTo(expectedEmail);
		await Assert.That(issues[1].GetProperty("code").GetString()).IsEqualTo("too_small");
		await Assert.That(issues[1].GetProperty("path").GetString()).IsEqualTo("Items[0].Quantity");
	}
}
#endif

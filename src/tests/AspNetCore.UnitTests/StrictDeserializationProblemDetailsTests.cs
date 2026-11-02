using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// A minimal-API body rejected by a strict value object converter while it is deserialized
/// (<c>JsonException(inner: ZodException)</c>) is answered with the errors located under the member that held the
/// value - the same body validating the hydrated model produces.
/// </summary>
public class StrictDeserializationProblemDetailsTests
{
	const string InvalidEmailBody = /*lang=json,strict*/
		"""{"customerEmail":"not-an-email","quantity":5}""";

	[Test]
	public async Task PostBody_GivenStrictConverterRejectsMember_ReportsTheMemberPath(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (response, document) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken);

		// Assert
		using (document)
		{
			await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");

			var root = document!.RootElement;
			var errors = root.GetProperty("errors");
			await Assert
				.That(errors.EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["CustomerEmail.Value"]);
			await Assert
				.That(errors.GetProperty("CustomerEmail.Value")[0].GetString())
				.IsEqualTo("Field 'CustomerEmail' must be a valid email address.");

			var issue = root.GetProperty("issues").EnumerateArray().Single();
			await Assert.That(issue.GetProperty("path").GetString()).IsEqualTo("CustomerEmail.Value");
			await Assert
				.That(
					string.Join('|', issue.GetProperty("segments").EnumerateArray().Select(static s => s.GetString()))
				)
				.IsEqualTo("CustomerEmail|Value");
			await Assert.That(issue.GetProperty("code").GetString()).IsEqualTo("invalid_string");
			await Assert.That(issue.GetProperty("origin").GetString()).IsEqualTo("string");
		}
	}

	[Test]
	public async Task PostBody_GivenStrictConverterRejectsMember_MatchesValidatingTheHydratedModel(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (strict, strictDocument) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken);
		var (hydrated, hydratedDocument) = await PostAsync(app, "/hydrated/orders", "{}", cancellationToken);

		// Assert
		using (strictDocument)
		using (hydratedDocument)
		{
			await Assert.That(strict.StatusCode).IsEqualTo(hydrated.StatusCode);
			await AssertSameContract(strictDocument!, hydratedDocument!);
		}
	}

	[Test]
	[Arguments( /*lang=json,strict*/
		"""{"customer":{"email":"x"},"lines":[{"quantity":1}]}""",
		"/hydrated/shipping/email",
		"Customer.Email.Value"
	)]
	[Arguments( /*lang=json,strict*/
		"""{"customer":{"email":"ada@example.com"},"lines":[{"quantity":1},{"quantity":0}]}""",
		"/hydrated/shipping/quantity",
		"Lines[1].Quantity.Value"
	)]
	public async Task PostBody_GivenNestedOrCollectionMemberRejected_ReportsTheFullMemberPath(
		string body,
		string hydratedRoute,
		string expectedKey,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (strict, strictDocument) = await PostAsync(app, "/shipping", body, cancellationToken);
		var (_, hydratedDocument) = await PostAsync(app, hydratedRoute, "{}", cancellationToken);

		// Assert
		using (strictDocument)
		using (hydratedDocument)
		{
			await Assert.That(strict.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert
				.That(strictDocument!.RootElement.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo([expectedKey]);
			await AssertSameContract(strictDocument, hydratedDocument!);
		}
	}

	[Test]
	public async Task PostBody_GivenJsonPathNaming_UsesJsonNamesLikeValidatingTheHydratedModel(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o =>
		{
			o.HandleBindingFailures = true;
			o.PathNaming = ZodPathNaming.Json;
		});

		// Act
		var (_, strictDocument) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken);
		var (_, hydratedDocument) = await PostAsync(app, "/hydrated/orders", "{}", cancellationToken);

		// Assert
		using (strictDocument)
		using (hydratedDocument)
		{
			var root = strictDocument!.RootElement;
			await Assert
				.That(root.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["customerEmail.value"]);
			await Assert
				.That(
					string.Join(
						'|',
						root.GetProperty("issues")[0]
							.GetProperty("segments")
							.EnumerateArray()
							.Select(static s => s.GetString())
					)
				)
				.IsEqualTo("customerEmail|value");
			await AssertSameContract(strictDocument, hydratedDocument!);
		}
	}

	[Test]
	[Arguments("""{"customerEmail":""", "application/json", HttpStatusCode.BadRequest)]
	[Arguments( /*lang=json,strict*/
		"""{"customerEmail":"ada@example.com","quantity":"five"}""",
		"application/json",
		HttpStatusCode.BadRequest
	)]
	public async Task PostBody_GivenBadRequestWithoutZodErrors_AnswersItsOwnStatusWithStandardProblemDetails(
		string body,
		string mediaType,
		HttpStatusCode expectedStatus,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (response, document) = await PostAsync(app, "/orders", body, cancellationToken, mediaType);

		// Assert
		using (document)
		{
			await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
			await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");

			var root = document!.RootElement;
			await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo((int)expectedStatus);
			await Assert.That(root.TryGetProperty("issues", out _)).IsFalse();
			await Assert.That(root.TryGetProperty("errors", out _)).IsFalse();
			// The serializer's message can quote the request, so it is never written.
			await Assert.That(root.TryGetProperty("detail", out _)).IsFalse();
		}
	}

	[Test]
	public async Task PostBody_GivenUnsupportedContentTypeWithOptIn_IsStillAnsweredByBindingWith415(
		CancellationToken cancellationToken
	)
	{
		// Arrange: binding answers an unexpected content type itself, whatever ThrowOnBadRequest says.
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (response, document) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken, "text/plain");

		// Assert
		using (document)
			await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnsupportedMediaType);
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task PostBody_GivenProductionWithoutOptIn_RewritesBindingsProblemWithTheMemberPath(
		CancellationToken cancellationToken
	)
	{
		// Arrange: .NET 11 binding writes its own validation problem for an unreadable body, through the ProblemDetails
		// service with the exception attached, so ZodSharp rewrites it without ThrowOnBadRequest.
		await using var app = await StartAsync(cancellationToken);

		// Act
		var (strict, strictDocument) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken);
		var (_, hydratedDocument) = await PostAsync(app, "/hydrated/orders", "{}", cancellationToken);

		// Assert
		using (strictDocument)
		using (hydratedDocument)
		{
			await Assert.That(strict.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert
				.That(strictDocument!.RootElement.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["CustomerEmail.Value"]);
			await AssertSameContract(strictDocument, hydratedDocument!);
		}
	}

	[Test]
	public async Task PostBody_GivenProductionWithoutOptInAndMalformedJson_LeavesBindingsProblemKeyedByJsonPath(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken);

		// Act
		var (response, document) = await PostAsync(app, "/orders", """{"customerEmail":""", cancellationToken);

		// Assert
		using (document)
		{
			await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert
				.That(document!.RootElement.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["$.customerEmail"]);
		}
	}
#else
	[Test]
	[Arguments(InvalidEmailBody)]
	[Arguments("""{"customerEmail":""")]
	public async Task PostBody_GivenProductionWithoutOptIn_AnswersBareBadRequestAsBefore(
		string body,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken);

		// Act
		var (response, document) = await PostAsync(app, "/orders", body, cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(document).IsNull();
	}
#endif

	[Test]
	public async Task PostBody_GivenDevelopmentWithoutOptIn_ReportsTheMemberPath(CancellationToken cancellationToken)
	{
		// Arrange: Development already throws on a bad request, so the handler sees the failure without the opt-in.
		await using var app = await StartAsync(cancellationToken, environment: Environments.Development);

		// Act
		var (response, document) = await PostAsync(app, "/orders", InvalidEmailBody, cancellationToken);

		// Assert
		using (document)
		{
			await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert
				.That(document!.RootElement.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["CustomerEmail.Value"]);
		}
	}

	[Test]
	public async Task Handler_GivenBareZodException_ReportsItsOwnPathAsBefore(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await StartAsync(cancellationToken, static o => o.HandleBindingFailures = true);

		// Act
		var (response, document) = await PostAsync(app, "/throws", "{}", cancellationToken);

		// Assert
		using (document)
		{
			await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
			await Assert
				.That(document!.RootElement.GetProperty("errors").EnumerateObject().Select(static p => p.Name))
				.IsEquivalentTo(["Value"]);
		}
	}

	[Test]
	public async Task TryHandleAsync_GivenOtherBadRequestWithoutOptIn_ReturnsFalse(CancellationToken cancellationToken)
	{
		// Arrange
		ZodExceptionHandler handler = new(Microsoft.Extensions.Options.Options.Create(new ZodProblemDetailsOptions()));
		DefaultHttpContext httpContext = new();
		BadHttpRequestException exception = new("Failed to read the body.", new JsonException("Unexpected end."));

		// Act
		var handled = await handler.TryHandleAsync(httpContext, exception, cancellationToken);

		// Assert
		await Assert.That(handled).IsFalse();
	}

	[Test]
	public async Task TryHandleAsync_GivenBadRequestCarryingZodException_ReturnsTrueWithoutOptIn(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ZodExceptionHandler handler = new(Microsoft.Extensions.Options.Options.Create(new ZodProblemDetailsOptions()));
		DefaultHttpContext httpContext = new();
		httpContext.Response.Body = new MemoryStream();
		BadHttpRequestException exception = new(
			"Failed to read the body.",
			new JsonException(
				"Cannot deserialize EmailAddress: the value failed validation.",
				new ZodException([ValidationError.Create("invalid", "Invalid value.", ZodPath.Member("Value"))])
			)
		);

		// Act
		var handled = await handler.TryHandleAsync(httpContext, exception, cancellationToken);

		// Assert
		await Assert.That(handled).IsTrue();
		await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
	}

	static async Task AssertSameContract(JsonDocument actual, JsonDocument expected)
	{
		foreach (var member in new[] { "status", "title", "type", "errors", "issues" })
		{
			await Assert
				.That(actual.RootElement.GetProperty(member).GetRawText())
				.IsEqualTo(expected.RootElement.GetProperty(member).GetRawText());
		}
	}

	static Task<TestApp> StartAsync(
		CancellationToken cancellationToken,
		Action<ZodProblemDetailsOptions>? configure = null,
		string environment = "Production"
	) =>
		TestApp.StartAsync(
			builder =>
			{
				builder.Services.ConfigureHttpJsonOptions(static options =>
					options.SerializerOptions.TypeInfoResolverChain.Insert(0, StrictValueObjectJsonContext.Default)
				);
				builder.Services.AddZodSharpProblemDetails(configure);
			},
			static app =>
			{
				app.UseExceptionHandler();
				app.MapPost("/orders", static (PlaceOrderRequest request) => Results.Ok());
				app.MapPost("/shipping", static (ShippingOrder order) => Results.Ok());
				// The same models validated after hydration (bound leniently, then validated): the target contract.
				app.MapPost("/hydrated/orders", static IResult () =>
					throw new ZodException(
						PlaceOrderRequestSchema
							.Validate(new PlaceOrderRequest(new EmailAddress("not-an-email"), new OrderQuantity(5)))
							.Errors
					)
				);
				app.MapPost("/hydrated/shipping/email", static IResult () =>
					throw new ZodException(
						ShippingOrderSchema
							.Validate(
								new ShippingOrder(
									new ShippingCustomer(new EmailAddress("x")),
									[new ShippingLine(new OrderQuantity(1))]
								)
							)
							.Errors
					)
				);
				app.MapPost("/hydrated/shipping/quantity", static IResult () =>
					throw new ZodException(
						ShippingOrderSchema
							.Validate(
								new ShippingOrder(
									new ShippingCustomer(new EmailAddress("ada@example.com")),
									[new ShippingLine(new OrderQuantity(1)), new ShippingLine(new OrderQuantity(0))]
								)
							)
							.Errors
					)
				);
				app.MapPost("/throws", static IResult () =>
					throw new ZodException([
						ValidationError.Create("invalid", "Invalid value.", ZodPath.Member("Value")),
					])
				);
			},
			cancellationToken,
			environment
		);

	static async Task<(HttpResponseMessage Response, JsonDocument? Document)> PostAsync(
		TestApp app,
		string path,
		string body,
		CancellationToken cancellationToken,
		string mediaType = "application/json"
	)
	{
		using StringContent content = new(body, Encoding.UTF8, mediaType);
		var response = await app.Client.PostAsync(new Uri(path, UriKind.Relative), content, cancellationToken);
		var text = await response.Content.ReadAsStringAsync(cancellationToken);
		return (response, text.Length == 0 ? null : JsonDocument.Parse(text));
	}
}

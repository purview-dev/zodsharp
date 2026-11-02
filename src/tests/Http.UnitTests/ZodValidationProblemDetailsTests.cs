using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore;
using ZodSharp.Core;
using ZodSharp.Http.Fixtures;

namespace ZodSharp.Http;

/// <summary>
/// Reading validation problems: <c>TryReadZodValidationProblemAsync</c>, <see cref="ZodValidationProblemDetails"/>, and
/// parity with what Purview.ZodSharp.AspNetCore writes.
/// </summary>
public class ZodValidationProblemDetailsTests
{
	[Test]
	public async Task TryReadZodValidationProblemAsync_GivenServerProblem_RoundTripsEveryErrorField(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ImmutableArray<ValidationError> written =
		[
			ValidationError.Create(
				"too_small",
				"Quantity must be at least 1",
				ZodPath.From(ZodPathSegment.Member("Lines"), ZodPathSegment.At(1), ZodPathSegment.Member("Quantity")),
				origin: "number",
				minimum: 1,
				inclusive: true,
				category: "too_small"
			),
			ValidationError.Create(
				"too_big",
				"Price must be less than 10.5",
				ZodPath.Member("Price"),
				origin: "number",
				category: "too_big"
			) with
			{
				Upper = new ErrorBound(10.5, Inclusive: false),
			},
			ValidationError.Create(
				"invalid_tenant",
				"Tenant is unknown",
				ZodPath.Member("Tenant"),
				new Dictionary<string, object?> { ["tenant"] = "acme" },
				category: "invalid_value"
			),
			ValidationError.Create("custom", "Order is invalid", ZodPath.Root),
			ZodIssues.InvalidUnion(
				[
					[
						ValidationError.Create(
							"invalid_type",
							"Required",
							ZodPath.Member("Name"),
							category: "invalid_type"
						),
					],
				],
				path: ZodPath.Member("Pet")
			),
		];
		await using var app = await StartServerAsync(written, cancellationToken);
		using var client = app.GetTestClient();
		using var response = await client.PostAsync(
			new Uri("/orders", UriKind.Relative),
			content: null,
			cancellationToken
		);

		// Act
		var problem = await response.TryReadZodValidationProblemAsync(cancellationToken);
		var read = problem!.ToValidationErrors();

		// Assert
		await Assert.That(problem.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert
			.That(read.Select(Describe))
			.IsEquivalentTo(written.Select(Describe), TUnit.Assertions.Enums.CollectionOrdering.Matching);
		var options = read.Single(static error => error.FullPath.ToDottedString() == "Pet").GetUnionErrors();
		await Assert
			.That(options.Single().Select(Describe))
			.IsEquivalentTo(written[^1].GetUnionErrors().Single().Select(Describe));
		await Assert
			.That(read.Single(static error => error.Code == "invalid_tenant").Parameters?["tenant"])
			.IsEqualTo("acme");
	}

	[Test]
	public async Task PostAsJsonValidatedAsync_GivenServerRejection_ReadsTheProblem(CancellationToken cancellationToken)
	{
		// Arrange
		var taken = ValidationError.Create("reference_taken", "Reference is taken", ZodPath.Member("Reference"));
		await using var app = await StartServerAsync([taken], cancellationToken);
		using var client = app.GetTestClient();

		// Act
		var sent = await client.PostAsJsonValidatedAsync(
			"/orders",
			new Order { Reference = "ABC-1", Quantity = 1 },
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);
		using var response = sent.Value!;
		var problem = await response.TryReadZodValidationProblemAsync(cancellationToken);
		var result = problem!.ToValidationResult<Order>();

		// Assert
		await Assert.That(sent.IsSuccess).IsTrue();
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo("reference_taken");
		await Assert.That(result.Errors.Single().FullPath).IsEqualTo(ZodPath.Member("Reference"));
	}

	[Test]
	public async Task TryReadZodValidationProblemAsync_GivenUnionIssue_ReadsTheOptions(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using HttpResponseMessage response = new(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(
				/*lang=json,strict*/"""
				{"status":400,"errors":{"Pet":["Invalid input"]},"issues":[{"path":"Pet","segments":["Pet"],
				"code":"invalid_union","category":"invalid_union","message":"Invalid input","unionErrors":[[{"path":"Name",
				"segments":["Name"],"code":"invalid_type","message":"Required"}]]}],"traceId":"00-abc"}
				""",
				Encoding.UTF8,
				"application/problem+json"
			),
		};

		// Act
		var problem = await response.TryReadZodValidationProblemAsync(cancellationToken);

		// Assert
		await Assert.That(problem).IsNotNull();
		await Assert.That(problem!.TraceId).IsEqualTo("00-abc");
		var error = problem.ToValidationErrors().Single();
		var options = error.GetUnionErrors();
		await Assert.That(options.Length).IsEqualTo(1);
		await Assert.That(options[0][0].Code).IsEqualTo("invalid_type");
	}

	[Test]
	[Arguments(
		HttpStatusCode.OK,
		"application/json", /*lang=json,strict*/
		"""{"errors":{"a":["b"]}}"""
	)]
	[Arguments(HttpStatusCode.BadRequest, "text/plain", "not json")]
	[Arguments(HttpStatusCode.BadRequest, "application/json", "{not json")]
	[Arguments(
		HttpStatusCode.NotFound,
		"application/problem+json", /*lang=json,strict*/
		"""{"title":"Not Found","status":404}"""
	)]
	public async Task TryReadZodValidationProblemAsync_GivenNonValidationResponse_ReturnsNull(
		HttpStatusCode status,
		string mediaType,
		string body,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using HttpResponseMessage response = new(status)
		{
			Content = new StringContent(body, Encoding.UTF8, mediaType),
		};

		// Act
		var problem = await response.TryReadZodValidationProblemAsync(cancellationToken);

		// Assert
		await Assert.That(problem).IsNull();
	}

	[Test]
	public async Task ToValidationErrors_GivenOnlyDottedErrors_ParsesTheKeysAsCustomErrors()
	{
		// Arrange
		ZodValidationProblemDetails problem = new()
		{
			Errors = new Dictionary<string, string[]>
			{
				["Lines[0].Sku"] = ["Sku is required"],
				[""] = ["Order is invalid"],
			},
		};

		// Act
		var errors = problem.ToValidationErrors();

		// Assert
		await Assert
			.That(errors.Select(static error => (error.FullPath, error.Code, error.Message)))
			.IsEquivalentTo([
				(
					ZodPath.From(ZodPathSegment.Member("Lines"), ZodPathSegment.At(0), ZodPathSegment.Member("Sku")),
					"custom",
					"Sku is required"
				),
				(ZodPath.Root, "custom", "Order is invalid"),
			]);
	}

	// Every field a client can observe, normalized so a numeric bound compares by value whatever its CLR type.
	static string Describe(ValidationError error) =>
		string.Join(
			'|',
			error.Code,
			error.Category,
			error.Message,
			error.FullPath.ToDottedString(),
			error.Origin,
			DescribeBound(error.Lower),
			DescribeBound(error.Upper)
		);

	static string DescribeBound(ErrorBound? bound) =>
		bound is { } value
			? string.Create(
				CultureInfo.InvariantCulture,
				$"{Convert.ToDecimal(value.Value, CultureInfo.InvariantCulture)}:{value.Inclusive}"
			)
			: "-";

	static async Task<WebApplication> StartServerAsync(
		ImmutableArray<ValidationError> errors,
		CancellationToken cancellationToken
	)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddZodSharpProblemDetails();
		var app = builder.Build();
		app.MapPost("/orders", (HttpContext context) =>
			ZodTypedResults.ValidationProblem(ValidationResult<Order>.Failure(errors), context)
		);
		await app.StartAsync(cancellationToken);
		return app;
	}
}

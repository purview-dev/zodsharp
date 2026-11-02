using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;
#if !NET11_0_OR_GREATER
using System.Net.Http.Json;
#endif

namespace ZodSharp.AspNetCore;

/// <summary>
/// One ProblemDetails contract on every path: the endpoint filter, a thrown <see cref="ZodException"/> handled by
/// <see cref="ZodExceptionHandler"/>, the manual results and, on .NET 11, the built-in validation pipeline.
/// </summary>
public class ProblemDetailsContractTests
{
	const string ExpectedContract = /*lang=json,strict*/
		"""
		{
		  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
		  "title": "One or more validation errors occurred.",
		  "status": 400,
		  "errors": {
		    "Email": ["Email must be a valid email address"],
		    "Items[0].Quantity": ["Quantity must be at least 1"]
		  },
		  "issues": [
		    { "path": "Email", "segments": ["Email"], "code": "invalid_email", "category": "invalid_format",
		      "origin": "string", "message": "Email must be a valid email address" },
		    { "path": "Items[0].Quantity", "segments": ["Items", 0, "Quantity"], "code": "too_small",
		      "category": "too_small", "origin": "number", "minimum": 1, "inclusive": true,
		      "message": "Quantity must be at least 1" }
		  ],
		  "traceId": "*"
		}
		""";

	static readonly OrderRequest InvalidOrder = new() { Email = "nope", Items = [new() { Quantity = 0 }] };

	static readonly string[] Paths =
	[
		"/filter",
		"/exception",
		"/manual",
		"/typed",
		"/problem",
#if NET11_0_OR_GREATER
		"/pipeline",
		"/pipeline-and-filter",
#endif
	];

	/// <summary>
	/// On .NET 11 the built-in validation pipeline would answer first; the endpoints that exercise the other paths
	/// opt out of it so each path is the one that writes the response.
	/// </summary>
	static TBuilder ModeBOnly<TBuilder>(TBuilder builder)
		where TBuilder : IEndpointConventionBuilder =>
#if NET11_0_OR_GREATER
		builder.DisableValidation();
#else
		builder;
#endif

	[Test]
	public async Task ProblemDetails_GivenTheSameErrors_IsIdenticalOnEveryPath(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			services =>
			{
				services.AddZodSharp(options =>
					options.ConfigureFactory = static factory => factory.Register(new OrderRequestValidator())
				);
				services.AddZodSharpProblemDetails(static options => options.Registry = new ErrorTypeRegistry());
#if NET11_0_OR_GREATER
				// Mode A: the built-in validation pipeline validates every endpoint below that does not opt out.
				services.AddZodSharpValidation();
#endif
			},
			app =>
			{
				app.UseExceptionHandler();

				ModeBOnly(app.MapPost("/filter", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation());
				ModeBOnly(
					app.MapPost("/exception", IResult (OrderRequest order) =>
						throw new ZodException(OrderRequestValidator.Check(order))
					)
				);
				ModeBOnly(
					app.MapPost(
						"/manual",
						IResult (OrderRequest order, IZodSchemaFactory factory, HttpContext context) =>
							factory.Validate(order) is { IsSuccess: false } result
								? result.ToValidationProblem(context)
								: TypedResults.Ok()
					)
				);
				ModeBOnly(
					app.MapPost(
						"/typed",
						IResult (OrderRequest order, IZodSchemaFactory factory, HttpContext context) =>
							factory.Validate(order) is { IsSuccess: false } result
								? ZodTypedResults.ValidationProblem(result, context)
								: TypedResults.Ok()
					)
				);
				ModeBOnly(
					app.MapPost(
						"/problem",
						IResult (OrderRequest order, IZodSchemaFactory factory, HttpContext context) =>
							factory.Validate(order) is { IsSuccess: false } result
								? ZodTypedResults.Problem(result, context)
								: TypedResults.Ok()
					)
				);
#if NET11_0_OR_GREATER
				// Mode A alone, and Mode A with the (then no-op) filter.
				app.MapPost("/pipeline", (OrderRequest order) => TypedResults.Ok());
				app.MapPost("/pipeline-and-filter", (OrderRequest order) => TypedResults.Ok()).WithZodSharpValidation();
#endif
			},
			cancellationToken
		);

		// Act
		List<(string Path, int Status, string? ContentType, string Body)> responses = [];
		foreach (var path in Paths)
		{
			using var response = await app.Client.PostAsJsonAsync(path, InvalidOrder, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			responses.Add(
				(
					path,
					(int)response.StatusCode,
					response.Content.Headers.ContentType?.MediaType,
					TestApp.NormalizeTraceId(body)
				)
			);
		}

		// Assert
		var reference = responses[0];
		await Assert
			.That(JsonNode.DeepEquals(JsonNode.Parse(reference.Body), JsonNode.Parse(ExpectedContract)))
			.IsTrue();

		foreach (var response in responses)
		{
			await Assert.That(response.Status).IsEqualTo(StatusCodes.Status400BadRequest).Because(response.Path);
			await Assert.That(response.ContentType).IsEqualTo("application/problem+json").Because(response.Path);
			await Assert.That(response.Body).IsEqualTo(reference.Body).Because(response.Path);
		}
	}
}

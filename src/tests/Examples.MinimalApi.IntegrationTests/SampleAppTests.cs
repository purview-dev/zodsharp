using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// Smoke tests for the minimal API sample, started in memory. CI additionally publishes it with Native AOT and
/// runs the same failing request against the native binary.
/// </summary>
public partial class SampleAppTests
{
	const string InvalidOrder = /*lang=json,strict*/
		"""
		{
			"customerEmail": "not-an-email",
			"shippingAddress": { "street": "1 Main Street", "city": "", "postCode": "LS1 1AA" },
			"lines": [{ "sku": "ABC-1234", "quantity": 0 }],
			"additionalAddresses": { "billing": { "street": "2 High Street", "city": "Leeds", "postCode": "1" } }
		}
		""";

	const string ValidOrder = /*lang=json,strict*/
		"""
		{
			"customerEmail": "ann@example.com",
			"shippingAddress": { "street": "1 Main Street", "city": "Leeds", "postCode": "LS1 1AA" },
			"lines": [{ "sku": "ABC-1234", "quantity": 2 }]
		}
		""";

	static async Task<(HttpStatusCode Status, string? MediaType, string Body)> PostAsync(
		SampleAppHost host,
		string path,
		string json,
		CancellationToken cancellationToken,
		string? acceptLanguage = null
	)
	{
		using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative))
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json"),
		};
		if (acceptLanguage is not null)
			request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

		using var response = await host.Client.SendAsync(request, cancellationToken);
		return (
			response.StatusCode,
			response.Content.Headers.ContentType?.MediaType,
			await response.Content.ReadAsStringAsync(cancellationToken)
		);
	}

	static string Code(JsonElement issues, string path)
	{
		foreach (var issue in issues.EnumerateArray())
		{
			if (issue.GetProperty("path").GetString() == path)
				return issue.GetProperty("code").GetString()!;
		}

		throw new InvalidOperationException($"No issue at '{path}'.");
	}

	[Test]
	public async Task PostOrders_GivenInvalidOrder_ReturnsZodValidationProblemDetails(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);

		// Act
		var (status, mediaType, body) = await PostAsync(host, "/orders", InvalidOrder, cancellationToken);
		var root = JsonElement.Parse(body);
		var errors = root.GetProperty("errors");
		var issues = root.GetProperty("issues");

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(mediaType).IsEqualTo("application/problem+json");
		await Assert.That(root.GetProperty("status").GetInt32()).IsEqualTo(400);
		await Assert
			.That(errors.EnumerateObject().Select(static e => e.Name))
			.IsEquivalentTo([
				"CustomerEmail",
				"ShippingAddress.City",
				"Lines[0].Quantity",
				"AdditionalAddresses[billing].PostCode",
			]);
		await Assert.That(Code(issues, "CustomerEmail")).IsEqualTo("invalid_string");
		await Assert.That(Code(issues, "ShippingAddress.City")).IsEqualTo("missing_field");
		await Assert.That(Code(issues, "Lines[0].Quantity")).IsEqualTo("invalid_range");
		await Assert.That(Code(issues, "AdditionalAddresses[billing].PostCode")).IsEqualTo("invalid_length");
		await Assert.That(root.TryGetProperty("traceId", out _)).IsTrue();
	}

	[Test]
	public async Task PostOrders_GivenValidOrder_ReturnsCreated(CancellationToken cancellationToken)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);

		// Act
		var (status, _, body) = await PostAsync(host, "/orders", ValidOrder, cancellationToken);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.Created);
		await Assert.That(JsonElement.Parse(body).GetProperty("lines").GetInt32()).IsEqualTo(1);
	}

	[Test]
	public async Task PostOrders_GivenPipelineFilterAndManualEndpoints_ReturnTheSameContract(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);

		// Act
		var pipeline = await PostAsync(host, "/orders", InvalidOrder, cancellationToken);
		var filter = await PostAsync(host, "/filtered/orders", InvalidOrder, cancellationToken);
		var manual = await PostAsync(host, "/manual/orders", InvalidOrder, cancellationToken);

		// Assert
		await Assert.That(filter.Status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(manual.Status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(NormalizeTraceId(filter.Body)).IsEqualTo(NormalizeTraceId(pipeline.Body));
		await Assert.That(NormalizeTraceId(manual.Body)).IsEqualTo(NormalizeTraceId(pipeline.Body));
	}

	[Test]
	public async Task PostCustomers_GivenRegisteredEmail_ReportsTheAsyncUniquenessRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);
		var customer = JsonSerializer.Serialize(
			new
			{
				name = "Ann",
				email = InMemoryCustomerEmailRegistry.SeededEmail,
				age = 30,
			}
		);

		// Act
		var (status, _, body) = await PostAsync(host, "/customers", customer, cancellationToken);
		var issues = JsonElement.Parse(body).GetProperty("issues");

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(Code(issues, "Email")).IsEqualTo(RegisterCustomerRequest.EmailTakenCode);
	}

	[Test]
	public async Task PostOrders_GivenFrenchAcceptLanguage_LocalizesMessagesButNotCodesOrPaths(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);

		// Act
		var (status, _, body) = await PostAsync(host, "/orders", InvalidOrder, cancellationToken, "fr");
		var root = JsonElement.Parse(body);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert
			.That(root.GetProperty("errors").GetProperty("Lines[0].Quantity")[0].GetString())
			.IsEqualTo("Le champ « Quantity » doit être compris entre 1 et 100.");
		await Assert.That(Code(root.GetProperty("issues"), "Lines[0].Quantity")).IsEqualTo("invalid_range");
	}

	[Test]
	public async Task OpenApiDocument_GivenValidatedEndpoints_DescribesZodValidationProblemDetails(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await SampleAppHost.StartAsync(cancellationToken);

		// Act
		var document = await host.Client.GetFromJsonAsync<JsonElement>(
			new Uri("/openapi/v1.json", UriKind.Relative),
			cancellationToken
		);
		var schemas = document.GetProperty("components").GetProperty("schemas");
		var badRequest = document
			.GetProperty("paths")
			.GetProperty("/orders")
			.GetProperty("post")
			.GetProperty("responses")
			.GetProperty("400")
			.GetProperty("content")
			.GetProperty("application/problem+json")
			.GetProperty("schema");

		// Assert
		await Assert.That(schemas.TryGetProperty("ZodValidationProblemDetails", out _)).IsTrue();
		await Assert.That(schemas.TryGetProperty("ZodValidationIssue", out _)).IsTrue();
		await Assert
			.That(badRequest.GetProperty("$ref").GetString())
			.IsEqualTo("#/components/schemas/ZodValidationProblemDetails");
	}

	[Test]
	public async Task Start_GivenSupportEmailIsACustomerAddress_FailsTheAsyncOptionsRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Dictionary<string, string?> settings = new()
		{
			["Sample:SupportEmail"] = InMemoryCustomerEmailRegistry.SeededEmail,
		};

		// Act
		async Task Act() => await (await SampleAppHost.StartAsync(cancellationToken, settings)).DisposeAsync();

		// Assert
		await Assert
			.That(Act)
			.Throws<OptionsValidationException>()
			.WithMessageContaining("Sample:SupportEmail: The support mailbox must not be a customer address");
	}

	static string NormalizeTraceId(string json) => TraceIdPattern().Replace(json, "\"traceId\":\"*\"");

	[GeneratedRegex("\"traceId\":\"[^\"]*\"")]
	private static partial Regex TraceIdPattern();
}

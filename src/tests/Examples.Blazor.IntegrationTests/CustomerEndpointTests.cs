using System.Net;
using System.Text;
using System.Text.Json;

namespace ZodSharp.Examples.Blazor;

/// <summary>
/// The API the WebAssembly form posts to: the ZodValidationProblemDetails contract it maps back onto its fields, the
/// uniqueness lookup and the culture switch.
/// </summary>
public class CustomerEndpointTests
{
	static async Task<(HttpStatusCode Status, string? MediaType, JsonElement Body)> PostCustomerAsync(
		HttpClient client,
		string json,
		CancellationToken cancellationToken,
		string? acceptLanguage = null
	)
	{
		using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/customers", UriKind.Relative))
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json"),
		};
		if (acceptLanguage is not null)
			request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

		using var response = await client.SendAsync(request, cancellationToken);
		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		return (
			response.StatusCode,
			response.Content.Headers.ContentType?.MediaType,
			body.Length == 0 ? default : JsonElement.Parse(body)
		);
	}

	static JsonElement Issue(JsonElement problem, string path) =>
		problem.GetProperty("issues").EnumerateArray().First(issue => issue.GetProperty("path").GetString() == path);

	[Test]
	public async Task PostCustomers_GivenInvalidCustomer_ReturnsZodValidationProblemDetails(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var (status, mediaType, problem) = await PostCustomerAsync(
			client,
			/*lang=json,strict*/"""{ "name": "A", "email": "not-an-email", "age": 5 }""",
			cancellationToken
		);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert.That(mediaType).IsEqualTo("application/problem+json");
		await Assert
			.That(problem.GetProperty("errors").EnumerateObject().Select(static e => e.Name))
			.IsEquivalentTo(["Name", "Email", "Age"]);
		await Assert.That(Issue(problem, "Email").GetProperty("code").GetString()).IsEqualTo("invalid_string");
		await Assert.That(Issue(problem, "Age").GetProperty("code").GetString()).IsEqualTo("invalid_range");
		await Assert.That(Issue(problem, "Age").GetProperty("segments").GetArrayLength()).IsEqualTo(1);
		await Assert.That(problem.TryGetProperty("traceId", out _)).IsTrue();
	}

	[Test]
	public async Task PostCustomers_GivenRegisteredEmailInFrench_ReturnsTheLocalizedAsyncRuleError(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var (status, _, problem) = await PostCustomerAsync(
			client,
			/*lang=json,strict*/$$"""{ "name": "Ann", "email": "{{InMemoryCustomerEmailRegistry.SeededEmail}}", "age": 30 }""",
			cancellationToken,
			acceptLanguage: "fr"
		);

		// Assert
		await Assert.That(status).IsEqualTo(HttpStatusCode.BadRequest);
		var issue = Issue(problem, "Email");
		await Assert.That(issue.GetProperty("code").GetString()).IsEqualTo(RegisterCustomerRequest.EmailTakenCode);
		await Assert
			.That(issue.GetProperty("message").GetString())
			.IsEqualTo("Cette adresse e-mail est déjà enregistrée");
	}

	[Test]
	public async Task PostCustomers_GivenValidCustomer_RegistersTheEmail(CancellationToken cancellationToken)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();
		const string customer = /*lang=json,strict*/
			"""{ "name": "Ann", "email": "ann@example.com", "age": 30 }""";

		// Act
		var (first, _, _) = await PostCustomerAsync(client, customer, cancellationToken);
		using var lookup = await client.GetAsync(
			new Uri("/api/customers/registered?email=ann@example.com", UriKind.Relative),
			cancellationToken
		);
		var registered = JsonElement.Parse(await lookup.Content.ReadAsStringAsync(cancellationToken));
		var (second, _, problem) = await PostCustomerAsync(client, customer, cancellationToken);

		// Assert
		await Assert.That(first).IsEqualTo(HttpStatusCode.Created);
		await Assert.That(registered.GetProperty("registered").GetBoolean()).IsTrue();
		await Assert.That(second).IsEqualTo(HttpStatusCode.BadRequest);
		await Assert
			.That(Issue(problem, "Email").GetProperty("code").GetString())
			.IsEqualTo(RegisterCustomerRequest.EmailTakenCode);
	}

	[Test]
	public async Task GetCultureSet_GivenFrench_StoresTheCultureAndRendersTheNextPageInFrench(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var document = await SampleAppFactory.GetDocumentAsync(
			client,
			"/culture/set?culture=fr&redirectUri=%2Freturns",
			cancellationToken
		);

		// Assert
		await Assert.That(document.DocumentElement.GetAttribute("lang")).IsEqualTo("fr");
		await Assert.That(document.QuerySelector("h1")?.TextContent).IsEqualTo("Return items");
	}

	[Test]
	public async Task GetCultureSet_GivenAnExternalRedirect_RedirectsHome(CancellationToken cancellationToken)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

		// Act
		using var response = await client.GetAsync(
			new Uri("/culture/set?culture=fr&redirectUri=https%3A%2F%2Fexample.com", UriKind.Relative),
			cancellationToken
		);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
		await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/");
	}
}

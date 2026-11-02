using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Examples.Blazor.Client;
using ZodSharp.Examples.Blazor.Components;

namespace ZodSharp.Examples.Blazor;

/// <summary>
/// The server-side half of the HttpClient story: the <see cref="CustomerAPIClient"/> the Interactive Server page uses,
/// resolved from the sample's own registration (a typed client from <c>IHttpClientFactory</c>, in a request scope) and
/// pointed at the in-memory server. Every call goes through the real API, its validation and its ProblemDetails.
/// </summary>
public class CustomerAPIClientTests
{
	[Test]
	public async Task RegisterAsync_GivenValidCustomer_RegistersThroughTheAPI(CancellationToken cancellationToken)
	{
		// Arrange
		await using ClientHost host = new();
		var customers = host.Resolve();
		var email = UniqueEmail();

		// Act
		var registration = await customers.RegisterAsync(
			new()
			{
				Name = "Ada",
				Email = email,
				Age = 36,
			},
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(registration.Outcome).IsEqualTo(RegistrationOutcome.Registered);
		await Assert.That(registration.Customer!.Email).IsEqualTo(email);
		await Assert.That(host.Posts).IsEqualTo(1);
	}

	[Test]
	public async Task RegisterAsync_GivenInvalidCustomer_RejectsWithoutSending(CancellationToken cancellationToken)
	{
		// Arrange
		await using ClientHost host = new();
		var customers = host.Resolve();

		// Act
		var registration = await customers.RegisterAsync(
			new()
			{
				Name = "A",
				Email = "not-an-email",
				Age = 5,
			},
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(registration.Outcome).IsEqualTo(RegistrationOutcome.RejectedBeforeSending);
		await Assert
			.That(registration.Errors.Select(static error => error.FullPath.ToDottedString()))
			.IsEquivalentTo(["Name", "Email", "Age"]);
		await Assert.That(host.Posts).IsEqualTo(0);
	}

	[Test]
	public async Task RegisterAsync_GivenRegisteredEmail_RunsTheServiceAwareRuleBeforeSending(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using ClientHost host = new();
		var customers = host.Resolve();

		// Act
		var registration = await customers.RegisterAsync(
			new()
			{
				Name = "Ann",
				Email = InMemoryCustomerEmailRegistry.SeededEmail,
				Age = 30,
			},
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(registration.Outcome).IsEqualTo(RegistrationOutcome.RejectedBeforeSending);
		var error = registration.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(RegisterCustomerRequest.EmailTakenCode);
		await Assert.That(error.FullPath).IsEqualTo(RegisterCustomerRequestSchema.Paths.Email);
		await Assert.That(host.Posts).IsEqualTo(0);
	}

	[Test]
	public async Task RegisterAsync_GivenEmailRegisteredByAnEarlierCall_RejectsTheSecondBeforeSending(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using ClientHost host = new();
		var customers = host.Resolve();
		RegisterCustomerRequest request = new()
		{
			Name = "Grace",
			Email = UniqueEmail(),
			Age = 85,
		};
		var first = await customers.RegisterAsync(request, cancellationToken: cancellationToken);

		// Act
		var second = await customers.RegisterAsync(request, cancellationToken: cancellationToken);

		// Assert
		await Assert.That(first.Outcome).IsEqualTo(RegistrationOutcome.Registered);
		await Assert.That(second.Outcome).IsEqualTo(RegistrationOutcome.RejectedBeforeSending);
		await Assert.That(second.Errors.Single().Code).IsEqualTo(RegisterCustomerRequest.EmailTakenCode);
		await Assert.That(host.Posts).IsEqualTo(1);
	}

	[Test]
	public async Task RegisterAsync_GivenValidateBeforeSendingFalse_ReturnsTheServersErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using ClientHost host = new();
		var customers = host.Resolve();

		// Act
		var registration = await customers.RegisterAsync(
			new()
			{
				Name = "A",
				Email = "not-an-email",
				Age = 5,
			},
			validateBeforeSending: false,
			cancellationToken
		);

		// Assert
		await Assert.That(registration.Outcome).IsEqualTo(RegistrationOutcome.RejectedByServer);
		await Assert.That(registration.Status).IsEqualTo(400);
		await Assert
			.That(registration.Errors.Select(static error => (error.FullPath.ToDottedString(), error.Code)))
			.Contains(("Email", "invalid_string"));
		await Assert
			.That(registration.Errors.Select(static error => error.FullPath.ToDottedString()).Distinct())
			.IsEquivalentTo(["Name", "Email", "Age"]);
		await Assert.That(host.Posts).IsEqualTo(1);
	}

	[Test]
	public async Task GetRegisterServer_GivenInteractiveServerPage_PrerendersTheSharedForm(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var document = await SampleAppFactory.GetDocumentAsync(client, "/register-server", cancellationToken);

		// Assert
		await Assert.That(document.QuerySelector("h1")?.TextContent).IsEqualTo("Register a customer (server)");
		await Assert.That(document.QuerySelector("#name")).IsNotNull();
		await Assert.That(document.QuerySelector("#email")).IsNotNull();
		await Assert.That(document.QuerySelector("#age")).IsNotNull();
		await Assert.That(document.QuerySelector("#post-unvalidated")).IsNotNull();
	}

	static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";

	/// <summary>
	/// The sample with its <see cref="CustomerAPIClient"/> registration kept as it is, except that the typed client's
	/// primary handler is the in-memory server's and it gets that server's base address - an in-memory server has no
	/// listening address to discover. Counts the requests the client actually sends to the API.
	/// </summary>
	sealed class ClientHost : IAsyncDisposable
	{
		readonly SampleAppFactory _factory = new();
		readonly WebApplicationFactory<App> _configured;
		readonly AsyncServiceScope _scope;
		int _posts;

		public ClientHost()
		{
			_configured = _factory.WithWebHostBuilder(builder =>
				builder.ConfigureTestServices(services =>
					services
						.AddHttpClient<CustomerAPIClient>()
						.ConfigureHttpClient(static http => http.BaseAddress = new("http://localhost/"))
						.ConfigurePrimaryHttpMessageHandler(() =>
							new CountingHandler(this, _configured!.Server.CreateHandler())
						)
				)
			);
			_scope = _configured.Services.CreateAsyncScope();
		}

		public int Posts => _posts;

		public CustomerAPIClient Resolve() => _scope.ServiceProvider.GetRequiredService<CustomerAPIClient>();

		public async ValueTask DisposeAsync()
		{
			await _scope.DisposeAsync();
			await _configured.DisposeAsync();
			await _factory.DisposeAsync();
		}

		sealed class CountingHandler(ClientHost host, HttpMessageHandler inner) : DelegatingHandler(inner)
		{
			protected override Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request,
				CancellationToken cancellationToken
			)
			{
				Interlocked.Increment(ref host._posts);
				return base.SendAsync(request, cancellationToken);
			}
		}
	}
}

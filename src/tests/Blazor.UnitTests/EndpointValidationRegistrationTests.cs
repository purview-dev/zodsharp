using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Blazor.Fixtures;

namespace ZodSharp.Blazor;

/// <summary>
/// <c>AddZodSharpBlazor()</c> registers the Microsoft.Extensions.Validation bridge for form models only. The platform's
/// endpoint filter validates every parameter a registered resolver claims, so the bridge must not claim parameters on
/// its own account: a Blazor Server host that also maps minimal APIs keeps its endpoints as they were unless it asks
/// for endpoint validation (<c>AddZodSharpValidation()</c> or <c>ValidateParameters</c>).
/// </summary>
public class EndpointValidationRegistrationTests
{
	const string InvalidCustomer = /*lang=json,strict*/
		"""{ "email": "not-an-email" }""";

	[Test]
	public async Task AddZodSharpBlazor_GivenAnEndpointBindingAZodSchemaType_LeavesTheEndpointUnvalidated(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(static services => services.AddZodSharpBlazor(), cancellationToken);

		// Act
		using var response = await PostAsync(app, cancellationToken);

		// Assert - the handler ran.
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task AddZodSharpBlazor_GivenAddZodSharpValidationToo_ValidatesTheEndpointInEitherOrder(
		bool blazorFirst,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(
			services =>
			{
				if (blazorFirst)
				{
					services.AddZodSharpBlazor();
					services.AddZodSharpValidation();
				}
				else
				{
					services.AddZodSharpValidation();
					services.AddZodSharpBlazor();
				}
			},
			cancellationToken
		);

		// Act
		using var response = await PostAsync(app, cancellationToken);

		// Assert - answered by the validation filter before the handler.
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
	}

	[Test]
	public async Task AddZodSharpBlazor_GivenValidateParametersTrue_ValidatesTheEndpoint(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await StartAsync(
			static services => services.AddZodSharpBlazor(static options => options.ValidateParameters = true),
			cancellationToken
		);

		// Act
		using var response = await PostAsync(app, cancellationToken);

		// Assert
		await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
	}

	static async Task<WebApplication> StartAsync(
		Action<IServiceCollection> register,
		CancellationToken cancellationToken
	)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddZodSharpFactory(static factory =>
			Generated.PurviewZodSharpBlazorUnitTestsZodSharpSchemas.Register(factory)
		);
		register(builder.Services);

		var app = builder.Build();
		app.MapPost("/customers", static (GeneratedCustomer customer) => Results.Ok(customer.Email));
		await app.StartAsync(cancellationToken);
		return app;
	}

	static async Task<HttpResponseMessage> PostAsync(WebApplication app, CancellationToken cancellationToken)
	{
		using StringContent content = new(InvalidCustomer, Encoding.UTF8, "application/json");
		return await app.GetTestClient().PostAsync(new Uri("/customers", UriKind.Relative), content, cancellationToken);
	}
}

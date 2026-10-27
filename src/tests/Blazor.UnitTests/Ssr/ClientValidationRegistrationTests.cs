using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ZodSharp.AspNetCore.ClientValidation;
using ZodSharp.Blazor.Fixtures;

namespace ZodSharp.Blazor.Ssr;

/// <summary>
/// <c>AddZodSharpClientValidation</c>'s defences against the <c>AddRazorComponents</c> ordering hazard.
/// </summary>
public class ClientValidationRegistrationTests
{
	[Test]
	public async Task AddZodSharpClientValidation_GivenCalledBeforeAddRazorComponents_DecoratesTheDefaultProvider()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		var services = builder.Services;

		// Act
		services.AddZodSharpClientValidation();
		services.AddRazorComponents();

		// Assert
		await AssertDecoratesDefaultAsync(builder);
	}

	[Test]
	public async Task AddZodSharpClientValidation_GivenCalledAfterAddRazorComponents_DecoratesTheDefaultProvider()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		var services = builder.Services;

		// Act
		services.AddRazorComponents();
		services.AddZodSharpClientValidation();

		// Assert
		await AssertDecoratesDefaultAsync(builder);
	}

	[Test]
	public async Task AddZodSharpClientValidation_GivenCalledTwice_DecoratesOnce()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		var services = builder.Services;
		services.AddRazorComponents();

		// Act
		services.AddZodSharpClientValidation();
		services.AddZodSharpClientValidation();

		// Assert
		await Assert.That(services.Count(static d => d.ServiceType == typeof(ClientValidationProvider))).IsEqualTo(1);
		await AssertDecoratesDefaultAsync(builder);
	}

	[Test]
	public async Task AddZodSharpClientValidation_GivenCustomProviderRegisteredFirst_DecoratesIt()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		var services = builder.Services;
		services.AddScoped<ClientValidationProvider, EmptyProvider>();

		// Act
		services.AddZodSharpClientValidation();
		services.AddRazorComponents();

		// Assert
		await using var app = builder.Build();
		await using var scope = app.Services.CreateAsyncScope();
		var resolved = scope.ServiceProvider.GetRequiredService<ClientValidationProvider>();
		await Assert.That(resolved).IsTypeOf<ZodClientValidationProvider>();
		await Assert.That(((ZodClientValidationProvider)resolved).Inner).IsTypeOf<EmptyProvider>();
	}

	[Test]
	public async Task Startup_GivenProviderReplacedAfterRegistration_WarnsInDevelopment(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using CapturingLoggerProvider logs = new();

		// Act
		await using var host = await StaticSsrHost.StartAsync(
			static services =>
			{
				services.AddRazorComponents();
				services.AddZodSharpClientValidation();
				services.Replace(ServiceDescriptor.Scoped<ClientValidationProvider, EmptyProvider>());
			},
			logs,
			cancellationToken
		);

		// Assert
		var warning = logs
			.Entries.Where(static e =>
				e.Category.EndsWith("ZodClientValidationStartupFilter", StringComparison.Ordinal)
			)
			.ToArray();
		await Assert.That(warning.Length).IsEqualTo(1);
		await Assert.That(warning.Single().Level).IsEqualTo(LogLevel.Warning);
		await Assert.That(warning.Single().Message).Contains(typeof(EmptyProvider).FullName!);
	}

	[Test]
	public async Task Startup_GivenProviderInPlace_DoesNotWarn(CancellationToken cancellationToken)
	{
		// Arrange
		using CapturingLoggerProvider logs = new();

		// Act
		await using var host = await StaticSsrHost.StartAsync(
			static services =>
			{
				services.AddZodSharpClientValidation();
				services.AddRazorComponents();
			},
			logs,
			cancellationToken
		);

		// Assert
		await Assert
			.That(
				logs.Entries.Where(static e =>
					e.Category.EndsWith("ZodClientValidationStartupFilter", StringComparison.Ordinal)
				)
			)
			.IsEmpty();
	}

	static async Task AssertDecoratesDefaultAsync(WebApplicationBuilder builder)
	{
		await using var app = builder.Build();
		await using var scope = app.Services.CreateAsyncScope();
		var resolved = scope.ServiceProvider.GetRequiredService<ClientValidationProvider>();

		await Assert.That(resolved).IsTypeOf<ZodClientValidationProvider>();
		await Assert
			.That(((ZodClientValidationProvider)resolved).Inner?.GetType().Name)
			.IsEqualTo("DataAnnotationsClientValidationProvider");
	}

	sealed class EmptyProvider : ClientValidationProvider
	{
		public override RenderFragment? RenderClientValidationRules(
			EditContext editContext,
			IReadOnlyDictionary<FieldIdentifier, string> renderedFields
		) => null;
	}
}

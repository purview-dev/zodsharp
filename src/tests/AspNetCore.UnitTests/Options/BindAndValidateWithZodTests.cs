using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;
#if NET11_0_OR_GREATER
using ZodSharp.Options;
#endif

namespace ZodSharp.AspNetCore.Options;

public class BindAndValidateWithZodTests
{
	public sealed class SmtpOptions
	{
		public List<SmtpServer> Servers { get; set; } = [];
	}

	public sealed class SmtpServer
	{
		public string? Host { get; set; }

		public int Port { get; set; }
	}

	static IEnumerable<ValidationError> Check(SmtpOptions options)
	{
		for (var i = 0; i < options.Servers.Count; i++)
		{
			if (string.IsNullOrEmpty(options.Servers[i].Host))
			{
				yield return ValidationError.Create(
					"required",
					"Host is required",
					ZodPath.From(ZodPathSegment.Member("Servers"), ZodPathSegment.At(i), ZodPathSegment.Member("Host"))
				);
			}
		}
	}

	static void Configure(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)
	{
		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Servers:0:Port"] = "25" });
		builder.Services.AddZodSharpFactory(static factory =>
			factory.Register(new DelegateValidator<SmtpOptions>(Check))
		);
	}

	[Test]
	public async Task BindAndValidateWithZod_GivenInvalidSection_FailsStartupWithConfigurationKeys(
		CancellationToken cancellationToken
	)
	{
		// Arrange / Act
		OptionsValidationException? exception = null;
		try
		{
			await using var app = await TestApp.StartAsync(
				builder =>
				{
					Configure(builder);
					builder.Services.AddOptions<SmtpOptions>().BindAndValidateWithZod("Smtp");
				},
				static _ => { },
				cancellationToken
			);
		}
		catch (OptionsValidationException ex)
		{
			exception = ex;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Failures).IsEquivalentTo(["Smtp:Servers:0:Host: Host is required (required)"]);
	}

	[Test]
	public async Task BindAndValidateWithZod_GivenValidSection_BindsTheOptions(CancellationToken cancellationToken)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			builder =>
			{
				Configure(builder);
				builder.Configuration.AddInMemoryCollection(
					new Dictionary<string, string?> { ["Smtp:Servers:0:Host"] = "mail.example.com" }
				);
				builder.Services.AddOptions<SmtpOptions>().BindAndValidateWithZod("Smtp");
			},
			static _ => { },
			cancellationToken
		);

		// Act
		var options = app.App.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;

		// Assert
		await Assert.That(options.Servers[0].Host).IsEqualTo("mail.example.com");
		await Assert.That(options.Servers[0].Port).IsEqualTo(25);
	}

	[Test]
	public async Task ValidateWithZod_GivenBindConfigurationAndInvalidSection_FailsStartupWithConfigurationKeys(
		CancellationToken cancellationToken
	)
	{
		// Arrange / Act
		OptionsValidationException? exception = null;
		try
		{
			await using var app = await TestApp.StartAsync(
				builder =>
				{
					Configure(builder);
					builder.Services.AddOptions<SmtpOptions>().BindConfiguration("Smtp").ValidateWithZod("Smtp");
				},
				static _ => { },
				cancellationToken
			);
		}
		catch (OptionsValidationException ex)
		{
			exception = ex;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Failures).IsEquivalentTo(["Smtp:Servers:0:Host: Host is required (required)"]);
	}

	[Test]
	public async Task ValidateWithZod_GivenValidSectionBoundByTheApplication_ExposesTheBoundOptions(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var app = await TestApp.StartAsync(
			builder =>
			{
				Configure(builder);
				builder.Configuration.AddInMemoryCollection(
					new Dictionary<string, string?> { ["Smtp:Servers:0:Host"] = "mail.example.com" }
				);
				builder.Services.AddOptions<SmtpOptions>().ValidateWithZod("Smtp").BindConfiguration("Smtp");
			},
			static _ => { },
			cancellationToken
		);

		// Act
		var options = app.App.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;

		// Assert — the call order does not matter: options are validated after every configuration step.
		await Assert.That(options.Servers[0].Host).IsEqualTo("mail.example.com");
		await Assert.That(options.Servers[0].Port).IsEqualTo(25);
	}

	[Test]
	public async Task ValidateWithZod_GivenNoBinding_DoesNotReadConfiguration(CancellationToken cancellationToken)
	{
		// Arrange — the section only names failures; without a bind the options keep their defaults.
		await using var app = await TestApp.StartAsync(
			builder =>
			{
				Configure(builder);
				builder.Services.AddOptions<SmtpOptions>().ValidateWithZod("Smtp");
			},
			static _ => { },
			cancellationToken
		);

		// Act
		var options = app.App.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;

		// Assert
		await Assert.That(options.Servers).IsEmpty();
	}

	[Test]
	public async Task ValidateWithZod_GivenBlankSection_Throws(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		ServiceCollection services = new();
		var builder = services.AddOptions<SmtpOptions>();

		// Act / Assert
		await Assert.That(() => builder.ValidateWithZod(" ")).Throws<ArgumentException>();
	}

#if NET11_0_OR_GREATER
	sealed class AsyncOnlyValidator : IZodSchemaValidator<SmtpOptions>
	{
		public bool RequiresAsyncValidation => true;

		public ValidationResult<SmtpOptions> Validate(SmtpOptions value) =>
			ValidationResult<SmtpOptions>.Success(value);

		public ValueTask<ValidationResult<SmtpOptions>> ValidateAsync(
			SmtpOptions value,
			CancellationToken cancellationToken = default
		) =>
			ValueTask.FromResult(
				ValidationResult<SmtpOptions>.Failure(
					ValidationError.Create("unreachable", "Server is unreachable", ZodPath.Member("Servers"))
				)
			);
	}

	[Test]
	public async Task HostStart_GivenAsyncOnlyRule_FailsWithTheAsyncError(CancellationToken cancellationToken)
	{
		// Arrange / Act
		OptionsValidationException? exception = null;
		try
		{
			await using var app = await TestApp.StartAsync(
				builder =>
				{
					builder.Services.AddZodSharpFactory(static factory => factory.Register(new AsyncOnlyValidator()));
					builder
						.Services.AddOptions<SmtpOptions>()
						.AddZodSchemaValidator(MissingValidatorBehavior.Throw, validateOnStart: true, "Smtp");
				},
				static _ => { },
				cancellationToken
			);
		}
		catch (OptionsValidationException ex)
		{
			exception = ex;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Failures).IsEquivalentTo(["Smtp:Servers: Server is unreachable (unreachable)"]);
	}
#endif
}

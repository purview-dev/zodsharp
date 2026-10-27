using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.Options;

public class ZodSchemaOptionsValidatorPathTests
{
	public sealed class SmtpOptions
	{
		public List<SmtpServer> Servers { get; set; } = [];
	}

	public sealed class SmtpServer
	{
		public string? Host { get; set; }
	}

	static ValidationError HostRequired =>
		ValidationError.Create(
			"required",
			"Host is required",
			ZodPath.From(ZodPathSegment.Member("Servers"), ZodPathSegment.At(0), ZodPathSegment.Member("Host"))
		);

	/// <summary>
	/// Reports <see cref="HostRequired"/> synchronously, and an extra error only from the service-aware
	/// asynchronous path, recording the services it was given.
	/// </summary>
	sealed class SmtpValidator(bool asyncOnlyError = false) : IZodSchemaValidator<SmtpOptions>
	{
		public IServiceProvider? AsyncServices { get; private set; }

		public int AsyncCalls { get; private set; }

		public bool RequiresAsyncValidation => asyncOnlyError;

		public ValidationResult<SmtpOptions> Validate(SmtpOptions value) =>
			value.Servers.Count > 0 && value.Servers[0].Host is null
				? ValidationResult<SmtpOptions>.Failure(HostRequired)
				: ValidationResult<SmtpOptions>.Success(value);

		public ValueTask<ValidationResult<SmtpOptions>> ValidateAsync(
			SmtpOptions value,
			CancellationToken cancellationToken = default
		) => ValidateAsync(value, services: null, cancellationToken);

		public ValueTask<ValidationResult<SmtpOptions>> ValidateAsync(
			SmtpOptions value,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		)
		{
			AsyncCalls++;
			AsyncServices = services;

			var result = Validate(value);
			if (!asyncOnlyError)
				return ValueTask.FromResult(result);

			var asyncError = ValidationError.Create("in_use", "Port is already in use", ZodPath.Member("Port"));
			return ValueTask.FromResult(
				ValidationResult<SmtpOptions>.Failure([.. result.IsSuccess ? [] : result.Errors, asyncError])
			);
		}
	}

	static SmtpOptions InvalidOptions() => new() { Servers = [new SmtpServer()] };

	static ServiceCollection CreateServices(IZodSchemaValidator<SmtpOptions> validator)
	{
		ZodSchemaFactory factory = new();
		factory.Register(validator);

		ServiceCollection services = new();
		services.AddSingleton<IZodSchemaFactory>(factory);
		return services;
	}

	[Test]
	public async Task Validate_GivenFailure_RendersPathMessageAndCode()
	{
		// Defect O1: failures carried only the message, losing the path and the code.

		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new SmtpValidator());
		ZodSchemaOptionsValidator<SmtpOptions> validator = new(factory);

		// Act
		var result = validator.Validate(Microsoft.Extensions.Options.Options.DefaultName, InvalidOptions());

		// Assert
		await Assert.That(result.Failures).IsEquivalentTo(["Servers[0].Host: Host is required (required)"]);
		await Assert.That(result).IsTypeOf<ZodValidateOptionsResult>();
		await Assert.That(((ZodValidateOptionsResult)result).Errors[0].Code).IsEqualTo("required");
	}

	[Test]
	public async Task OptionsAccess_GivenNamedOptions_PrefixesTheName()
	{
		// Arrange
		var services = CreateServices(new SmtpValidator());
		services
			.AddOptions<SmtpOptions>("primary")
			.Configure(static o => o.Servers.Add(new SmtpServer()))
			.AddZodSchemaValidator();
		await using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<SmtpOptions>>();

		// Act
		var exception = Catch(() => monitor.Get("primary"));

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert
			.That(exception!.Failures)
			.IsEquivalentTo(["primary: Servers[0].Host: Host is required (required)"]);
	}

	[Test]
	public async Task OptionsAccess_GivenNamedRegistration_DoesNotValidateOtherNames()
	{
		// Arrange
		var services = CreateServices(new SmtpValidator());
		services.AddOptions<SmtpOptions>("primary").AddZodSchemaValidator();
		services.AddOptions<SmtpOptions>("secondary").Configure(static o => o.Servers.Add(new SmtpServer()));
		await using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<SmtpOptions>>();

		// Act
		var exception = Catch(() => monitor.Get("secondary"));

		// Assert
		await Assert.That(exception).IsNull();
	}

	[Test]
	public async Task OptionsAccess_GivenConfigurationSection_UsesConfigurationKeys()
	{
		// Arrange
		var services = CreateServices(new SmtpValidator());
		services
			.AddOptions<SmtpOptions>()
			.Configure(static o => o.Servers.Add(new SmtpServer()))
			.AddZodSchemaValidator(
				MissingValidatorBehavior.Throw,
				validateOnStart: false,
				configurationSection: "Smtp"
			);
		await using var provider = services.BuildServiceProvider();

		// Act
		var exception = Catch(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Failures).IsEquivalentTo(["Smtp:Servers:0:Host: Host is required (required)"]);
	}

	[Test]
	public async Task ValidateAsync_RunsTheServiceAwarePathWithAScopedProvider()
	{
		// Arrange
		SmtpValidator schema = new(asyncOnlyError: true);
		var services = CreateServices(schema);
		await using var provider = services.BuildServiceProvider();
		ZodSchemaOptionsValidator<SmtpOptions> validator = new(
			provider.GetRequiredService<IZodSchemaFactory>(),
			provider
		);

		// Act
		var result = await validator.ValidateAsync(null, new SmtpOptions());

		// Assert
		await Assert.That(result.Failures).IsEquivalentTo(["Port: Port is already in use (in_use)"]);
		await Assert.That(schema.AsyncServices).IsNotNull();
		await Assert.That(schema.AsyncServices).IsNotSameReferenceAs(provider);
	}

	[Test]
	public async Task Validate_GivenAsyncOnlyRules_ChecksTheSynchronousRulesOnly()
	{
		// Arrange
		SmtpValidator schema = new(asyncOnlyError: true);
		ZodSchemaFactory factory = new();
		factory.Register(schema);
		ZodSchemaOptionsValidator<SmtpOptions> validator = new(factory);

		// Act
		var result = validator.Validate(null, new SmtpOptions());

		// Assert
		await Assert.That(result.Succeeded).IsTrue();
		await Assert.That(schema.AsyncCalls).IsEqualTo(0);
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task Validator_OnNet11_ImplementsAsyncValidateOptions()
	{
		// Arrange
		ZodSchemaOptionsValidator<SmtpOptions> validator = new(new ZodSchemaFactory());

		// Act
		var isAsync = validator is IAsyncValidateOptions<SmtpOptions>;

		// Assert
		await Assert.That(isAsync).IsTrue();
	}

	[Test]
	public async Task ValidateOnStart_GivenAsyncRule_FailsStartupValidationWithTheAsyncError(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var services = CreateServices(new SmtpValidator(asyncOnlyError: true));
		services.AddOptions<SmtpOptions>().AddZodSchemaValidator(MissingValidatorBehavior.Throw, validateOnStart: true);
		await using var provider = services.BuildServiceProvider();
		var startup = provider.GetRequiredService<IAsyncStartupValidator>();

		// Act
		OptionsValidationException? exception = null;
		try
		{
			await startup.ValidateAsync(cancellationToken);
		}
		catch (OptionsValidationException ex)
		{
			exception = ex;
		}

		// Assert
		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.Failures).Contains("Port: Port is already in use (in_use)");
	}

	[Test]
	public async Task OptionsAccess_GivenAsyncOnlyRule_ValidatesSynchronousRulesOnly()
	{
		// Arrange: the platform contract - synchronous access runs Validate, never ValidateAsync.
		SmtpValidator schema = new(asyncOnlyError: true);
		var services = CreateServices(schema);
		services.AddOptions<SmtpOptions>().AddZodSchemaValidator(MissingValidatorBehavior.Throw, validateOnStart: true);
		await using var provider = services.BuildServiceProvider();

		// Act
		var exception = Catch(() => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

		// Assert
		await Assert.That(exception).IsNull();
		await Assert.That(schema.AsyncCalls).IsEqualTo(0);
	}
#else
	[Test]
	public async Task Validator_BeforeNet11_ImplementsOnlyTheSynchronousInterface()
	{
		// Arrange
		var interfaces = typeof(ZodSchemaOptionsValidator<SmtpOptions>).GetInterfaces();

		// Act
		var names = interfaces.Select(static type => type.Name).ToArray();

		// Assert
		await Assert.That(names).IsEquivalentTo(["IValidateOptions`1"]);
	}
#endif

	static OptionsValidationException? Catch(Func<object> access)
	{
		try
		{
			_ = access();
			return null;
		}
		catch (OptionsValidationException ex)
		{
			return ex;
		}
	}
}

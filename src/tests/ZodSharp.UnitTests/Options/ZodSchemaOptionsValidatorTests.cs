using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.Core;

namespace ZodSharp.Options;

public class ZodSchemaOptionsValidatorTests
{
	sealed class TestOptions
	{
		public string? RoutePrefix { get; set; }
	}

	sealed class FakeValidator<T>(ValidationResult<T> result) : IZodSchemaValidator<T>
	{
		public ValidationResult<T> Validate(T value) => result;

		public ValueTask<ValidationResult<T>> ValidateAsync(T value, CancellationToken cancellationToken = default) =>
			ValueTask.FromResult(result);
	}

	[Test]
	public async Task Validate_GivenRegisteredValidatorWithFailures_ReturnsFailedWithMessages()
	{
		ZodSchemaFactory factory = new();
		var errors = new[] { ValidationError.Create("custom", "RoutePrefix must start with '/'.", ["RoutePrefix"]) };
		factory.Register(new FakeValidator<TestOptions>(ValidationResult<TestOptions>.Failure(errors)));

		ZodSchemaOptionsValidator<TestOptions> validator = new(factory);

		var result = validator.Validate(null, new TestOptions());

		await Assert.That(result.Failed).IsTrue();
		await Assert.That(result.FailureMessage).Contains("RoutePrefix must start with '/'.");
	}

	[Test]
	public async Task Validate_GivenRegisteredValidatorWithSuccess_ReturnsSucceeded()
	{
		ZodSchemaFactory factory = new();
		factory.Register(new FakeValidator<TestOptions>(ValidationResult<TestOptions>.Success(new TestOptions())));

		ZodSchemaOptionsValidator<TestOptions> validator = new(factory);

		var result = validator.Validate("named", new TestOptions());

		await Assert.That(result.Succeeded).IsTrue();
	}

	[Test]
	public async Task Validate_GivenUnregisteredType_ReturnsSucceeded()
	{
		ZodSchemaFactory factory = new();

		ZodSchemaOptionsValidator<TestOptions> validator = new(factory);

		var result = validator.Validate(null, new TestOptions());

		await Assert.That(result.Succeeded).IsTrue();
	}

	[Test]
	public async Task AddZodSchemaOptionsValidator_RegistersIValidateOptionsSingleton()
	{
		ServiceCollection services = new();
		ZodSchemaFactory factory = new();
		factory.Register(new FakeValidator<TestOptions>(ValidationResult<TestOptions>.Success(new TestOptions())));

		services.AddSingleton<IZodSchemaFactory>(factory);
		services.AddZodSchemaOptionsValidator<TestOptions>();

		await using var provider = services.BuildServiceProvider();
		var validator = provider.GetRequiredService<IValidateOptions<TestOptions>>();

		var result = validator.Validate(null, new TestOptions());

		await Assert.That(result.Succeeded).IsTrue();
	}
}

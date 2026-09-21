using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;

namespace ZodSharp.DependencyInjection;

public class AddZodSharpFactoryTests
{
	[Test]
	public async Task AddZodSharpFactory_CalledTwice_OnlyFirstConfigureIsApplied()
	{
		ServiceCollection services = new();
		services.AddZodSharpFactory(factory => factory.Register(new ZodSchemaValidator<string>(Z.String().Min(2))));
		services.AddZodSharpFactory(factory => factory.Register(new ZodSchemaValidator<double>(Z.Number().Min(2))));

		await using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		await Assert.That(factory.IsRegistered<string>()).IsTrue();
		await Assert.That(factory.IsRegistered<double>()).IsFalse();
	}

	[Test]
	public async Task AddZodSharpFactory_GivenExistingFactoryRegistration_DoesNotReplace()
	{
		ServiceCollection services = new();
		ZodSchemaFactory existing = new();
		existing.Register(new ZodSchemaValidator<string>(Z.String().Min(2)));

		services.AddSingleton<IZodSchemaFactory>(existing);
		services.AddZodSharpFactory(factory => factory.Register(new ZodSchemaValidator<double>(Z.Number().Min(2))));

		await using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		await Assert.That(factory.IsRegistered<string>()).IsTrue();
		await Assert.That(factory.IsRegistered<double>()).IsFalse();
	}

	[Test]
	public async Task AddZodSharpFactory_GivenExistingFactoryRegistration_ReturnsSameInstance()
	{
		ServiceCollection services = new();
		ZodSchemaFactory existing = new();

		services.AddSingleton<IZodSchemaFactory>(existing);
		services.AddZodSharpFactory();

		await using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		await Assert.That(ReferenceEquals(factory, existing)).IsTrue();
	}
}

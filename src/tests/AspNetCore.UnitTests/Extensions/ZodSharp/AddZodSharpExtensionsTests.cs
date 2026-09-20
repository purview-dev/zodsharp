using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp;

public class AddZodSharpExtensionsTests
{
	[Test]
	public async Task AddZodSharp_RegistersFactory_AndResolvesRegisteredValidator()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts =>
			opts.ConfigureFactory = static factory =>
				factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(2)))
		);
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		var result = factory.Validate("ok");
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task AddZodSharp_WithNullConfigure_RegistersEmptyFactory()
	{
		ServiceCollection services = new();
		services.AddZodSharp();
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		await Assert.That(factory.IsRegistered<string>()).IsFalse();
	}

	[Test]
	public async Task AddZodSharp_AutoRegistersGeneratedValidators_FromConfiguredAssemblies()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(AddZodSharpExtensionsTests).Assembly));
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
	}
}

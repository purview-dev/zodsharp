using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

public class GeneratedSchemaDITests
{
	[Test]
	public async Task AddZodSharp_WithAssemblyScan_RegistersGeneratedUserDtoValidator()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(UserDto).Assembly));
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		await Assert.That(factory.IsRegistered<UserDto>()).IsTrue();
		var ok = factory.Validate(new UserDto { Name = "A", Age = 1 });
		await Assert.That(ok.IsSuccess).IsTrue();
	}

	[Test]
	public async Task GeneratedValidator_ProducesCorrectValidationResult()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(UserDto).Assembly));
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		var result = factory.Validate(new UserDto { Name = "A", Age = 1 });

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsNotNull();
	}
}

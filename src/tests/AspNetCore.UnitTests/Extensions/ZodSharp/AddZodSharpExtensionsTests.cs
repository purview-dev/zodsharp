using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore.Fixtures;
using ZodSharp.Core;
using ZodSharp.Examples.CLI;
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

	[Test]
	public async Task AddZodSharpAssembly_RegistersGeneratedValidators_FromSpecifiedAssembly()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharp();
		services.AddZodSharpAssembly(typeof(AddZodSharpExtensionsTests).Assembly);

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharpAssembly_CalledMultipleTimes_RemainsAdditive()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharp();
		services.AddZodSharpAssembly(typeof(AddZodSharpExtensionsTests).Assembly);
		services.AddZodSharpAssembly(typeof(UserDto).Assembly);

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
		await Assert.That(factory.IsRegistered<UserDto>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharpAssembly_WorksWhenCalledBeforeAddZodSharp()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharpAssembly(typeof(AddZodSharpExtensionsTests).Assembly);
		services.AddZodSharp();

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharpAssemblyGraph_RegistersGeneratedValidators_FromReferencedAssemblies()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharp();
		services.AddZodSharpAssemblyGraph(typeof(AddZodSharpExtensionsTests).Assembly);

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<GraphScanUser>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharp_WithLoadedAssemblyScan_RegistersGeneratedValidators_FromLoadedAssemblies()
	{
		// Arrange
		var _ = typeof(GraphScanUser).Assembly;
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanLoadedAssemblies = true);

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<GraphScanUser>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharpLoadedAssemblies_RegistersGeneratedValidators_FromLoadedAssemblies()
	{
		// Arrange
		var _ = typeof(GraphScanUser).Assembly;
		ServiceCollection services = new();
		services.AddZodSharp();
		services.AddZodSharpLoadedAssemblies();

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<GraphScanUser>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharp_CalledTwice_OnlyFirstConfigurationIsApplied()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(AddZodSharpExtensionsTests).Assembly));
		services.AddZodSharp();
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharp_CalledTwice_OnlyFirstConfigureIsApplied_ButAssemblyContributionsRemainAdditive()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharp(static opts =>
			opts.ConfigureFactory = static factory =>
				factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(2)))
		);
		services.AddZodSharp(static opts =>
			opts.ConfigureFactory = static factory =>
				factory.Register(new ZodSchemaValidator<double>(Z.Number().Min(2)))
		);
		services.AddZodSharpAssembly(typeof(AddZodSharpExtensionsTests).Assembly);
		services.AddZodSharpAssembly(typeof(UserDto).Assembly);

		// Act
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Assert
		await Assert.That(factory.IsRegistered<string>()).IsTrue();
		await Assert.That(factory.IsRegistered<double>()).IsFalse();
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsTrue();
		await Assert.That(factory.IsRegistered<UserDto>()).IsTrue();
	}

	[Test]
	public async Task AddZodSharp_AfterAddZodSharpFactory_DoesNotReplaceFactory()
	{
		ServiceCollection services = new();
		services.AddZodSharpFactory(static factory =>
			factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(2)))
		);
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(AddZodSharpExtensionsTests).Assembly));
		var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();
		await Assert.That(factory.IsRegistered<string>()).IsTrue();
		await Assert.That(factory.IsRegistered<SampleDiDto>()).IsFalse();
	}
}

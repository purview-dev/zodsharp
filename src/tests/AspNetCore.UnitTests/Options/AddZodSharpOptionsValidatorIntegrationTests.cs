using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZodSharp.AspNetCore.Fixtures;

namespace ZodSharp.AspNetCore.Options;

public class AddZodSharpOptionsValidatorIntegrationTests
{
	[Test]
	public async Task AddZodSchemaValidator_GivenFactoryPopulatedViaAddZodSharpAssembly_ValidatesOptions()
	{
		// Arrange
		ServiceCollection services = new();
		services.AddZodSharp();
		services.AddZodSharpAssembly(typeof(UserDto).Assembly);
		services.AddOptions<UserDto>().AddZodSchemaValidator();

		await using var provider = services.BuildServiceProvider();
		var validator = provider.GetRequiredService<IValidateOptions<UserDto>>();

		// Act
		var result = validator.Validate(null, new UserDto { Name = "A", Age = 1 });

		// Assert
		await Assert.That(result.Succeeded).IsTrue();
	}
}

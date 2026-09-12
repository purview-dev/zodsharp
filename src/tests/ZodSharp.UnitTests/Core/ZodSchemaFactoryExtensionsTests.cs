namespace ZodSharp.Core;

sealed class SampleDto
{
	public string? Name { get; set; }
}

sealed class SampleDtoSchemaValidator : IZodSchemaValidator<SampleDto>
{
	public ValidationResult<SampleDto> Validate(SampleDto value) => ValidationResult<SampleDto>.Success(value);

	public ValueTask<ValidationResult<SampleDto>> ValidateAsync(
		SampleDto value,
		CancellationToken cancellationToken = default
	) => new(Validate(value));
}

public class ZodSchemaFactoryExtensionsTests
{
	[Test]
	public async Task RegisterFromAssembly_ScansForZodSchemaGenerated_AndRegisters()
	{
		ZodSchemaFactory factory = new();
		factory.RegisterFromAssembly(typeof(ZodSchemaFactoryExtensionsTests).Assembly);
		await Assert.That(factory.IsRegistered<SampleDto>()).IsTrue();
		var result = factory.Validate(new SampleDto { Name = "x" });
		await Assert.That(result.IsSuccess).IsTrue();
	}
}

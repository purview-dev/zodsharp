using ZodSharp.Schemas;

namespace ZodSharp.Core;

public class ZodSchemaValidatorTests
{
	[Test]
	public async Task Adapter_Delegates_ToInnerSchema()
	{
		IZodSchema<string> inner = new ZodString().Min(3);
		ZodSchemaValidator<string> adapter = new(inner);
		var ok = adapter.Validate("hello");
		var bad = adapter.Validate("a");
		await Assert.That(ok.IsSuccess).IsTrue();
		await Assert.That(bad.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Adapter_ValidateAsync_Delegates_ToInnerSchema()
	{
		IZodSchema<string> inner = new ZodString().Min(3);
		ZodSchemaValidator<string> adapter = new(inner);
		var ok = await adapter.ValidateAsync("hello");
		await Assert.That(ok.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Adapter_ImplementsNonGenericMarker()
	{
		IZodSchemaValidator adapter = new ZodSchemaValidator<string>(new ZodString());
		await Assert.That(adapter).IsNotNull();
	}
}

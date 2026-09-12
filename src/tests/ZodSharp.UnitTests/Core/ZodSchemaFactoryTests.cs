using ZodSharp.Schemas;

namespace ZodSharp.Core;

public class ZodSchemaFactoryTests
{
	[Test]
	public async Task Register_And_Resolve_Roundtrips()
	{
		ZodSchemaFactory factory = new();
		factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(2)));
		var validator = factory.Resolve<string>();

		await Assert.That(validator).IsNotNull();

		var result = validator!.Validate("ok");
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ResolveRequired_UnregisteredType_Throws()
	{
		ZodSchemaFactory factory = new();
		await Assert.That(() => factory.ResolveRequired<int>()).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Resolve_UnregisteredType_ReturnsNull()
	{
		ZodSchemaFactory factory = new();

		var sut = factory.Resolve<int>();

		await Assert.That(sut).IsNull();
	}

	[Test]
	public async Task Validate_UnregisteredType_Throws()
	{
		ZodSchemaFactory factory = new();
		await Assert.That(() => factory.Validate(42)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task TryRegister_ReturnsFalse_OnDuplicate()
	{
		ZodSchemaFactory factory = new();
		ZodSchemaValidator<string> v = new(new ZodString());
		var first = factory.TryRegister(v);
		var second = factory.TryRegister(v);
		await Assert.That(first).IsTrue();
		await Assert.That(second).IsFalse();
	}

	[Test]
	public async Task Register_OverwritesExisting()
	{
		ZodSchemaFactory factory = new();
		factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(5)));
		factory.Register(new ZodSchemaValidator<string>(new ZodString().Min(1)));
		var result = factory.Validate("a");
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task IsRegistered_UnregisteredType_ReturnsFalse()
	{
		ZodSchemaFactory factory = new();
		await Assert.That(factory.IsRegistered<int>()).IsFalse();
	}

	[Test]
	public async Task IsRegistered_RegisteredType_ReturnsTrue()
	{
		ZodSchemaFactory factory = new();
		factory.Register(new ZodSchemaValidator<string>(new ZodString()));
		await Assert.That(factory.IsRegistered<string>()).IsTrue();
	}
}

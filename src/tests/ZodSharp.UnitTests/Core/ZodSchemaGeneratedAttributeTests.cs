using System.Reflection;

namespace ZodSharp.Core;

public class ZodSchemaGeneratedAttributeTests
{
	[Test]
	public async Task Attribute_TargetsModule_AndExposesTargetType()
	{
		ZodSchemaGeneratedAttribute attr = new(typeof(string));
		await Assert.That(attr.TargetType).IsEqualTo(typeof(string));
	}

	[Test]
	public async Task Attribute_AllowMultipleAndNotInherited()
	{
		var usage = typeof(ZodSchemaGeneratedAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
		await Assert.That(usage.AllowMultiple).IsTrue();
		await Assert.That(usage.Inherited).IsFalse();
	}

	[Test]
	public async Task Attribute_ValidOn_ModuleClassAssembly()
	{
		var usage = typeof(ZodSchemaGeneratedAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
		await Assert
			.That(usage.ValidOn)
			.IsEqualTo(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Assembly);
	}
}

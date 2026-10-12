using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task ComplexType_Runtime_ValidatesNestedSchema(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Parent
	{
		public Child Child { get; set; } = new();
	}

	[ZodSchema]
	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var parentType = assembly.GetType("Testing.Parent")!;
		var schemaType = assembly.GetType("Testing.ParentSchema")!;

		var instance = Activator.CreateInstance(parentType)!;
		var child = Activator.CreateInstance(assembly.GetType("Testing.Child")!);
		child!.GetType().GetProperty("Name")!.SetValue(child, "A");
		parentType.GetProperty("Child")!.SetValue(instance, child);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		var isSuccess = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;

		await Assert.That(isSuccess).IsFalse();
	}

	[Test]
	public async Task ComplexType_Runtime_MultiLevelValidation_MergesPaths(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class GrandParent
	{
		public Parent Parent { get; set; } = new();
	}

	[ZodSchema]
	public class Parent
	{
		public Child Child { get; set; } = new();
	}

	[ZodSchema]
	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var grandParentType = assembly.GetType("Testing.GrandParent")!;
		var schemaType = assembly.GetType("Testing.GrandParentSchema")!;

		var instance = Activator.CreateInstance(grandParentType)!;
		var parent = Activator.CreateInstance(assembly.GetType("Testing.Parent")!);
		var child = Activator.CreateInstance(assembly.GetType("Testing.Child")!);
		child!.GetType().GetProperty("Name")!.SetValue(child, "A");
		parent!.GetType().GetProperty("Child")!.SetValue(parent, child);
		grandParentType.GetProperty("Parent")!.SetValue(instance, parent);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		dynamic dynResult = result;
		var isSuccess = (bool)dynResult.IsSuccess;
		var errors = (System.Collections.Immutable.ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);

		var error = errors[0];
		await Assert.That(error.Path.Length).IsEqualTo(3);
		await Assert.That(error.Path[0]).IsEqualTo("Parent");
		await Assert.That(error.Path[1]).IsEqualTo("Child");
		await Assert.That(error.Path[2]).IsEqualTo("Name");
	}

	[Test]
	public async Task ComplexType_Runtime_MultiLevelValidationWithoutZodSchema_MergesPaths(
		CancellationToken cancellationToken
	)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public class GrandParent
	{
		public Parent Parent { get; set; } = new();
	}

	public class Parent
	{
		public Child Child { get; set; } = new();
	}

	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var grandParentType = assembly.GetType("Testing.GrandParent")!;
		var schemaType = assembly.GetType("Testing.GrandParentSchema")!;

		var instance = Activator.CreateInstance(grandParentType)!;
		var parent = Activator.CreateInstance(assembly.GetType("Testing.Parent")!);
		var child = Activator.CreateInstance(assembly.GetType("Testing.Child")!);
		child!.GetType().GetProperty("Name")!.SetValue(child, "A");
		parent!.GetType().GetProperty("Child")!.SetValue(parent, child);
		grandParentType.GetProperty("Parent")!.SetValue(instance, parent);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		dynamic dynResult = result;
		var isSuccess = (bool)dynResult.IsSuccess;
		var errors = (System.Collections.Immutable.ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);

		var error = errors[0];
		await Assert.That(error.Path.Length).IsEqualTo(3);
		await Assert.That(error.Path[0]).IsEqualTo("Parent");
		await Assert.That(error.Path[1]).IsEqualTo("Child");
		await Assert.That(error.Path[2]).IsEqualTo("Name");
	}

	[Test]
	public async Task ArrayElement_Runtime_ValidatesNestedSchema(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class ArrayContainer
	{
		public Child[] Items { get; set; } = [];
	}

	[ZodSchema]
	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var containerType = await Assert.That(assembly.GetType("Testing.ArrayContainer")).IsNotNull();
		var schemaType = await Assert.That(assembly.GetType("Testing.ArrayContainerSchema")).IsNotNull();
		var childType = await Assert.That(assembly.GetType("Testing.Child")).IsNotNull();

		var instance = await Assert.That(Activator.CreateInstance(containerType)).IsNotNull();
		var items = await Assert.That(Array.CreateInstance(childType, 1)).IsNotNull();
		var child = await Assert.That(Activator.CreateInstance(childType)).IsNotNull();

		childType.GetProperty("Name")!.SetValue(child, "A");
		items.SetValue(child, 0);
		containerType.GetProperty("Items")!.SetValue(instance, items);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		var isSuccess = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;

		await Assert.That(isSuccess).IsFalse();
	}

	[Test]
	public async Task ListElement_Runtime_ValidatesNestedSchemaWithIndexPath(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class ListContainer
	{
		public List<Child> Items { get; set; } = new();
	}

	[ZodSchema]
	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var containerType = assembly.GetType("Testing.ListContainer")!;
		var schemaType = assembly.GetType("Testing.ListContainerSchema")!;
		var childType = assembly.GetType("Testing.Child")!;

		var instance = Activator.CreateInstance(containerType)!;
		var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType))!;
		var child = Activator.CreateInstance(childType)!;
		childType.GetProperty("Name")!.SetValue(child, "A");
		list.Add(child);
		containerType.GetProperty("Items")!.SetValue(instance, list);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		dynamic dynResult = result;
		var isSuccess = (bool)dynResult.IsSuccess;
		var errors = (System.Collections.Immutable.ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);

		var error = errors[0];
		await Assert.That(error.Path.Length).IsEqualTo(3);
		await Assert.That(error.Path[0]).IsEqualTo("Items");
		await Assert.That(error.Path[1]).IsEqualTo("0");
		await Assert.That(error.Path[2]).IsEqualTo("Name");
	}

	[Test]
	public async Task ListElement_Runtime_SkipsNullItems(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class NullableListContainer
	{
		public List<Child?> Items { get; set; } = new();
	}

	[ZodSchema]
	public class Child
	{
		[Required]
		[StringLength(10, MinimumLength = 2)]
		public string Name { get; set; } = string.Empty;
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var containerType = assembly.GetType("Testing.NullableListContainer")!;
		var schemaType = assembly.GetType("Testing.NullableListContainerSchema")!;
		var childType = assembly.GetType("Testing.Child")!;

		var instance = Activator.CreateInstance(containerType)!;
		var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType))!;
		list.Add(null);
		var child = Activator.CreateInstance(childType)!;
		childType.GetProperty("Name")!.SetValue(child, "A");
		list.Add(child);
		containerType.GetProperty("Items")!.SetValue(instance, list);

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		dynamic dynResult = result;
		var isSuccess = (bool)dynResult.IsSuccess;
		var errors = (System.Collections.Immutable.ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Path[0]).IsEqualTo("Items");
		await Assert.That(errors[0].Path[1]).IsEqualTo("1");
	}
}

using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string ChildElementSource =
		@"
	[ZodSchema]
	public class Child
	{
		public string Name { get; set; } = ""valid"";
	}";

	[Test]
	public async Task CollectionNullItems_GivenNonNullableElementAndNullItem_ReportsInvalidTypeAtElementPath(
		CancellationToken cancellationToken
	)
	{
		// Arrange — Child[] rejects null elements by default.
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, errors) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert — the error sits at the element's own path, matching Zod's per-element issue path.
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_type");
		await Assert.That(errors[0].Path.Length).IsEqualTo(2);
		await Assert.That(errors[0].Path[0]).IsEqualTo("Items");
		await Assert.That(errors[0].Path[1]).IsEqualTo("0");
	}

	[Test]
	public async Task CollectionNullItems_GivenStringArrayAndNullItem_ReportsInvalidType(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a collection with no nested schema is still null-checked.
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		public string[] Items { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, errors) = ValidateWithNullElement(
			assembly,
			typeof(string),
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_type");
		await Assert.That(errors[0].Path[0]).IsEqualTo("Items");
		await Assert.That(errors[0].Path[1]).IsEqualTo("0");
	}

	[Test]
	public async Task CollectionNullItems_GivenAllowNullsAttribute_SkipsNullItem(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		[CollectionBehavior(AllowNulls = true)]
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_GivenNullableElementAndRejectAttribute_ReportsInvalidType(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the explicit attribute overrides the element type's T? opt-in.
		var source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		[CollectionBehavior(AllowNulls = false)]
		public List<Child?> Items { get; set; } = new();
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, errors) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: false
		);

		// Assert
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_type");
	}

	[Test]
	public async Task CollectionNullItems_GivenNullableElementAndNoAttribute_SkipsNullItem(
		CancellationToken cancellationToken
	)
	{
		// Arrange — T? is an explicit opt-in, so the default rejection does not apply.
		var source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		public List<Child?> Items { get; set; } = new();
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: false
		);

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_GivenZodSchemaAllowNullsTrue_SkipsNullItem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema(AllowNulls = true)]
	public class Container
	{
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_GivenAssemblyDefaultsAllowNullsTrue_SkipsNullItem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			@"
using ZodSharp;

[assembly: ZodSharpSchemaDefaults(AllowNulls = true)]

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_GivenMsBuildAllowNullsTrue_SkipsNullItem(CancellationToken cancellationToken)
	{
		// Arrange
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property.ZodSharpAllowNulls",
				"true"
			),
		};
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Container
	{
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(source, options, cancellationToken);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_GivenPropertyRejectOverTypeAllow_ReportsInvalidType(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the property attribute is the most specific statement and wins.
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema(AllowNulls = true)]
	public class Container
	{
		[CollectionBehavior(AllowNulls = false)]
		public Child[] Items { get; set; } = [];
	}

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, errors) = ValidateWithNullElement(
			assembly,
			assembly.GetType("Testing.Child")!,
			"Container",
			"Items",
			isArray: true
		);

		// Assert
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
	}

	[Test]
	public async Task CollectionNullItems_GivenPositionalRecordParameterAttribute_SkipsNullItem(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the attribute on a positional parameter merges onto the synthesized property.
		var source =
			@"
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public readonly partial record struct Container([CollectionBehavior(AllowNulls = true)] Child[] Items);

"
			+ ChildElementSource
			+ @"
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var containerType = assembly.GetType("Testing.Container")!;
		var schemaType = assembly.GetType("Testing.ContainerSchema")!;
		var childType = assembly.GetType("Testing.Child")!;
		var array = Array.CreateInstance(childType, 1);
		var instance = Activator.CreateInstance(containerType, [array])!;

		// Act
		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		dynamic dynResult = result;
		var isSuccess = (bool)dynResult.IsSuccess;

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_JsonSchema_GivenAllowNulls_ExportsNullItemBranch(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema(AllowNulls = true)]
	public class Widget
	{
		public string[] Items { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert
		var items = definition.Properties!["items"].Items!;
		await Assert.That(items.AnyOf!).Count().IsEqualTo(2);
		await Assert.That(items.AnyOf!.Any(static branch => branch.Type == "null")).IsTrue();
	}

	[Test]
	public async Task CollectionNullItems_JsonSchema_GivenRejectedNulls_ExportsNoNullItemBranch(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		public string[] Items { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert
		await Assert.That(definition.Properties!["items"].Items!.AnyOf).IsNull();
	}

	/// <summary>
	/// Builds an instance of <paramref name="containerTypeName"/> whose collection property holds a single
	/// null element, validates it, and returns the result.
	/// </summary>
	static (bool IsSuccess, ImmutableArray<ValidationError> Errors) ValidateWithNullElement(
		System.Reflection.Assembly assembly,
		Type elementType,
		string containerTypeName,
		string propertyName,
		bool isArray
	)
	{
		var containerType = assembly.GetType($"Testing.{containerTypeName}")!;
		var schemaType = assembly.GetType($"Testing.{containerTypeName}Schema")!;
		var instance = Activator.CreateInstance(containerType)!;

		object collection;
		if (isArray)
		{
			var array = Array.CreateInstance(elementType, 1);
			array.SetValue(null, 0);
			collection = array;
		}
		else
		{
			var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
			list.Add(null);
			collection = list;
		}

		containerType.GetProperty(propertyName)!.SetValue(instance, collection);

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		dynamic dynResult = result;
		return ((bool)dynResult.IsSuccess, (ImmutableArray<ValidationError>)dynResult.Errors);
	}
}

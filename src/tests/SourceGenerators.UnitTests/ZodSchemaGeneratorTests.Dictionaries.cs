using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Dictionaries_GivenReferenceValueType_GeneratesCompilableValidator(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the shape that used to emit a null test over a KeyValuePair (CS0037).
		const string source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class VariantConfiguration
	{
		[Required(AllowEmptyStrings = false)]
		public string ResourceName { get; set; } = string.Empty;
	}

	[ZodSchema]
	public class WebAppKitOptions
	{
		[Required]
		public Dictionary<string, VariantConfiguration> Variants { get; set; } = [];
	}
}";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert — the generated validator compiles, and it iterates the entries by value and key.
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		await Assert.That(assembly).IsNotNull();

		var generated = driverResult.GetSource("WebAppKitOptionsSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).Contains("variantsEntry.Value");
		await Assert.That(generated!).Contains("variantsEntry.Key.ToString()");
	}

	[Test]
	public async Task Dictionaries_GivenValueTypeValues_GeneratesNoNullTestForTheEntry(
		CancellationToken cancellationToken
	)
	{
		// Arrange — KeyValuePair<string, int> cannot be null, so no null test is emitted for the entry.
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		public Dictionary<string, int> Counts { get; set; } = [];
	}
}";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		await Assert.That(assembly).IsNotNull();

		var generated = driverResult.GetSource("WidgetSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).DoesNotContain("countsEntry is null");
	}

	[Test]
	public async Task Dictionaries_GivenNestedValueSchema_ValidatesValuesAtKeyPath(CancellationToken cancellationToken)
	{
		// Arrange — the dictionary's value type has a schema, discovered through the map value.
		const string source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		[MinLength(3)]
		public string Name { get; set; } = string.Empty;
	}

	[ZodSchema]
	public class Container
	{
		public Dictionary<string, Child> Items { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var generated = driverResult.GetSource("ContainerSchema");
		await Assert.That(generated).IsNotNull();
		await Assert.That(generated!).ContainsGeneratedCode("ChildSchema.Validate(");

		// Act
		var (isSuccess, errors) = ValidateDictionary(assembly!, "Container", "Items", ("a", "Name", "x"));

		// Assert — the nested error is reported at the entry's key, matching z.record's issue path.
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Path.Length).IsEqualTo(3);
		await Assert.That(errors[0].Path[0]).IsEqualTo("Items");
		await Assert.That(errors[0].Path[1]).IsEqualTo("a");
		await Assert.That(errors[0].Path[2]).IsEqualTo("Name");
	}

	[Test]
	public async Task Dictionaries_GivenNullValue_ReportsInvalidTypeAtKeyPath(CancellationToken cancellationToken)
	{
		// Arrange — a non-nullable reference value type rejects a null value by default.
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		public string Name { get; set; } = ""valid"";
	}

	[ZodSchema]
	public class Container
	{
		public Dictionary<string, Child> Items { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, errors) = ValidateDictionary(assembly!, "Container", "Items", ("a", null, null));

		// Assert
		await Assert.That(isSuccess).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_type");
		await Assert.That(errors[0].Path[1]).IsEqualTo("a");
	}

	[Test]
	public async Task Dictionaries_GivenAllowNulls_SkipsNullValue(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		public string Name { get; set; } = ""valid"";
	}

	[ZodSchema]
	public class Container
	{
		[CollectionBehavior(AllowNulls = true)]
		public Dictionary<string, Child> Items { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		// Act
		var (isSuccess, _) = ValidateDictionary(assembly!, "Container", "Items", ("a", null, null));

		// Assert
		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task Dictionaries_JsonSchema_GivenNestedValue_ExportsObjectWithAdditionalProperties(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		public string Name { get; set; } = ""valid"";
	}

	[ZodSchema]
	public class Container
	{
		public Dictionary<string, Child> Items { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "ContainerSchema", cancellationToken);

		// Assert — a dictionary is a JSON object, not an array of pairs or of key strings.
		var items = definition.Properties!["items"];
		await Assert.That(items.Type).IsEqualTo("object");
		await Assert.That(items.Items).IsNull();

		var additionalProperties = items.AdditionalProperties as JsonSchemaDefinition;
		await Assert.That(additionalProperties).IsNotNull();
		await Assert.That(additionalProperties!.Type).IsEqualTo("object");
		await Assert.That(additionalProperties.Properties).ContainsKey("name");
	}

	[Test]
	public async Task Dictionaries_JsonSchema_GivenNullableValue_ExportsNullValueBranch(
		CancellationToken cancellationToken
	)
	{
		// Arrange — an explicitly nullable value opts the map into accepting null values.
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		public string Name { get; set; } = ""valid"";
	}

	[ZodSchema]
	public class Container
	{
		public Dictionary<string, Child?> Items { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "ContainerSchema", cancellationToken);

		// Assert
		var additionalProperties = definition.Properties!["items"].AdditionalProperties as JsonSchemaDefinition;
		await Assert.That(additionalProperties).IsNotNull();
		await Assert.That(additionalProperties!.AnyOf).Count().IsEqualTo(2);
		await Assert.That(additionalProperties.AnyOf!.Any(static branch => branch.Type == "null")).IsTrue();
	}

	[Test]
	public async Task Dictionaries_JsonSchema_GivenLength_ExportsMinMaxProperties(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		[Length(1, 5)]
		public Dictionary<string, int> Counts { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert — member counts use the object keywords, not the array ones.
		var counts = definition.Properties!["counts"];
		await Assert.That(counts.Type).IsEqualTo("object");
		await Assert.That(counts.MinProperties).IsEqualTo(1);
		await Assert.That(counts.MaxProperties).IsEqualTo(5);
		await Assert.That(counts.MinItems).IsNull();
		await Assert.That(counts.MaxItems).IsNull();
	}

	[Test]
	public async Task Enumerables_GivenEnumerableProperty_ValidatesElements(CancellationToken cancellationToken)
	{
		// Arrange — a property declared as the enumerable interface itself still resolves its element type.
		const string source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;

namespace Testing
{
	[ZodSchema]
	public class Child
	{
		[MinLength(3)]
		public string Name { get; set; } = string.Empty;
	}

	[ZodSchema]
	public class Container
	{
		public IEnumerable<Child> Items { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var containerType = assembly!.GetType("Testing.Container")!;
		var childType = assembly.GetType("Testing.Child")!;
		var child = Activator.CreateInstance(childType)!;
		childType.GetProperty("Name")!.SetValue(child, "x");

		var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType))!;
		list.Add(child);

		var instance = Activator.CreateInstance(containerType)!;
		containerType.GetProperty("Items")!.SetValue(instance, list);

		// Act
		var result = assembly.GetType("Testing.ContainerSchema")!.GetMethod("Validate")!.Invoke(null, [instance])!;
		dynamic dynResult = result;

		// Assert
		await Assert.That((bool)dynResult.IsSuccess).IsFalse();
		var errors = (ImmutableArray<ValidationError>)dynResult.Errors;
		await Assert.That(errors[0].Path[1]).IsEqualTo("0");
	}

	/// <summary>
	/// Builds a container whose dictionary property holds the supplied entries and validates it.
	/// </summary>
	static (bool IsSuccess, ImmutableArray<ValidationError> Errors) ValidateDictionary(
		System.Reflection.Assembly assembly,
		string containerTypeName,
		string propertyName,
		params (string Key, string? Property, object? Value)[] entries
	)
	{
		var containerType = assembly.GetType($"Testing.{containerTypeName}")!;
		var propertyType = containerType.GetProperty(propertyName)!.PropertyType;
		var valueType = propertyType.GetGenericArguments()[1];
		var dictionary = (System.Collections.IDictionary)
			Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType))!;

		foreach (var (key, property, assignedValue) in entries)
		{
			object? value = null;
			if (property is not null)
			{
				value = Activator.CreateInstance(valueType)!;
				valueType.GetProperty(property)!.SetValue(value, assignedValue);
			}

			dictionary[key] = value;
		}

		var instance = Activator.CreateInstance(containerType)!;
		containerType.GetProperty(propertyName)!.SetValue(instance, dictionary);

		var result = assembly
			.GetType($"Testing.{containerTypeName}Schema")!
			.GetMethod("Validate")!
			.Invoke(null, [instance])!;
		dynamic dynResult = result;
		return ((bool)dynResult.IsSuccess, (ImmutableArray<ValidationError>)dynResult.Errors);
	}
}

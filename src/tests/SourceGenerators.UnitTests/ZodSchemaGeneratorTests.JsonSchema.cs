using System.Collections.Immutable;
using ZodSharp.JsonSchema;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task JsonSchema_GivenZodSchema_EmitsBuildJsonSchemaMethod(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        public string Name { get; set; } = """";
    }
}";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WidgetSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("BuildJsonSchema");
		await Assert.That(generated).ContainsGeneratedCode("JsonSchemaDefinition");
	}

	[Test]
	public async Task JsonSchema_GivenZodSchema_ValidatorAdapterImplementsTheProvider(
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
        public string Name { get; set; } = """";
    }
}";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WidgetSchema");

		// Assert — the adapter is what Z.ToJsonSchema receives, so it must forward to the schema.
		await Assert.That(generated).ContainsGeneratedCode("IJsonSchemaProvider");
		await Assert.That(generated).ContainsGeneratedCode("WidgetSchema.BuildJsonSchema()");
	}

	[Test]
	public async Task JsonSchema_GivenNonSentinelString_ExportsTheNonWhitespaceConstraint(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
using ZodSharp.Rules;

namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [NonSentinel]
        public string Name { get; set; } = """";
    }
}";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var schemaType = assembly.GetType("Testing.WidgetSchema")!;
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — a non-nullable string is required and its rules reject null, so the property schema is the
		// string shape directly (no null branch).
		await Assert.That(definition.Type).IsEqualTo("object");
		await Assert.That(definition.Properties).IsNotNull();
		var nameSchema = definition.Properties!["name"];
		await Assert.That(nameSchema.Type).IsEqualTo("string");
		await Assert.That(nameSchema.MinLength).IsEqualTo(1);
		await Assert.That(nameSchema.Pattern).IsEqualTo("\\S");
		await Assert.That(definition.Required!).Contains("name");
	}

	[Test]
	public async Task JsonSchema_GivenNestedSchema_ExportsTheNestedDefinition(CancellationToken cancellationToken)
	{ // Arrange
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Wrapper
    {
        public Inner Inner { get; set; } = new();
    }

    [ZodSchema]
    public class Inner
    {
        public bool Flag { get; set; }
    }
}";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var schemaType = assembly.GetType("Testing.WrapperSchema")!;
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert
		var innerSchema = definition.Properties!["inner"];
		await Assert.That(innerSchema.Type).IsEqualTo("object");
		await Assert.That(innerSchema.Properties!["flag"].Type).IsEqualTo("boolean");
	}

	[Test]
	public async Task JsonSchema_GivenGenerateJsonSchemaFalse_DoesNotEmitTheExportMembers(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
    [ZodSchema(GenerateJsonSchema = false)]
    public class Widget
    {
        public string Name { get; set; } = """";
    }
}";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WidgetSchema");

		// Assert — the validation members are still generated; only the export members are omitted.
		await Assert.That(generated).ContainsGeneratedCode("Validate");
		await Assert.That(generated).DoesNotContain("BuildJsonSchema");
		await Assert.That(generated).DoesNotContain("IJsonSchemaProvider");
	}

	[Test]
	public async Task JsonSchema_GivenNestedExportDisabled_FallsBackToAnEmptySchema(CancellationToken cancellationToken)
	{
		// Arrange — Inner opts out of JSON Schema, so Wrapper's exported schema cannot reference it.
		const string source =
			@"
namespace Testing
{
    [ZodSchema(GenerateJsonSchema = false)]
    public class Inner
    {
        public bool Flag { get; set; }
    }

    [ZodSchema]
    public class Wrapper
    {
        public Inner Inner { get; set; } = new();
    }
}";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var definition = (JsonSchemaDefinition)
			assembly.GetType("Testing.WrapperSchema")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — the property is an empty (any) schema rather than a reference to a missing method.
		var innerSchema = definition.Properties!["inner"];
		await Assert.That(innerSchema.Type).IsNull();
		await Assert.That(innerSchema.Properties).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenFlagsEnum_ExportsAnIntegerNotAnEnumeration(CancellationToken cancellationToken)
	{
		// Arrange — a [Flags] enum accepts combinations that are not defined members, so enumerating the
		// members would reject valid values.
		const string source =
			@"
using System;

namespace Testing
{
    [Flags]
    public enum Permissions
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    [ZodSchema]
    public class Widget
    {
        public Permissions Permissions { get; set; }
    }
}";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var definition = (JsonSchemaDefinition)
			assembly.GetType("Testing.WidgetSchema")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert
		var permissions = definition.Properties!["permissions"];
		await Assert.That(permissions.Type).IsEqualTo("integer");
		await Assert.That(permissions.Enum).IsNull();
	}

	[Test]
	public async Task JsonSchema_GivenNumericEnum_ExportsNumericValues(CancellationToken cancellationToken)
	{
		// Arrange — the System.Text.Json default serialises an enum as its numeric value.
		const string source = EnumSchemaSource;

		// Act
		var definition = await BuildEnumDefinitionAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum).IsNotNull();
		await Assert.That(status.Enum!).Contains(0);
		await Assert.That(status.Enum!).Contains(1);
		await Assert.That(status.Enum!).DoesNotContain("Active");
	}

	[Test]
	public async Task JsonSchema_GivenStringEnumConverterOnTheEnum_ExportsMemberNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the enum declares its converter, so System.Text.Json writes the member names.
		const string source =
			@"
using System.Text.Json.Serialization;

namespace Testing
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Status { Active = 0, Inactive = 1 }

    [ZodSchema]
    public class Widget
    {
        public Status Status { get; set; }
    }
}";

		// Act
		var definition = await BuildEnumDefinitionAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum).IsNotNull();
		await Assert.That(status.Enum!).Contains("Active");
		await Assert.That(status.Enum!).Contains("Inactive");
		await Assert.That(status.Enum!).DoesNotContain(0);
	}

	[Test]
	public async Task JsonSchema_GivenStringEnumConverterOnTheProperty_ExportsMemberNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the converter is on the property rather than the enum.
		const string source =
			@"
using System.Text.Json.Serialization;

namespace Testing
{
    public enum Status { Active = 0, Inactive = 1 }

    [ZodSchema]
    public class Widget
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Status Status { get; set; }
    }
}";

		// Act
		var definition = await BuildEnumDefinitionAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum!).Contains("Active");
		await Assert.That(status.Enum!).Contains("Inactive");
	}

	[Test]
	public async Task JsonSchema_GivenJsonStringEnumMemberName_ExportsTheCustomName(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using System.Text.Json.Serialization;

namespace Testing
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Status
    {
        [JsonStringEnumMemberName(""active"")] Active = 0,
        [JsonStringEnumMemberName(""inactive"")] Inactive = 1,
    }

    [ZodSchema]
    public class Widget
    {
        public Status Status { get; set; }
    }
}";

		// Act
		var definition = await BuildEnumDefinitionAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum!).Contains("active");
		await Assert.That(status.Enum!).Contains("inactive");
		await Assert.That(status.Enum!).DoesNotContain("Active");
	}

	[Test]
	public async Task JsonSchema_GivenAssemblyEnumSerializationString_ExportsMemberNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the assembly-level switch matches a global JsonStringEnumConverter.
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>.Empty.Add(
				"build_property:ZodSharpEnumSerialization",
				"String"
			),
		};

		// Act
		var definition = await BuildEnumDefinitionAsync(EnumSchemaSource, options, cancellationToken);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum!).Contains("Active");
		await Assert.That(status.Enum!).Contains("Inactive");
	}

	[Test]
	public async Task JsonSchema_GivenAssemblyEnumNamingPolicyCamelCase_ExportsCamelCaseNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ZodSourceGeneratorTestOptions options = new()
		{
			CompileToAssembly = true,
			AnalyzerConfigOptions = ImmutableDictionary<string, string>
				.Empty.Add("build_property:ZodSharpEnumSerialization", "String")
				.Add("build_property:ZodSharpEnumNamingPolicy", "CamelCase"),
		};

		// Act
		var definition = await BuildEnumDefinitionAsync(EnumSchemaSource, options, cancellationToken);

		// Assert
		var status = definition.Properties!["status"];
		await Assert.That(status.Enum!).Contains("active");
		await Assert.That(status.Enum!).Contains("inactive");
	}

	[Test]
	public async Task JsonSchema_GivenNonNullableAndNullableStrings_MatchesValidationRequiredness(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a non-nullable string is required and its rule rejects null; a nullable string keeps a
		// null branch.
		const string source =
			@"
using ZodSharp.Rules;

namespace Testing
{
    [ZodSchema]
    public sealed class Item
    {
        [NonSentinel]
        public string Name { get; set; } = """";

        [NullOrNonWhiteSpace]
        public string? Branch { get; set; }
    }
}";

		// Act
		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var schemaType = assembly.GetType("Testing.ItemSchema")!;
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — the non-nullable string is required with no null branch.
		await Assert.That(definition.Required!).Contains("name");
		var name = definition.Properties!["name"];
		await Assert.That(name.Type).IsEqualTo("string");
		await Assert.That(name.MinLength).IsEqualTo(1);
		await Assert.That(name.Pattern).IsEqualTo("\\S");
		await Assert.That(name.AnyOf).IsNull();

		// Assert — the nullable string keeps a null branch and is not required.
		await Assert.That(definition.Required!).DoesNotContain("branch");
		var branch = definition.Properties!["branch"].AnyOf!;
		await Assert.That(branch.Any(candidate => candidate.Type == "null")).IsTrue();
		var branchText = branch.Single(candidate => candidate.Type == "string");
		await Assert.That(branchText.MinLength).IsEqualTo(1);
		await Assert.That(branchText.Pattern).IsEqualTo("\\S");

		// Assert — runtime validation agrees with the schema for the same fixtures.
		var itemType = assembly.GetType("Testing.Item")!;
		var validate = schemaType.GetMethod("Validate")!;

		bool IsValid(string? name, string? branch)
		{
			var item = Activator.CreateInstance(itemType)!;
			itemType.GetProperty("Name")!.SetValue(item, name);
			itemType.GetProperty("Branch")!.SetValue(item, branch);
			var result = validate.Invoke(null, [item])!;
			return (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;
		}

		await Assert.That(IsValid(null, null)).IsFalse();
		await Assert.That(IsValid("", null)).IsFalse();
		await Assert.That(IsValid("   ", null)).IsFalse();
		await Assert.That(IsValid("name", null)).IsTrue();
		await Assert.That(IsValid("name", "   ")).IsFalse();
		await Assert.That(IsValid("name", "main")).IsTrue();
	}

	const string EnumSchemaSource =
		@"
namespace Testing
{
    public enum Status { Active = 0, Inactive = 1 }

    [ZodSchema]
    public class Widget
    {
        public Status Status { get; set; }
    }
}";

	async Task<JsonSchemaDefinition> BuildEnumDefinitionAsync(
		string source,
		ZodSourceGeneratorTestOptions options,
		CancellationToken cancellationToken
	)
	{
		var driverResult = await GenerateAsync(source, options, cancellationToken);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		return (JsonSchemaDefinition)
			assembly.GetType("Testing.WidgetSchema")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;
	}
}

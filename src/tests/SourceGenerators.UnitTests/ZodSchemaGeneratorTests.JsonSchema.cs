using System.Collections.Immutable;
using ZodSharp.JsonSchema;
using ZodSharp.Rules;
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
		using var driverResult = await GenerateAsync(source, cancellationToken);
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
		using var driverResult = await GenerateAsync(source, cancellationToken);
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
		using var driverResult = await GenerateAsync(
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
		using var driverResult = await GenerateAsync(
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
		using var driverResult = await GenerateAsync(source, cancellationToken);
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
		using var driverResult = await GenerateAsync(
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
		using var driverResult = await GenerateAsync(
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
				"build_property.ZodSharpEnumSerialization",
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
				.Empty.Add("build_property.ZodSharpEnumSerialization", "String")
				.Add("build_property.ZodSharpEnumNamingPolicy", "CamelCase"),
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
		using var driverResult = await GenerateAsync(
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

	[Test]
	public async Task JsonSchema_GivenGuidProperty_ExportsTheCanonicalPatternWithoutUuidFormat(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;

			namespace Testing
			{
				[ZodSchema]
				public class Widget
				{
					public Guid Id { get; set; }
				}
			}
			""";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert — a C# Guid accepts any 128-bit value, so it must not carry Zod's RFC UUID format.
		var id = definition.Properties!["id"];
		await Assert.That(id.Type).IsEqualTo("string");
		await Assert.That(id.Format).IsNull();
		await Assert.That(id.Pattern).IsEqualTo(GuidRule.JsonSchemaPattern);
	}

	[Test]
	public async Task JsonSchema_GivenUuidAttributes_ExportsFormatAndVersionPattern(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public class Widget
				{
					[UUID]
					public string Any { get; set; } = string.Empty;

					[UUID(UuidVersion.V4)]
					public string V4 { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert — the versionless rule is the uuid format alone; the versioned rule pins the version with a
		// pattern, since JSON Schema has no versioned uuid format.
		var any = definition.Properties!["any"];
		await Assert.That(any.Format).IsEqualTo("uuid");
		await Assert.That(any.Pattern).IsNull();

		var v4 = definition.Properties!["v4"];
		await Assert.That(v4.Format).IsEqualTo("uuid");
		await Assert.That(v4.Pattern).IsEqualTo(UUIDRule.JsonSchemaVersionPattern(UuidVersion.V4));
	}

	[Test]
	public async Task JsonSchema_GivenGuidScalarWithNonSentinel_ComposesBothPatternsThroughAllOf(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar]
					[ZodSchema]
					[NonSentinel]
					public partial record struct TenantId
						: Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
					{
						public Guid Value { get; init; }
					}
				}
				""";

		// Act
		var definition = await BuildDefinitionAsync(source, "TenantIdSchema", cancellationToken);

		// Assert — the canonical Guid pattern and the non-empty pattern are both preserved.
		await Assert.That(definition.Type).IsEqualTo("string");
		await Assert.That(definition.Format).IsNull();
		await Assert.That(definition.Pattern).IsNull();
		await Assert.That(definition.AllOf!).Count().IsEqualTo(2);
		await Assert.That(definition.AllOf!.Select(branch => branch.Pattern)).Contains(GuidRule.JsonSchemaPattern);
		await Assert
			.That(definition.AllOf!.Select(branch => branch.Pattern))
			.Contains(NonSentinelRule<Guid>.GuidJsonSchemaPattern);
	}

	[Test]
	public async Task JsonSchema_GivenGuidScalarWithUuidVersion_ExportsUuidFormatAndVersionPattern(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar]
					[ZodSchema]
					[UUID(UuidVersion.V7)]
					public partial record struct AssetId
						: Purview.ValueObjects.IScalarValueObject<AssetId, Guid>
					{
						public Guid Value { get; init; }
					}
				}
				""";

		// Act
		var definition = await BuildDefinitionAsync(source, "AssetIdSchema", cancellationToken);

		// Assert — a Guid-backed scalar can require a UUID version; the base Guid pattern and the version
		// pattern compose, so the version is not lost.
		await Assert.That(definition.Type).IsEqualTo("string");
		await Assert.That(definition.Format).IsEqualTo("uuid");
		await Assert.That(definition.AllOf).IsNotNull();
		await Assert.That(definition.AllOf!.Select(branch => branch.Pattern)).Contains(GuidRule.JsonSchemaPattern);
		await Assert
			.That(definition.AllOf!.Select(branch => branch.Pattern))
			.Contains(UUIDRule.JsonSchemaVersionPattern(UuidVersion.V7));
	}

	[Test]
	public async Task JsonSchema_GivenGuidScalarNestedInObjectAndArray_ExportsThePatternAtEachLevel(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar]
					[ZodSchema]
					[NonSentinel]
					public partial record struct TenantId
						: Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
					{
						public Guid Value { get; init; }
					}

					[ZodSchema]
					public class Holder
					{
						public Guid Plain { get; set; }

						public System.Collections.Generic.List<Guid> PlainGuids { get; set; } = new();

						public TenantId Tenant { get; set; }

						public System.Collections.Generic.List<TenantId> Tenants { get; set; } = new();
					}
				}
				""";

		// Act
		var definition = await BuildDefinitionAsync(source, "HolderSchema", cancellationToken);

		// Assert — a plain Guid and a Guid-backed scalar keep their constraint at the property, element and
		// nested-scalar level.
		await Assert.That(definition.Properties!["plain"].Pattern).IsEqualTo(GuidRule.JsonSchemaPattern);
		await Assert.That(definition.Properties!["plainGuids"].Items!.Pattern).IsEqualTo(GuidRule.JsonSchemaPattern);
		await Assert.That(definition.Properties!["tenant"].AllOf!).Count().IsEqualTo(2);
		await Assert.That(definition.Properties!["tenants"].Items!.AllOf!).Count().IsEqualTo(2);
	}

	async Task<JsonSchemaDefinition> BuildDefinitionAsync(
		string source,
		string schemaTypeName,
		CancellationToken cancellationToken
	)
	{
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		return (JsonSchemaDefinition)
			assembly.GetType($"Testing.{schemaTypeName}")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;
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
		// The emitted assembly is loaded into a collectible context; dispose it once the schema has been
		// built so the context unloads instead of accumulating for the lifetime of the test run.
		using var driverResult = await GenerateAsync(source, options, cancellationToken);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		return (JsonSchemaDefinition)
			assembly.GetType("Testing.WidgetSchema")!.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;
	}
}

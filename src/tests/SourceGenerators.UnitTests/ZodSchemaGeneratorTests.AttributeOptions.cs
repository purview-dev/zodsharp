namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task SchemaName_GivenCustomName_UsesItForSchemaAndAdapter(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema(SchemaName = "UserZod")]
				public class User
				{
					[Required]
					[StringLength(50, MinimumLength = 3)]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		var schema = driverResult.GetSource("UserZod.g.cs", HintNameMatchMode.Suffix);
		await Assert.That(schema).ContainsGeneratedCode("class UserZod");
		await Assert.That(schema).DoesNotContain("class UserSchema");
		await Assert.That(schema).ContainsGeneratedCode("class UserZodValidator");
	}

	[Test]
	public async Task GenerateValidateMethod_GivenFalse_DoesNotEmitValidateOrDependentMembers(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema(GenerateValidateMethod = false)]
				public class User
				{
					[Required]
					[StringLength(50, MinimumLength = 3)]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var schema = driverResult.GetSource("UserSchema.g.cs", HintNameMatchMode.Suffix);

		// Assert
		await Assert.That(schema).DoesNotContain("Validate(");
		await Assert.That(schema).DoesNotContain(" Parse(");
		await Assert.That(schema).DoesNotContain("ApplyAnd");
	}

	[Test]
	public async Task GenerateParseMethod_GivenFalse_StillEmitsValidate(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema(GenerateParseMethod = false)]
				public class User
				{
					[Required]
					[StringLength(50, MinimumLength = 3)]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var schema = driverResult.GetSource("UserSchema.g.cs", HintNameMatchMode.Suffix);

		// Assert
		await Assert.That(schema).ContainsGeneratedCode("ValidationResult<global::Testing.User> Validate(");
		await Assert.That(schema).DoesNotContain("global::Testing.User Parse(");
	}

	[Test]
	public async Task LengthValidation_GivenCollectionProperty_ReportsCollectionOrigin(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Basket
				{
					[Required]
					[Length(2, 5)]
					public List<string>? Items { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var schema = driverResult.GetSource("BasketSchema.g.cs", HintNameMatchMode.Suffix);

		// Assert
		await Assert.That(schema).ContainsGeneratedCode("origin: \"collection\"");
		await Assert.That(schema).DoesNotContain("origin: \"array\"");
	}

	[Test]
	public async Task LengthValidation_GivenArrayProperty_ReportsArrayOrigin(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Basket
				{
					[Required]
					[Length(2, 5)]
					public string[]? Items { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var schema = driverResult.GetSource("BasketSchema.g.cs", HintNameMatchMode.Suffix);

		// Assert
		await Assert.That(schema).ContainsGeneratedCode("origin: \"array\"");
	}
}

using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string EnumModel = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing
		{
			public enum ExampleEnum
			{
				Unspecified,

				AValidValue,

				AnotherValidValue
			}

			[ZodSchema]
			public class Model
			{
				public ExampleEnum Status { get; set; }
			}
		}
		""";

	[Test]
	public async Task EnumProperty_GivenNoAttributes_GeneratesEnumRuleValidation(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = EnumModel;

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"var statusEnumRule = new global::ZodSharp.Rules.EnumRule<global::Testing.ExampleEnum>()"
			);
		await Assert.That(generated).ContainsGeneratedCode("!statusEnumRule.IsValid(statusEnumRuleValue)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"((global::ZodSharp.Core.IZodRule)statusEnumRule).Code ?? global::ZodSharp.Rules.EnumRule<global::Testing.ExampleEnum>.ErrorCode"
			);
	}

	[Test]
	public async Task EnumProperty_GivenUndefinedValue_ReportsInvalidEnumValue(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = EnumModel;

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(instance, Enum.ToObject(enumType, 999));

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_enum_value");
		await Assert.That(errors[0].Path[0]).IsEqualTo("Status");
	}

	[Test]
	public async Task EnumProperty_GivenDefinedValue_ReturnsSuccess(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = EnumModel;

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(instance, Enum.ToObject(enumType, 1));

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsTrue();
	}

	[Test]
	public async Task EnumProperty_GivenValidateEnumValuesFalse_DoesNotGenerateEnumRuleValidation(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue
				}

				[ZodSchema(ValidateEnumValues = false)]
				public class Model
				{
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert.That(generated).DoesNotContain("EnumRule", StringComparison.Ordinal);
		await Assert.That(generated).DoesNotContain("invalid_enum_value", StringComparison.Ordinal);
	}

	[Test]
	public async Task EnumProperty_GivenFlagsEnum_DoesNotGenerateEnumRuleValidation(CancellationToken cancellationToken)
	{
		// Arrange - a flags combination is valid without being a defined member.
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[System.Flags]
				public enum ExampleFlags
				{
					None = 0,

					First = 1,

					Second = 2
				}

				[ZodSchema]
				public class Model
				{
					public ExampleFlags Flags { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert.That(generated).DoesNotContain("EnumRule", StringComparison.Ordinal);
	}

	[Test]
	public async Task EnumProperty_GivenAllowedValues_DoesNotGenerateEnumRuleValidation(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the explicit allow-list governs the property.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue,

					AnotherValidValue
				}

				[ZodSchema]
				public class Model
				{
					[AllowedValues(ExampleEnum.AValidValue, ExampleEnum.AnotherValidValue)]
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert.That(generated).DoesNotContain("EnumRule", StringComparison.Ordinal);
		await Assert.That(generated).ContainsGeneratedCode("invalid_value");
	}

	[Test]
	public async Task EnumProperty_GivenDeniedValues_AbsorbsThemIntoTheEnumRule(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue,

					AnotherValidValue
				}

				[ZodSchema]
				public class Model
				{
					[DeniedValues(ExampleEnum.Unspecified)]
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert - the denied value is the rule's disallowed set, and no standalone check duplicates it.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.EnumRule<global::Testing.ExampleEnum>(new global::Testing.ExampleEnum[] { (global::Testing.ExampleEnum)0 })"
			);
		await Assert.That(generated).DoesNotContain("contains a denied value", StringComparison.Ordinal);
	}

	[Test]
	public async Task EnumProperty_GivenDeniedValue_ReportsASingleInvalidEnumValue(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue,

					AnotherValidValue
				}

				[ZodSchema]
				public class Model
				{
					[DeniedValues(ExampleEnum.Unspecified)]
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(instance, Enum.ToObject(enumType, 0));

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_enum_value");
	}

	[Test]
	public async Task EnumProperty_GivenZodIgnoreMember_AbsorbsItIntoTheEnumRule(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					[ZodIgnore]
					Unspecified,

					AValidValue,

					AnotherValidValue
				}

				[ZodSchema]
				public class Model
				{
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.EnumRule<global::Testing.ExampleEnum>(new global::Testing.ExampleEnum[] { global::Testing.ExampleEnum.Unspecified })"
			);
	}

	[Test]
	public async Task EnumProperty_GivenZodIgnoreMember_RejectsIt(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					[ZodIgnore]
					Unspecified,

					AValidValue,

					AnotherValidValue
				}

				[ZodSchema]
				public class Model
				{
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(validInstance, Enum.ToObject(enumType, 1));

		var ignoredInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(ignoredInstance, Enum.ToObject(enumType, 0));

		var validResult = schemaType.GetMethod("Validate")!.Invoke(null, [validInstance])!;
		var ignoredResult = schemaType.GetMethod("Validate")!.Invoke(null, [ignoredInstance])!;

		// Assert - the member is defined, so only the ignored marker rejects it.
		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
		await Assert.That((bool)ignoredResult.GetType().GetProperty("IsSuccess")!.GetValue(ignoredResult)!).IsFalse();

		var errors =
			(ImmutableArray<ValidationError>)ignoredResult.GetType().GetProperty("Errors")!.GetValue(ignoredResult)!;
		await Assert.That(errors[0].Code).IsEqualTo("invalid_enum_value");
	}

	[Test]
	public async Task EnumProperty_GivenNullableEnum_ValidatesOnlyWhenNotNull(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue
				}

				[ZodSchema]
				public class Model
				{
					public ExampleEnum? Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var nullInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(nullInstance, null);

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(invalidInstance, Enum.ToObject(enumType, 999));

		var nullResult = schemaType.GetMethod("Validate")!.Invoke(null, [nullInstance])!;
		var invalidResult = schemaType.GetMethod("Validate")!.Invoke(null, [invalidInstance])!;

		// Assert
		await Assert.That((bool)nullResult.GetType().GetProperty("IsSuccess")!.GetValue(nullResult)!).IsTrue();
		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
	}

	[Test]
	public async Task EnumProperty_GivenZodIgnoreMemberInTheEnum_ReportsTheEnumRuleForEveryPropertyOfThatType(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the marker is on the enum, so every property of the type enforces it.
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public enum ExampleEnum
				{
					[ZodIgnore]
					Unspecified,

					AValidValue
				}

				[ZodSchema]
				public class Model
				{
					public ExampleEnum First { get; set; }

					public ExampleEnum Second { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ModelSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("var firstEnumRule");
		await Assert.That(generated).ContainsGeneratedCode("var secondEnumRule");
	}

	[Test]
	public async Task EnumProperty_GivenExplicitEnumAttribute_ResolvesTheRuleAndValidates(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the automatic rule is off, so the explicit [Enum] attribute is what validates. The rule
		// closes over the enum member type, which is what the constraint resolution has to accept.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue
				}

				[ZodSchema(ValidateEnumValues = false)]
				public class Model
				{
					[Enum(Message = "Not a valid option.")]
					public ExampleEnum Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("ModelSchema");
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(invalidInstance, Enum.ToObject(enumType, 999));

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [invalidInstance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.EnumRule<global::Testing.ExampleEnum>(");
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_enum_value");
		await Assert.That(errors[0].Message).IsEqualTo("Not a valid option.");
	}

	[Test]
	public async Task EnumProperty_GivenExplicitEnumAttributeOnNullableEnum_ResolvesTheRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the explicit attribute must close the rule over the underlying enum type, matching the
		// automatic path, so the nullable property is validated only when it is not null.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				public enum ExampleEnum
				{
					Unspecified,

					AValidValue
				}

				[ZodSchema(ValidateEnumValues = false)]
				public class Model
				{
					[Enum]
					public ExampleEnum? Status { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.Model")!;
		var schemaType = assembly.GetType("Testing.ModelSchema")!;
		var enumType = assembly.GetType("Testing.ExampleEnum")!;

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Status")!.SetValue(invalidInstance, Enum.ToObject(enumType, 999));

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [invalidInstance])!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
	}
}

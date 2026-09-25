using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string GenericRuleSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp.Core;

		namespace Testing
		{
			public readonly record struct NotEmptyRule<T>(string? Code = null, string? Message = null)
				: IValidationRule<T>, IZodRule
				where T : struct, IEquatable<T>
			{
				public bool IsValid(in T value) => !value.Equals(default(T));

				public string GetErrorMessage(in T value) => Message ?? "Value must not be empty.";

				string? IZodRule.Code => Code;

				string? IZodRule.Origin => "value_object";
			}

			[ZodRule(typeof(NotEmptyRule<>))]
			[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
			public sealed class NotEmptyAttribute : ValidationAttribute
			{
				public string? Code { get; set; }

				public string? Message { get; set; }
			}

			[ZodSchema]
			public partial record struct AssetId
			{
				[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
				public Guid Value { get; init; }
			}

			[ZodSchema]
			public partial record struct Sequence
			{
				[NotEmpty]
				public int Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task GenericRule_GivenUnboundGenericRule_ClosesWithPropertyType(CancellationToken cancellationToken)
	{
		// Act
		var driverResult = await GenerateAsync(GenericRuleSource, cancellationToken);
		var assetSchema = driverResult.GetSource("AssetIdSchema");
		var sequenceSchema = driverResult.GetSource("SequenceSchema");

		// Assert
		await Assert.That(assetSchema).ContainsGeneratedCode("new global::Testing.NotEmptyRule<global::System.Guid>(");
		await Assert.That(sequenceSchema).ContainsGeneratedCode("new global::Testing.NotEmptyRule<int>(");
	}

	[Test]
	public async Task GenericRule_GivenRuleOwningIdentity_EmitsRuleCodeExpression(CancellationToken cancellationToken)
	{
		// Act
		var driverResult = await GenerateAsync(GenericRuleSource, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the rule implements IZodRule, so its own identity is preferred over the attribute's.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("((global::ZodSharp.Core.IZodRule)valueCustomRule0).Code ?? \"invalid_asset_id\"");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("((global::ZodSharp.Core.IZodRule)valueCustomRule0).Origin ?? null");
	}

	[Test]
	public async Task GenericRule_GivenScalarLikeStruct_FailsAtRuntimeForEmptyGuid(CancellationToken cancellationToken)
	{
		// Arrange
		var driverResult = await GenerateAsync(
			GenericRuleSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.AssetId")!;
		var schemaType = assembly.GetType("Testing.AssetIdSchema")!;
		var validate = schemaType.GetMethod("Validate")!;

		// Act — default(Guid) is Guid.Empty
		var emptyResult = validate.Invoke(null, [Activator.CreateInstance(modelType)!])!;

		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Value")!.SetValue(validInstance, Guid.NewGuid());
		var validResult = validate.Invoke(null, [validInstance])!;

		// Assert
		await Assert.That((bool)emptyResult.GetType().GetProperty("IsSuccess")!.GetValue(emptyResult)!).IsFalse();

		var errors = (System.Collections.Immutable.ImmutableArray<ZodSharp.Core.ValidationError>)
			emptyResult.GetType().GetProperty("Errors")!.GetValue(emptyResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("invalid_asset_id");
		await Assert.That(errors[0].Origin).IsEqualTo("value_object");
		await Assert.That(errors[0].Message).IsEqualTo("AssetId must not be empty.");

		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
	}

	[Test]
	public async Task GenericRule_GivenReferenceTypeProperty_DoesNotEmitUnclosableRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange — NotEmptyRule<T> requires T : struct, so a string property cannot be closed.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NotEmptyRule<T>(string? Message = null)
					: IValidationRule<T>
					where T : struct, IEquatable<T>
				{
					public bool IsValid(in T value) => !value.Equals(default(T));

					public string GetErrorMessage(in T value) => Message ?? "Value must not be empty.";
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute { }

				[ZodSchema]
				public class Broken
				{
					[NotEmpty]
					public string? Name { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("BrokenSchema");

		// Assert
		await Assert.That(generated).DoesNotContain("NotEmptyRule");
	}

	[Test]
	public async Task AttributeCode_GivenCodeNamedArgument_OverridesZodRuleCode(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NonEmptyGuidRule(string? Message = null) : IValidationRule<Guid>
				{
					public bool IsValid(in Guid value) => value != Guid.Empty;

					public string GetErrorMessage(in Guid value) => Message ?? "Empty.";
				}

				[ZodRule(typeof(NonEmptyGuidRule), Code = "rule_code")]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NonEmptyGuidAttribute : ValidationAttribute
				{
					public string? Code { get; set; }

					public string? Message { get; set; }
				}

				[ZodSchema]
				public partial record struct AssetId
				{
					[NonEmptyGuid(Code = "attr_code", Message = "AssetId must not be empty.")]
					public Guid Value { get; init; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the attribute-level Code wins over the [ZodRule] Code.
		await Assert.That(generated).ContainsGeneratedCode("\"attr_code\"");
		await Assert.That(generated).DoesNotContain("rule_code");
	}
}

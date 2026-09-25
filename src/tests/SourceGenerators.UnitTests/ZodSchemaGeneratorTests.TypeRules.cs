using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string TypeRuleSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Testing
		{
			public interface IScalarValueObject<TSelf, TValue>
				where TSelf : IScalarValueObject<TSelf, TValue>
			{
				TValue Value { get; }
			}

			public readonly record struct NotEmptyRule<TSelf>(string? Code = null, string? Message = null)
				: IValidationRule<TSelf>, IZodRule
				where TSelf : IScalarValueObject<TSelf, Guid>
			{
				public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

				public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";

				string? IZodRule.Code => Code;

				string? IZodRule.Origin => "value_object";
			}

			[ZodRule(typeof(NotEmptyRule<>))]
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
			public sealed class NotEmptyAttribute : ValidationAttribute
			{
				public string? Code { get; set; }

				public string? Message { get; set; }
			}

			[NotEmpty(Code = "invalid_asset_id", Message = "AssetId must not be empty.")]
			[ZodSchema]
			public partial record struct AssetId : IScalarValueObject<AssetId, Guid>
			{
				public Guid Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task TypeRule_GivenRuleAttributeOnSchemaType_EmitsWholeObjectRule(CancellationToken cancellationToken)
	{
		// Act
		var driverResult = await GenerateAsync(TypeRuleSource, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the rule runs against the value object itself with an empty path.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::Testing.NotEmptyRule<global::Testing.AssetId>(");
		await Assert.That(generated).ContainsGeneratedCode(".IsValid(value)");
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath, origin:");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("((global::ZodSharp.Core.IZodRule)assetIdCustomRule0).Code ?? \"invalid_asset_id\"");
		// The rule is bound to the value object, not to its Guid property.
		await Assert.That(generated).DoesNotContain("NotEmptyRule<global::System.Guid>");
	}

	[Test]
	public async Task TypeRule_GivenEmptyValueObject_FailsAtRuntimeWithTypeLevelIdentity(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var driverResult = await GenerateAsync(
			TypeRuleSource,
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
		await Assert.That(errors[0].Path.Length).IsEqualTo(0);

		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
	}

	[Test]
	public async Task TypeRule_GivenNestedTypeWithoutZodSchema_EmitsWholeObjectRule(CancellationToken cancellationToken)
	{
		// Arrange — AssetId carries the rule but no [ZodSchema]; it is reached through Order's property.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Testing
			{
				public interface IScalarValueObject<TSelf, TValue>
					where TSelf : IScalarValueObject<TSelf, TValue>
				{
					TValue Value { get; }
				}

				public readonly record struct NotEmptyRule<TSelf>(string? Code = null, string? Message = null)
					: IValidationRule<TSelf>, IZodRule
					where TSelf : IScalarValueObject<TSelf, Guid>
				{
					public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

					public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";

					string? IZodRule.Code => Code;

					string? IZodRule.Origin => "value_object";
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute
				{
					public string? Code { get; set; }

					public string? Message { get; set; }
				}

				[NotEmpty(Code = "invalid_asset_id")]
				public partial record struct AssetId : IScalarValueObject<AssetId, Guid>
				{
					public Guid Value { get; init; }
				}

				[ZodSchema]
				public class Order
				{
					public AssetId Id { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::Testing.NotEmptyRule<global::Testing.AssetId>(");
		await Assert.That(generated).ContainsGeneratedCode(".IsValid(value)");
	}
}

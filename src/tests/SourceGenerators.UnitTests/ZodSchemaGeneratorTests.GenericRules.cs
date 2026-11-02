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
		using var driverResult = await GenerateAsync(GenericRuleSource, cancellationToken);
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
		using var driverResult = await GenerateAsync(GenericRuleSource, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the rule implements IZodRule, so its own identity is preferred over the attribute's.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("((global::ZodSharp.Core.IZodRule)valueCustomRule0).Code ?? \"invalid_asset_id\"");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("ZodRuleErrors.Create(valueCustomRule0, valueCustomRuleValue0");
	}

	[Test]
	public async Task GenericRule_GivenScalarLikeStruct_FailsAtRuntimeForEmptyGuid(CancellationToken cancellationToken)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
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

		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
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
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("BrokenSchema");

		// Assert
		await Assert.That(generated).DoesNotContain("NotEmptyRule");
	}

	/// <summary>
	/// A rule pair: a non-generic rule for the primitive and an arity-1 generic rule constrained to the scalar
	/// value object, addressed through a single attribute mapped to the open generic. This mirrors the shape
	/// used by consumers such as <c>NonWhiteSpaceStringRule</c>.
	/// </summary>
	const string RuleFamilySource = """
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

			public readonly record struct NonWhiteSpaceStringRule(string? Message = null)
				: IValidationRule<string?>
			{
				public bool IsValid(in string? value) => value != null && !string.IsNullOrWhiteSpace(value);

				public string GetErrorMessage(in string? value) => Message ?? "Value must not be empty.";
			}

			public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Code = null, string? Message = null)
				: IValidationRule<TSelf>
				where TSelf : IScalarValueObject<TSelf, string>
			{
				public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);

				public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
			}

			[ZodRule(typeof(NonWhiteSpaceStringRule<>))]
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
			public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute
			{
				public string? Code { get; set; }

				public string? Message { get; set; }
			}

			[ZodSchema]
			public partial class Repository
			{
				[NonWhiteSpaceString]
				public string? Name { get; set; }
			}

			[ZodSchema]
			[NonWhiteSpaceString]
			public readonly partial record struct ExternalUserId : IScalarValueObject<ExternalUserId, string>
			{
				public string Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task GenericRule_GivenValueObjectRuleOnPrimitiveMember_ResolvesNonGenericSibling(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the attribute maps to a value-object-only rule and is applied to a `string` member and to
		// a scalar value object.
		// Act
		using var driverResult = await GenerateAsync(
			RuleFamilySource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var repositorySchema = driverResult.GetSource("RepositorySchema");
		var externalUserIdSchema = driverResult.GetSource("ExternalUserIdSchema");

		// Assert — the primitive member resolves to the non-generic sibling, the scalar closes the generic, and
		// the generated validators compile. Closing the generic over `string` used to leak CS0311 into the
		// consumer's generated code.
		await Assert.That(repositorySchema).ContainsGeneratedCode("new global::Testing.NonWhiteSpaceStringRule(");
		await Assert.That(repositorySchema).DoesNotContain("NonWhiteSpaceStringRule<");
		await Assert
			.That(externalUserIdSchema)
			.ContainsGeneratedCode("new global::Testing.NonWhiteSpaceStringRule<global::Testing.ExternalUserId>(");
		// The hand-authored attribute maps to the open generic, so it covers the whole family.
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN038");
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task GenericRule_GivenClosedRuleOnScalarValueObject_ResolvesGenericFamilyMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the attribute maps to the non-generic rule but is applied to the scalar value object, so the
		// resolution has to walk up to the generic family member.
		var source = RuleFamilySource.Replace(
			"typeof(NonWhiteSpaceStringRule<>)",
			"typeof(NonWhiteSpaceStringRule)",
			StringComparison.Ordinal
		);

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var externalUserIdSchema = driverResult.GetSource("ExternalUserIdSchema");

		// Assert
		await Assert
			.That(externalUserIdSchema)
			.ContainsGeneratedCode("new global::Testing.NonWhiteSpaceStringRule<global::Testing.ExternalUserId>(");
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task GenericRule_GivenUnsatisfiableValueObjectConstraint_DoesNotEmitUnclosableRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange — only the value-object rule exists, so nothing can validate the `string` member.
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

				public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Code = null, string? Message = null)
					: IValidationRule<TSelf>
					where TSelf : IScalarValueObject<TSelf, string>
				{
					public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);

					public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
				}

				[ZodRule(typeof(NonWhiteSpaceStringRule<>))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute { }

				[ZodSchema]
				public partial class Repository
				{
					[NonWhiteSpaceString]
					public string? Name { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var repositorySchema = driverResult.GetSource("RepositorySchema");

		// Assert — the unclosable rule is dropped rather than emitted, so the generated validator still
		// compiles. ZODSGEN030 reports it through the analyzer (see ZodSchemaAnalyzerTests.CustomRules).
		await Assert.That(repositorySchema).DoesNotContain("NonWhiteSpaceStringRule<");
		driverResult.AssertNoCompilationErrors();
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
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the attribute-level Code wins over the [ZodRule] Code.
		await Assert.That(generated).ContainsGeneratedCode("\"attr_code\"");
		await Assert.That(generated).DoesNotContain("rule_code");
	}
}

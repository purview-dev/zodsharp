using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Tests for the automatic scalar forms (<c>[Scalar&lt;TValue&gt;]</c> and
/// <c>[Scalar(typeof(TValue))]</c>), where the underlying property is emitted by the value-object
/// generator and is therefore invisible to the ZodSharp generator. The schema must be generated from the
/// attribute, and type-level rules must be adapted to the scalar's underlying value.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// Stand-in Purview.ValueObjects types. This repository does not reference the value-objects package, so
	/// the attribute contracts (both automatic forms, with <c>Nullable</c> on the shared base) and the
	/// scalar rule adapter are declared here. In a real consumer the adapter is emitted by the value-object
	/// generator.
	/// </summary>
	const string AutomaticScalarPrelude = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;
		using ZodSharp.Rules;

		namespace Purview.ValueObjects.Serialization
		{
			public abstract class ScalarOptionsAttribute : Attribute
			{
				protected ScalarOptionsAttribute(string propertyName) => PropertyName = propertyName;

				public string PropertyName { get; }

				public bool Nullable { get; init; }
			}

			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
			public sealed class ScalarAttribute : ScalarOptionsAttribute
			{
				public ScalarAttribute(string propertyName = "Value") : base(propertyName) { }

				public ScalarAttribute(Type valueType, string propertyName = "Value") : base(propertyName) =>
					ValueType = valueType;

				public Type? ValueType { get; }
			}

			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
			public sealed class ScalarAttribute<TValue>(string propertyName = "Value")
				: ScalarOptionsAttribute(propertyName);
		}

		namespace Purview.ValueObjects
		{
			public interface IScalarValueObject<TSelf, TValue>
				where TSelf : IScalarValueObject<TSelf, TValue>
			{
				TValue Value { get; }
			}

			// Stands in for the adapter the value-object generator emits into a real consumer.
			public readonly record struct ScalarRuleAdapter<TSelf, TValue, TRule>(TRule Rule)
				: IValidationRule<TSelf>
				where TSelf : IScalarValueObject<TSelf, TValue>
				where TRule : IValidationRule<TValue>
			{
				public bool IsValid(in TSelf value) => Rule.IsValid(value.Value);

				public string GetErrorMessage(in TSelf value) => Rule.GetErrorMessage(value.Value);
			}
		}
		""";

	[Test]
	public async Task AutomaticScalar_GivenGenericAttribute_AdaptsTypeLevelRuleToUnderlyingValue(
		CancellationToken cancellationToken
	)
	{
		// Arrange — no declared member: the value-object generator owns `Value`.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<Guid>]
					[NonSentinel(Message = "InstallationId must not be empty.")]
					[ZodSchema]
					public readonly partial record struct InstallationId { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("InstallationIdSchema");

		// Assert — the rule is closed over the underlying Guid and adapted to the value object.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.InstallationId, global::System.Guid, global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>>("
			);
		await Assert.That(generated).ContainsGeneratedCode(".IsValid(value)");
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath");

		// The property is synthesized from the attribute (name defaults to Value).
		await Assert.That(generated).ContainsGeneratedCode("ImmutableArray.Create(\"Value\")");
	}

	[Test]
	public async Task AutomaticScalar_GivenTypeofAttribute_AdaptsTypeLevelRuleToUnderlyingValue(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the non-generic automatic form, which is not a generic attribute.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar(typeof(Guid))]
					[NonSentinel(Message = "InstallationId must not be empty.")]
					[ZodSchema]
					public readonly partial record struct InstallationId { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("InstallationIdSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.InstallationId, global::System.Guid, global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>>("
			);
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath");
	}

	[Test]
	public async Task AutomaticScalar_GivenNullableReference_ClosesAdapterOverNullableString(
		CancellationToken cancellationToken
	)
	{
		// Arrange — [Scalar<string>(Nullable = true)] round-trips JSON null, so the schema's string rule must
		// accept null.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
					[NullOrNonWhiteSpace(Message = "Nickname must be null or non-whitespace.")]
					[ZodSchema]
					public readonly partial record struct Nickname { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("NicknameSchema");

		// Assert — the wrapped rule and the adapter's TValue keep the nullable annotation.
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.NullOrNonWhiteSpaceRule(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.Nickname, string?, global::ZodSharp.Rules.NullOrNonWhiteSpaceRule>("
			);
	}

	[Test]
	public async Task AutomaticScalar_GivenTypeofNullableReference_ClosesAdapterOverNullableString(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar(typeof(string), Nullable = true)]
					[NullOrNonWhiteSpace(Message = "Nickname must be null or non-whitespace.")]
					[ZodSchema]
					public readonly partial record struct Nickname { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("NicknameSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.Nickname, string?, global::ZodSharp.Rules.NullOrNonWhiteSpaceRule>("
			);
	}

	[Test]
	public async Task AutomaticScalar_GivenNullableValueType_EmitsSchemaThatAcceptsNull(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<int?>]
					[ZodSchema]
					public readonly partial record struct Score { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("ScoreSchema");

		// Assert — the synthesized nullable property is guarded, so null is accepted.
		await Assert.That(generated).ContainsGeneratedCode("value.Value != null");
		await Assert.That(generated).ContainsGeneratedCode("ImmutableArray.Create(\"Value\")");
	}

	[Test]
	public async Task AutomaticScalar_GivenCustomPropertyName_SynthesizesThatProperty(
		CancellationToken cancellationToken
	)
	{
		// Arrange — both automatic spellings with a custom property name.
		const string genericSource =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<Guid>("Id")]
					[NonSentinel]
					[ZodSchema]
					public readonly partial record struct TenantId { }
				}
				""";
		const string typeofSource =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar(typeof(Guid), "Id")]
					[NonSentinel]
					[ZodSchema]
					public readonly partial record struct TenantId { }
				}
				""";

		// Act
		var genericResult = await GenerateAsync(
			genericSource,
			ZodSourceGeneratorTestOptions.NoValidation,
			cancellationToken
		);
		var typeofResult = await GenerateAsync(
			typeofSource,
			ZodSourceGeneratorTestOptions.NoValidation,
			cancellationToken
		);
		var generic = genericResult.GetSource("TenantIdSchema");
		var typeofGenerated = typeofResult.GetSource("TenantIdSchema");

		// Assert — the synthesized property is named Id for both forms.
		await Assert.That(generic).ContainsGeneratedCode("ImmutableArray.Create(\"Id\")");
		await Assert.That(typeofGenerated).ContainsGeneratedCode("ImmutableArray.Create(\"Id\")");
	}

	[Test]
	public async Task AutomaticScalar_GivenRequiredZod_ReportsMissingFieldThroughAdaptedRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<string>(Nullable = true)]
					[RequiredZod]
					[ZodSchema]
					public readonly partial record struct Name { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("NameSchema");

		// Assert — [RequiredZod] is closed over the underlying string? and adapted, with an empty path.
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.RequiredRule<string?>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.Name, string?, global::ZodSharp.Rules.RequiredRule<string?>>("
			);
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath");
	}

	[Test]
	public async Task AutomaticScalar_GivenCustomRule_ReportsCodeMessageAndOrigin(CancellationToken cancellationToken)
	{
		// Arrange — a hand-authored [ZodRule]-mapped rule written against the underlying value.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					public readonly record struct NonEmptyRule<T>(string? Message = null)
						: IValidationRule<T>, IZodRule
						where T : IEquatable<T>
					{
						public bool IsValid(in T value) => !value.Equals(default(T)!);

						public string GetErrorMessage(in T value) => Message ?? "Value must not be empty.";

						string? IZodRule.Code => "invalid_value";

						string? IZodRule.Origin => "value_object";
					}

					[ZodRule(typeof(NonEmptyRule<>))]
					[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
					public sealed class NonEmptyAttribute : ValidationAttribute
					{
						public string? Message { get; set; }
					}

					[Purview.ValueObjects.Serialization.Scalar<Guid>]
					[NonEmpty(Message = "Id must not be empty.")]
					[ZodSchema]
					public readonly partial record struct ExternalId { }
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("ExternalIdSchema");

		// Assert — the wrapped rule owns the identity, not the adapter.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.ExternalId, global::System.Guid, global::Testing.NonEmptyRule<global::System.Guid>>("
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"((global::ZodSharp.Core.IZodRule)externalIdCustomRuleInner0).Code ?? \"validation_failed\""
			);
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath, origin:");
	}

	[Test]
	public async Task AutomaticScalar_GivenGenericAttributeWithDeclaredProperty_ValidatesAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the property is declared so the harness can compile the generated schema (the real
		// automatic form gets it from the value-object generator, which this harness does not run).
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<Guid>]
					[NonSentinel(Message = "InstallationId must not be empty.")]
					[ZodSchema]
					public readonly partial record struct InstallationId
						: Purview.ValueObjects.IScalarValueObject<InstallationId, Guid>
					{
						public Guid Value { get; init; }
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
		var modelType = assembly.GetType("Testing.InstallationId")!;
		var validate = assembly.GetType("Testing.InstallationIdSchema")!.GetMethod("Validate")!;

		var emptyResult = validate.Invoke(null, [Activator.CreateInstance(modelType)!])!;
		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Value")!.SetValue(validInstance, Guid.NewGuid());
		var validResult = validate.Invoke(null, [validInstance])!;

		// Assert
		await Assert.That((bool)emptyResult.GetType().GetProperty("IsSuccess")!.GetValue(emptyResult)!).IsFalse();

		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			emptyResult.GetType().GetProperty("Errors")!.GetValue(emptyResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo(Rules.NonSentinelRule<Guid>.ErrorCode);
		await Assert.That(errors[0].Message).IsEqualTo("InstallationId must not be empty.");
		await Assert.That(errors[0].Path.Length).IsEqualTo(0);

		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
	}

	[Test]
	public async Task AutomaticScalar_GivenTypeLevelRuleWithoutMessage_NamesTheScalarInTheDefaultMessage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the property is declared so the harness can compile the generated schema.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<Guid>]
					[NonSentinel]
					[ZodSchema]
					public readonly partial record struct TenantId
						: Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
					{
						public Guid Value { get; init; }
					}
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("TenantIdSchema");

		// Assert — the rule's own default message is reworded to name the scalar, since the error has an
		// empty path and nothing else identifies it.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(\"TenantId has a sentinel value, but got {0}\", null!)"
			);
	}

	[Test]
	public async Task AutomaticScalar_GivenTypeLevelRuleWithoutMessage_ReportsTheScalarNameAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the manual `[Scalar]` form with a declared `Value` property, exactly as a consumer writes it.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar]
					[NonSentinel]
					[ZodSchema]
					public readonly partial record struct TenantId
						: Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
					{
						public Guid Value { get; init; }
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
		var modelType = assembly.GetType("Testing.TenantId")!;
		var validate = assembly.GetType("Testing.TenantIdSchema")!.GetMethod("Validate")!;

		var emptyResult = validate.Invoke(null, [Activator.CreateInstance(modelType)!])!;

		// Assert
		await Assert.That((bool)emptyResult.GetType().GetProperty("IsSuccess")!.GetValue(emptyResult)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			emptyResult.GetType().GetProperty("Errors")!.GetValue(emptyResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Message).IsEqualTo("TenantId has a sentinel value, but got an empty GUID");
		await Assert.That(errors[0].Path.Length).IsEqualTo(0);
	}

	[Test]
	public async Task AutomaticScalar_GivenTypeLevelRuleWithMessage_KeepsTheSuppliedMessage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — an explicit message must never be rewritten.
		const string source =
			AutomaticScalarPrelude
			+ """
				namespace Testing
				{
					[Purview.ValueObjects.Serialization.Scalar<Guid>]
					[NonSentinel(Message = "TenantId must not be empty.")]
					[ZodSchema]
					public readonly partial record struct TenantId
						: Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
					{
						public Guid Value { get; init; }
					}
				}
				""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("TenantIdSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(\"TenantId must not be empty.\", null!)"
			);
	}

	[Test]
	public async Task PropertyRule_GivenRuleWithoutMessage_KeepsTheGenericSubject(CancellationToken cancellationToken)
	{
		// Arrange — a rule on a member reports the member through the error path, so the rule's own default
		// message is left untouched.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public partial class Tenant
				{
					[NonSentinel]
					public Guid TenantId { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("TenantSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(null!, null!)");
	}
}

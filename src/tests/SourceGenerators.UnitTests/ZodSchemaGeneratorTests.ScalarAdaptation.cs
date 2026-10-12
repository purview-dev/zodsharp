using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// A scalar value object whose rules are written against its underlying value. The
	/// <c>Purview.ValueObjects</c> types are declared here because this repository does not reference the
	/// value-objects package; in a real consumer the adapter is emitted by the value-object generator.
	/// </summary>
	const string ScalarAdaptationSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Purview.ValueObjects.Serialization
		{
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
			public sealed class ScalarAttribute : Attribute
			{
				public ScalarAttribute(string propertyName = "Value") => PropertyName = propertyName;

				public string PropertyName { get; }
			}
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

		namespace Testing
		{
			public readonly record struct NonSentinelRule<T>(string? Message = null)
				: IValidationRule<T>, IZodRule
				where T : IEquatable<T>
			{
				public bool IsValid(in T value) => !value.Equals(default(T)!);

				public string GetErrorMessage(in T value) => Message ?? "Value must not be the default.";

				string? IZodRule.Code => "invalid_value";

				string? IZodRule.Origin => "value_object";
			}

			[ZodRule(typeof(NonSentinelRule<>))]
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
			public sealed class NonSentinelAttribute : ValidationAttribute
			{
				public string? Message { get; set; }
			}

			[Purview.ValueObjects.Serialization.Scalar]
			[NonSentinel(Message = "TenantId must not be the default.")]
			[ZodSchema]
			public partial record struct TenantId : Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
			{
				public Guid Value { get; init; }
			}
		}
		""";

	/// <summary>
	/// A scalar whose underlying value is a nullable reference. The adapter's <c>TValue</c> has to keep the
	/// annotation, because the wrapped rule is closed over <c>string?</c> and the value object implements
	/// <c>IScalarValueObject&lt;TSelf, string?&gt;</c>: an adapter closed over <c>string</c> satisfies
	/// neither constraint and the generated schema reports CS8631.
	/// </summary>
	const string NullableScalarAdaptationSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Purview.ValueObjects.Serialization
		{
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
			public sealed class ScalarAttribute : Attribute
			{
				public ScalarAttribute(string propertyName = "Value") => PropertyName = propertyName;

				public string PropertyName { get; }
			}
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

		namespace Testing
		{
			public readonly record struct NotNullRule<T>(string? Message = null)
				: IValidationRule<T>, IZodRule
			{
				public bool IsValid(in T value) => value is not null;

				public string GetErrorMessage(in T value) => Message ?? "Value must not be null.";

				string? IZodRule.Code => "null_value";

				string? IZodRule.Origin => "value_object";
			}

			[ZodRule(typeof(NotNullRule<>))]
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
			public sealed class NotNullAttribute : ValidationAttribute
			{
				public string? Message { get; set; }
			}

			[Purview.ValueObjects.Serialization.Scalar]
			[NotNull(Message = "TenantKey must not be null.")]
			[ZodSchema]
			public partial record struct TenantKey : Purview.ValueObjects.IScalarValueObject<TenantKey, string?>
			{
				public string? Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task ScalarAdaptation_GivenNullableReferenceScalarValue_KeepsTheNullableAnnotation(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(
			NullableScalarAdaptationSource,
			ZodSourceGeneratorTestOptions.NoValidation,
			cancellationToken
		);
		var generated = driverResult.GetSource("TenantKeySchema");

		// Assert — the wrapped rule and the adapter's TValue are both closed over string?, matching the
		// rule's IValidationRule<string?> and the scalar's IScalarValueObject<TenantKey, string?>.
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.NotNullRule<string?>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.TenantKey, string?, global::Testing.NotNullRule<string?>>("
			);

		// The generated schema is in a #nullable enable context, so an adapter closed over the wrong
		// nullability would surface here as CS8631 rather than at a consumer's build.
		var diagnostics = driverResult.CompilationResult.Compilation.GetDiagnostics(cancellationToken);
		await Assert.That(diagnostics).DoesNotContain(d => d.Id == "CS8631");
	}

	[Test]
	public async Task ScalarAdaptation_GivenRuleWrittenAgainstTheUnderlyingValue_EmitsAdapter(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(ScalarAdaptationSource, cancellationToken);
		var generated = driverResult.GetSource("TenantIdSchema");

		// Assert — the rule is constructed for the Guid and wrapped in the scalar adapter, which validates
		// the value object as a unit.
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.NonSentinelRule<global::System.Guid>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.TenantId, global::System.Guid, global::Testing.NonSentinelRule<global::System.Guid>>("
			);
		await Assert.That(generated).ContainsGeneratedCode(".IsValid(value)");
		await Assert.That(generated).ContainsGeneratedCode("EmptyPath, origin:");
		// The wrapped rule owns the identity, not the adapter.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"((global::ZodSharp.Core.IZodRule)tenantIdCustomRuleInner0).Code ?? \"validation_failed\""
			);
		await Assert.That(generated).DoesNotContain("((global::ZodSharp.Core.IZodRule)tenantIdCustomRule0)");
	}

	[Test]
	public async Task ScalarAdaptation_GivenRuleWrittenAgainstTheUnderlyingValue_FailsAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			ScalarAdaptationSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.TenantId")!;
		var schemaType = assembly.GetType("Testing.TenantIdSchema")!;
		var validate = schemaType.GetMethod("Validate")!;

		// Act — the default Guid is Guid.Empty, which the underlying rule rejects.
		var emptyResult = validate.Invoke(null, [Activator.CreateInstance(modelType)!])!;

		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Value")!.SetValue(validInstance, Guid.NewGuid());
		var validResult = validate.Invoke(null, [validInstance])!;

		// Assert
		await Assert.That((bool)emptyResult.GetType().GetProperty("IsSuccess")!.GetValue(emptyResult)!).IsFalse();

		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			emptyResult.GetType().GetProperty("Errors")!.GetValue(emptyResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("invalid_value");
		await Assert.That(errors[0].Origin).IsEqualTo("value_object");
		await Assert.That(errors[0].Message).IsEqualTo("TenantId must not be the default.");
		await Assert.That(errors[0].Path.Length).IsEqualTo(0);

		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
	}

	[Test]
	public async Task ScalarAdaptation_GivenRuleWrittenAgainstTheScalar_KeepsTheDirectRule(
		CancellationToken cancellationToken
	)
	{
		// A rule whose constraint asks for the scalar itself is a scalar rule family member: it must keep
		// closing over the value object rather than being adapted.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Purview.ValueObjects.Serialization
			{
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
				public sealed class ScalarAttribute : Attribute { }
			}

			namespace Purview.ValueObjects
			{
				public interface IScalarValueObject<TSelf, TValue>
					where TSelf : IScalarValueObject<TSelf, TValue>
				{
					TValue Value { get; }
				}
			}

			namespace Testing
			{
				public readonly record struct NotEmptyRule<TSelf>(string? Code = null)
					: IValidationRule<TSelf>, IZodRule
					where TSelf : Purview.ValueObjects.IScalarValueObject<TSelf, Guid>
				{
					public bool IsValid(in TSelf value) => value.Value != Guid.Empty;

					public string GetErrorMessage(in TSelf value) => "Value must not be empty.";

					string? IZodRule.Code => Code;

					string? IZodRule.Origin => "value_object";
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute
				{
					public string? Code { get; set; }
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[NotEmpty(Code = "invalid_asset_id")]
				[ZodSchema]
				public partial record struct AssetId : Purview.ValueObjects.IScalarValueObject<AssetId, Guid>
				{
					public Guid Value { get; init; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AssetIdSchema");

		// Assert — the rule closes over the value object and is not adapted.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::Testing.NotEmptyRule<global::Testing.AssetId>(");
		await Assert.That(generated).DoesNotContain("ScalarRuleAdapter");
	}

	[Test]
	public async Task ScalarAdaptation_GivenRenamedScalarProperty_AdaptsFromThatProperty(
		CancellationToken cancellationToken
	)
	{
		// The scalar's underlying member can be renamed with [Scalar("Id")], so the adapter must read the
		// value from the declared property rather than assuming Value.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Purview.ValueObjects.Serialization
			{
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
				public sealed class ScalarAttribute : Attribute
				{
					public ScalarAttribute(string propertyName = "Value") => PropertyName = propertyName;

					public string PropertyName { get; }
				}
			}

			namespace Purview.ValueObjects
			{
				public interface IScalarValueObject<TSelf, TValue>
					where TSelf : IScalarValueObject<TSelf, TValue>
				{
					TValue Value { get; }
				}
			}

			namespace Testing
			{
				public readonly record struct NonSentinelRule<T>(string? Message = null)
					: IValidationRule<T>, IZodRule
					where T : IEquatable<T>
				{
					public bool IsValid(in T value) => !value.Equals(default(T)!);

					public string GetErrorMessage(in T value) => Message ?? "Value must not be the default.";

					string? IZodRule.Code => "invalid_value";

					string? IZodRule.Origin => "value_object";
				}

				[ZodRule(typeof(NonSentinelRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NonSentinelAttribute : ValidationAttribute
				{
					public string? Message { get; set; }
				}

				[Purview.ValueObjects.Serialization.Scalar("Id")]
				[NonSentinel(Message = "TenantId must not be the default.")]
				[ZodSchema]
				public partial record struct TenantId : Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
				{
					public Guid Id { get; init; }
				}
			}
			""";

		// Act — the stand-in adapter reads Value, which this renamed scalar does not declare, so the emitted
		// rule is asserted instead of compiled.
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);
		var generated = driverResult.GetSource("TenantIdSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.NonSentinelRule<global::System.Guid>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.TenantId, global::System.Guid, global::Testing.NonSentinelRule<global::System.Guid>>("
			);
	}
}

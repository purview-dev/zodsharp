using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task CustomRule_GivenRuleThatDoesNotMatchPropertyType_ProducesZODSGEN030(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NumberOnlyRule : IValidationRule<double>
				{
					public bool IsValid(in double value) => value > 0;

					public string GetErrorMessage(in double value) => "Must be positive.";
				}

				[ZodRule(typeof(NumberOnlyRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NumberOnlyAttribute : ValidationAttribute { }

				[ZodSchema]
				public sealed class MismatchedRuleModel
				{
					[Required]
					[NumberOnly]
					public string? Name { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	[Test]
	public async Task CustomRule_GivenRequiredCodeWithoutValue_ProducesZODSGEN031(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct RequiredCodeRule(string Code, string? Message = null, string? Origin = null)
					: IValidationRule<string>, IZodRule
				{
					public bool IsValid(in string value) => !string.IsNullOrWhiteSpace(value);

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}

				[ZodRule(typeof(RequiredCodeRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RequiredCodeAttribute : ValidationAttribute { }

				[ZodSchema]
				public sealed class Model
				{
					[RequiredCode]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnmappableCustomRuleArgument);
	}

	[Test]
	public async Task CustomRule_GivenExplicitNullForNonNullableParameter_ProducesZODSGEN031(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct RequiredCodeRule(string Code, string? Message = null, string? Origin = null)
					: IValidationRule<string>, IZodRule
				{
					public bool IsValid(in string value) => !string.IsNullOrWhiteSpace(value);

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}

				[ZodRule(typeof(RequiredCodeRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RequiredCodeAttribute : ValidationAttribute
				{
					public string? Code { get; set; }
				}

				[ZodSchema]
				public sealed class Model
				{
					[RequiredCode(Code = null)]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnmappableCustomRuleArgument);
	}

	[Test]
	public async Task CustomRule_GivenUnclosableGenericRule_ProducesZODSGEN030(CancellationToken cancellationToken)
	{
		// NotEmptyRule<T> requires T : struct, so a string property cannot satisfy the constraint.
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
				public sealed class StrictScalarModel
				{
					[NotEmpty]
					public string? Name { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	[Test]
	public async Task TypeRule_GivenRuleAttributeWithoutSchema_ProducesZODSGEN033(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NotEmptyRule<TSelf>(string? Message = null)
					: IValidationRule<TSelf>
				{
					public bool IsValid(in TSelf value) => value is not null;

					public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute { }

				[NotEmpty]
				public partial record struct AssetId
				{
					public Guid Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.RuleAttributeWithoutSchema);
	}

	[Test]
	public async Task TypeRule_GivenNestedReachableType_DoesNotProduceZODSGEN033(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct NotEmptyRule<TSelf>(string? Message = null)
					: IValidationRule<TSelf>
				{
					public bool IsValid(in TSelf value) => value is not null;

					public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute { }

				[NotEmpty]
				public partial record struct AssetId
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

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.RuleAttributeWithoutSchema);
	}

	[Test]
	public async Task CustomRule_GivenValueObjectConstraintUnsatisfiableByPropertyType_ProducesZODSGEN030(
		CancellationToken cancellationToken
	)
	{
		// The rule is constrained to a scalar value object, so a `string` property cannot satisfy it and no
		// non-generic sibling exists to fall back to.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute { }

				[ZodSchema]
				public sealed class Repository
				{
					[NonWhiteSpaceString]
					public string? Name { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	[Test]
	public async Task CustomRule_GivenValueObjectConstraintWithNonGenericSibling_ProducesNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		// The same rule shape plus a non-generic sibling that validates the string member.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute { }

				[ZodSchema]
				public sealed class Repository
				{
					[NonWhiteSpaceString]
					public string? Name { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	[Test]
	public async Task CustomRule_GivenArgumentTheRuleCannotConsume_ProducesZODSGEN040(
		CancellationToken cancellationToken
	)
	{
		// `Threshold` is neither a rule constructor parameter nor part of the error identity, so the value has
		// no effect on validation.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct RankRule(string? Message = null) : IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}

				[ZodRule(typeof(RankRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RankAttribute : ValidationAttribute
				{
					public string? Code { get; set; }

					public int Threshold { get; set; }
				}

				[ZodSchema]
				public sealed class Sample
				{
					[Rank(Threshold = 3)]
					public int Value { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnusedRuleAttributeArgument);
	}

	[Test]
	public async Task CustomRule_GivenArgumentsTheRuleConsumes_ProducesNoZODSGEN040(CancellationToken cancellationToken)
	{
		// `Code` is error identity, `Message` and `ErrorMessage` feed the message, so nothing is unused.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct RankRule(string? Code = null, string? Message = null)
					: IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}

				[ZodRule(typeof(RankRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RankAttribute : ValidationAttribute
				{
					public string? Code { get; set; }

					public string? Message { get; set; }
				}

				[ZodSchema]
				public sealed class Sample
				{
					[Rank(Code = "invalid_rank", Message = "Bad.")]
					public int Value { get; set; }

					[Rank(ErrorMessage = "Also bad.")]
					public int Other { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnusedRuleAttributeArgument);
	}

	[Test]
	public async Task CustomRule_GivenIdentityContractMembers_ProducesNoZODSGEN040(CancellationToken cancellationToken)
	{
		// The attribute implements IZodRuleAttribute, so its members are identity properties by contract.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct RankRule(string? Message = null) : IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}

				[ZodRule(typeof(RankRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RankAttribute : ValidationAttribute, IZodRuleAttribute
				{
					public string? Code { get; set; }

					public string? Origin { get; set; }
				}

				[ZodSchema]
				public sealed class Sample
				{
					[Rank(Code = "invalid_rank", Origin = "ranking")]
					public int Value { get; set; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnusedRuleAttributeArgument);
	}

	/// <summary>
	/// A type-level rule on a value object whose interface implementation is contributed by another generator
	/// resolves without a diagnostic: the rule is emitted and the consumer's compilation verifies the
	/// constraint, because the generator cannot see the completed type.
	/// </summary>
	[Test]
	public async Task TypeRule_GivenPartialValueObjectWhoseInterfaceIsGenerated_ProducesNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute
				{
					public string? Code { get; set; }

					public string? Message { get; set; }
				}

				// The interface implementation comes from the value object generator, so this compilation
				// cannot see it.
				[NotEmpty(Code = "invalid_asset_id")]
				[ZodSchema]
				public readonly partial record struct AssetId
				{
					public Guid Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	/// <summary>
	/// A type-level rule on a type no generator can complete still reports ZODSGEN030, so a rule that can
	/// never run is never dropped silently.
	/// </summary>
	[Test]
	public async Task TypeRule_GivenTypeThatCannotImplementTheConstraint_ProducesZODSGEN030(
		CancellationToken cancellationToken
	)
	{
		// The type is not partial, so nothing can add the interface the rule's constraint asks for.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				}

				[ZodRule(typeof(NotEmptyRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NotEmptyAttribute : ValidationAttribute { }

				[NotEmpty]
				[ZodSchema]
				public readonly record struct NotAValueObject
				{
					public Guid Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	/// <summary>
	/// A rule written against a scalar value object's underlying value resolves without a diagnostic: it is
	/// adapted to the value object, so the rule is not dropped as an unsupported target.
	/// </summary>
	[Test]
	public async Task TypeRule_GivenRuleWrittenAgainstScalarUnderlyingValue_ProducesNoDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				public readonly record struct NonSentinelRule<T>(string? Message = null)
					: IValidationRule<T>, IZodRule
					where T : IEquatable<T>
				{
					public bool IsValid(in T value) => !value.Equals(default(T)!);

					public string GetErrorMessage(in T value) => Message ?? "Value must not be the default.";
				}

				[ZodRule(typeof(NonSentinelRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NonSentinelAttribute : ValidationAttribute
				{
					public string? Message { get; set; }
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[NonSentinel]
				[ZodSchema]
				public partial record struct TenantId : Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
				{
					public Guid Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}

	/// <summary>
	/// A nullable value scalar's underlying value is <c>Nullable&lt;T&gt;</c>, which does not satisfy a rule's
	/// <c>IEquatable&lt;T&gt;</c> constraint. The analyzer resolves the rule against the unwrapped value, so the
	/// rule is not reported as an unsupported target.
	/// </summary>
	[Test]
	public async Task TypeRule_GivenNullableValueTypeScalar_ResolvesAgainstUnwrappedValue(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
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
				public readonly record struct NonSentinelRule<T>(string? Message = null)
					: IValidationRule<T>, IZodRule
					where T : IEquatable<T>
				{
					public bool IsValid(in T value) => !value.Equals(default(T)!);

					public string GetErrorMessage(in T value) => Message ?? "Value must not be the default.";
				}

				[ZodRule(typeof(NonSentinelRule<>))]
				[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
				public sealed class NonSentinelAttribute : ValidationAttribute
				{
					public string? Message { get; set; }
				}

				[Purview.ValueObjects.Serialization.Scalar]
				[NonSentinel]
				[ZodSchema]
				public partial record struct OptionalTimestamp
					: Purview.ValueObjects.IScalarValueObject<OptionalTimestamp, DateTimeOffset?>
				{
					public DateTimeOffset? Value { get; init; }
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.UnsupportedCustomRuleTarget);
	}
}

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
}

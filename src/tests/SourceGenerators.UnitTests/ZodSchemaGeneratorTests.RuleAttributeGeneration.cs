using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task RuleAttributeGeneration_GivenZodRuleOnRule_GeneratesMatchingAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule(Code = "invalid_string", Origin = "string")]
				public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
					: IValidationRule<string>
				{
					public bool IsValid(in string value) => AllowEmpty || value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("NoWhitespaceAttribute");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("namespace Testing.Rules");
		await Assert.That(generated).ContainsGeneratedCode("class NoWhitespaceAttribute");
		await Assert.That(generated).Contains("global::System.ComponentModel.DataAnnotations.ValidationAttribute");
		await Assert.That(generated).ContainsGeneratedCode("public bool AllowEmpty { get; set; } = true;");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::Testing.Rules.NoWhitespaceRule)");
		await Assert.That(generated).ContainsGeneratedCode("\"invalid_string\"");
		await Assert.That(generated).ContainsGeneratedCode("\"string\"");
		// The rule's 'message' parameter is surfaced as a Message alias; the inherited
		// ValidationAttribute.ErrorMessage remains the fallback the resolver maps onto it.
		await Assert.That(generated).ContainsGeneratedCode("public string? Message { get; set; } = null;");
		// A generated attribute can annotate a scalar value object as well as a member.
		await Assert.That(generated).Contains("global::System.AttributeTargets.Class");
		await Assert.That(generated).Contains("global::System.AttributeTargets.Struct");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenZodRuleOnRule_GeneratedAttributeCompilesAndCarriesMapping(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule(Code = "invalid_string", Origin = "string")]
				public readonly record struct NoWhitespaceRule(bool AllowEmpty = true, string? Message = null)
					: IValidationRule<string>
				{
					public bool IsValid(in string value) => AllowEmpty || value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
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

		// Assert
		var attributeType = assembly.GetType("Testing.Rules.NoWhitespaceAttribute");
		await Assert.That(attributeType).IsNotNull();
		await Assert
			.That(attributeType!.BaseType!.FullName)
			.IsEqualTo("System.ComponentModel.DataAnnotations.ValidationAttribute");
		await Assert.That(attributeType.GetProperty("AllowEmpty")).IsNotNull();

		var mapping = attributeType
			.GetCustomAttributes(inherit: false)
			.FirstOrDefault(static a => a.GetType().FullName == "ZodSharp.Core.ZodRuleAttribute");
		await Assert.That(mapping).IsNotNull();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenHandAuthoredAttribute_DoesNotGenerateDuplicate(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.IndexOf(' ') < 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}

				[ZodRule(typeof(NoWhitespaceRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NoWhitespaceAttribute : ValidationAttribute { }

				[ZodRule]
				public readonly record struct OtherRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert — the hand-authored attribute is left alone, the unmapped rule still gets one.
		await Assert.That(driverResult.GetSource("OtherAttribute")).Contains("class OtherAttribute");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN037");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenHandAuthoredAttributeOfDerivedName_ReportsCollision(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the rule derives `NoWhitespaceAttribute`, which is already declared by hand.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.IndexOf(' ') < 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}

				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NoWhitespaceAttribute : ValidationAttribute { }
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert — the hand-authored declaration wins, nothing is generated, and the suppression is reported
		// rather than silent (previously the hand-authored mapping re-mapped the name with no notice).
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN037");
		await Assert.That(driverResult.GetSource("NoWhitespaceAttribute")).IsNull();
	}

	/// <summary>
	/// A rule pair where the arity-1 generic rule carries the <c>[ZodRule]</c> marker, so the generator emits a
	/// single attribute mapped to the open generic — the form that serves both the primitive member (resolved
	/// through the non-generic sibling) and the scalar value object (closed with that type).
	/// </summary>
	const string GenericRuleMarkerSource = """
		using ZodSharp.Core;

		namespace Testing.Rules
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

			[ZodRule]
			public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Code = null, string? Message = null)
				: IValidationRule<TSelf>
				where TSelf : IScalarValueObject<TSelf, string>
			{
				public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);

				public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenZodRuleOnGenericRule_GeneratesAttributeMappedToOpenGeneric(
		CancellationToken cancellationToken
	)
	{
		// Act
		using var driverResult = await GenerateAsync(
			GenericRuleMarkerSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("NonWhiteSpaceStringAttribute");

		// Assert — the attribute is generated in the rule's namespace and maps to the unbound generic so its
		// consumers resolve the primitive or the scalar form automatically.
		await Assert.That(generated).ContainsGeneratedCode("namespace Testing.Rules");
		await Assert.That(generated).ContainsGeneratedCode("class NonWhiteSpaceStringAttribute");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::Testing.Rules.NonWhiteSpaceStringRule<>)");
		await Assert.That(generated).Contains("Code { get; set; }");
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenBothRuleHalvesMarked_GeneratesSingleAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange — both halves of the pair are marked, so both would derive `NonWhiteSpaceStringAttribute`.
		var source = GenericRuleMarkerSource.Replace(
			"""
			public readonly record struct NonWhiteSpaceStringRule(string? Message = null)
				: IValidationRule<string?>
			""",
			"""
			[ZodRule]
			public readonly record struct NonWhiteSpaceStringRule(string? Message = null)
				: IValidationRule<string?>
			""",
			StringComparison.Ordinal
		);

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("NonWhiteSpaceStringAttribute");

		// Assert — one attribute is emitted (a duplicate hint name would fail the generator) and it keeps the
		// open generic mapping.
		await Assert.That(generated).ContainsGeneratedCode("class NonWhiteSpaceStringAttribute");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::Testing.Rules.NonWhiteSpaceStringRule<>)");
		driverResult.AssertNoGenerationExceptions();
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenAllowMultipleMarker_MirrorsItOnTheGeneratedAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange — one rule opts into repeated application, one keeps the AttributeUsage default.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule(AllowMultiple = true)]
				public readonly record struct RepeatingRule(int Step = 1, string? Message = null) : IValidationRule<int>
				{
					public bool IsValid(in int value) => Step != 0 && value % Step == 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid.";
				}

				[ZodRule]
				public readonly record struct SingleRule(int Step = 1, string? Message = null) : IValidationRule<int>
				{
					public bool IsValid(in int value) => Step != 0 && value % Step == 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid.";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var repeating = driverResult.GetSource("RepeatingAttribute");
		var single = driverResult.GetSource("SingleAttribute");

		// Assert
		await Assert.That(repeating).ContainsGeneratedCode("AllowMultiple = true");
		await Assert.That(single).ContainsGeneratedCode("AllowMultiple = false");
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenAttributeAppliedTwice_EmitsEveryApplicationAsItsOwnRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a hand-authored attribute that permits repeated application.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Testing
			{
				public readonly record struct MultipleOfRule(int Factor = 1, string? Message = null)
					: IValidationRule<int>
				{
					public bool IsValid(in int value) => Factor != 0 && value % Factor == 0;

					public string GetErrorMessage(in int value) => Message ?? "Not a multiple.";
				}

				[ZodRule(typeof(MultipleOfRule), Code = "not_multiple")]
				[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
				public sealed class MultipleOfAttribute : ValidationAttribute
				{
					public int Factor { get; set; }
				}

				[ZodSchema]
				public partial class Sample
				{
					[MultipleOf(Factor = 3)]
					[MultipleOf(Factor = 5)]
					public int Value { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("SampleSchema");
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.Sample")!;
		var validate = assembly.GetType("Testing.SampleSchema")!.GetMethod("Validate")!;

		// Both applications are emitted as their own rule, in source order.
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.MultipleOfRule(3, null!)");
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.MultipleOfRule(5, null!)");

		// Act — 15 satisfies both applications, 3 satisfies only the first.
		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Value")!.SetValue(validInstance, 15);
		var validResult = validate.Invoke(null, [validInstance])!;

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Value")!.SetValue(invalidInstance, 3);
		var invalidResult = validate.Invoke(null, [invalidInstance])!;

		// Assert
		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();

		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			invalidResult.GetType().GetProperty("Errors")!.GetValue(invalidResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("not_multiple");
	}

	/// <summary>
	/// A rule family whose members share the name the attribute encodes: the non-generic primitive rule and an
	/// arity-1 generic value-object rule, with a placeholder for the attribute's mapping.
	/// </summary>
	const string RuleFamilyAttributeSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp.Core;

		namespace Testing.Rules
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

			public readonly record struct NonWhiteSpaceStringRule<TSelf>(string? Message = null)
				: IValidationRule<TSelf>
				where TSelf : IScalarValueObject<TSelf, string>
			{
				public bool IsValid(in TSelf value) => value.Value != null && !string.IsNullOrWhiteSpace(value.Value);

				public string GetErrorMessage(in TSelf value) => Message ?? "Value must not be empty.";
			}

			[MAPPING]
			[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
			public sealed class NonWhiteSpaceStringAttribute : ValidationAttribute { }
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenMappingThatSkipsTheGenericSibling_ReportsIncompleteCoverage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the attribute name encodes `NonWhiteSpaceStringRule`, but the mapping declares only the
		// non-generic member while an arity-1 generic sibling also exists.
		var source = RuleFamilyAttributeSource.Replace(
			"[MAPPING]",
			"[ZodRule(typeof(NonWhiteSpaceStringRule))]",
			StringComparison.Ordinal
		);

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert — the generic half of the family is not covered by the declared mapping.
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN038");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenMappingToOpenGeneric_ReportsNoIncompleteCoverage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the open generic addresses both the primitive member (through the non-generic sibling) and
		// the scalar value object, so the family is covered.
		var source = RuleFamilyAttributeSource.Replace(
			"[MAPPING]",
			"[ZodRule(typeof(NonWhiteSpaceStringRule<>))]",
			StringComparison.Ordinal
		);

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN038");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenMappingToUnrelatedRule_ReportsIncompleteCoverage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — `WidgetAttribute` encodes `WidgetRule`, but the mapping declares an unrelated rule.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				public readonly record struct WidgetRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid widget.";
				}

				public readonly record struct GadgetRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid gadget.";
				}

				[ZodRule(typeof(GadgetRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class WidgetAttribute : ValidationAttribute { }
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN038");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenFreeFormAttributeName_ReportsNoIncompleteCoverage(
		CancellationToken cancellationToken
	)
	{
		// Arrange — `NoSpacesAttribute` does not encode a declared rule name, so any mapping is accepted.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.IndexOf(' ') < 0;

					public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
				}

				[ZodRule(typeof(NoWhitespaceRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class NoSpacesAttribute : ValidationAttribute { }
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN038");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenIdentityParameterWithoutZodRule_ReportsInertParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the rule accepts `Code`, but only IZodRule surfaces it, so the value is inert.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct RankRule(string? Code = null, string? Message = null) : IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("RankAttribute");

		// Assert — the warning is raised and the attribute is still generated.
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN039");
		await Assert.That(generated).ContainsGeneratedCode("class RankAttribute");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenIdentityParameterWithZodRule_ReportsNoInertParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the same rule, now owning its identity.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct RankRule(string? Code = null, string? Message = null)
					: IValidationRule<int>,
						IZodRule
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";

					string? IZodRule.Code => Code;

					string? IZodRule.Origin => "ranking";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN039");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenAttributeImplementingIdentityContract_EmitsDeclaredIdentity(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the attribute implements IZodRuleAttribute, so the identity properties are compiler-enforced
		// and read from the applied attribute.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
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
				public partial class Sample
				{
					[Rank(Code = "invalid_rank", Origin = "ranking")]
					public int Value { get; set; }
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("SampleSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("\"invalid_rank\"");
		await Assert.That(generated).ContainsGeneratedCode("\"ranking\"");
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenMappedRuleWithIdentityParameter_ReportsInertParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the rule is only ever mapped (never marked) and accepts `Code` without implementing IZodRule.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				public readonly record struct RankRule(string? Code = null, string? Message = null)
					: IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}

				[ZodRule(typeof(RankRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RankAttribute : ValidationAttribute { }
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN039");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenMarkedAndMappedRuleWithIdentityParameter_ReportsOnce(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the rule is marked (checked at its declaration) and mapped (checked at the attribute), so the
		// mapping path must not report the same rule twice.
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct RankRule(string? Code = null, string? Message = null)
					: IValidationRule<int>
				{
					public bool IsValid(in int value) => value > 0;

					public string GetErrorMessage(in int value) => Message ?? "Invalid rank.";
				}

				[ZodRule(typeof(RankRule))]
				[AttributeUsage(AttributeTargets.Property)]
				public sealed class RankAttribute : ValidationAttribute { }
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).HasDiagnostics("ZODSGEN039", 1);
	}
}

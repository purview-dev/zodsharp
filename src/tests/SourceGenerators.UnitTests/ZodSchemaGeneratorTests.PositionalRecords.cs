using System.Collections.Immutable;
using System.Reflection;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Validation attributes written on a positional record parameter are applied to the parameter, not to the
/// synthesized property, whenever the attribute is valid on both targets. The generator must therefore merge
/// the parameter's attributes into the property's, or every rule on a positional record is silently dropped.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// A <c>[ZodSchema]</c> positional record carrying a rule attribute on a <see cref="DateTimeOffset"/>
	/// parameter: the case that reported <c>NonSentinelRule&lt;DateTimeOffset&gt;</c> failures as passing.
	/// </summary>
	const string NonSentinelDateTimeOffsetSource = """
		using System;
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			[ZodSchema]
			public sealed record Summary(
				[NonSentinel]
				DateTimeOffset LastObservedAt
			);
		}
		""";

	[Test]
	public async Task Generate_GivenNonSentinelOnPositionalDateTimeOffsetParameter_ClosesTheRuleWithThePropertyType(
		CancellationToken cancellationToken
	)
	{
		// Act
		using var driverResult = await GenerateAsync(NonSentinelDateTimeOffsetSource, cancellationToken);
		var generated = driverResult.GetSource("SummarySchema");

		// Assert - the open generic is closed with the property's own type, not dropped.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.DateTimeOffset>(null!, null!)"
			);
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN006");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN030");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN031");
	}

	[Test]
	public async Task Generate_GivenNonSentinelOnEverySentinelParameterType_ClosesTheRuleWithEachType(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Identifiers(
					[NonSentinel]
					Guid Id,
					[NonSentinel]
					DateTime When,
					[NonSentinel]
					DateTimeOffset LastObservedAt,
					[NonSentinel]
					DateOnly Day,
					[NonSentinel]
					TimeOnly Hour
				);
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("IdentifiersSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.DateTime>(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.DateTimeOffset>(null!, null!)"
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.DateOnly>(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.TimeOnly>(null!, null!)");
	}

	[Test]
	public async Task Generate_GivenNonSentinelOnPositionalRecordParameter_CarriesTheMessageOverride(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary(
					[NonSentinel(Message = "Last observed must not be the default.")]
					DateTimeOffset LastObservedAt
				);
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("SummarySchema");

		// Assert - the named Message argument flows into the rule like it does for a declared property.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"""new global::ZodSharp.Rules.NonSentinelRule<global::System.DateTimeOffset>("Last observed must not be the default.", null!)"""
			);
	}

	[Test]
	public async Task Generate_GivenBothTargetedAndPropertyTargetedNonSentinel_EmitsTheRuleOnce(
		CancellationToken cancellationToken
	)
	{
		// Arrange - `[NonSentinel]` lands on the parameter and `[property: NonSentinel]` on the property, so the
		// merged set must not apply the same attribute type twice.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary(
					[NonSentinel]
					[property: NonSentinel]
					DateTimeOffset LastObservedAt
				);
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("SummarySchema")!;

		// Assert — the merged attribute set must not apply the same rule twice. The rule is constructed once
		// in Validate and once in BuildJsonSchema (the exported JSON Schema), so a duplicate application would
		// show up as four occurrences rather than two.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.DateTimeOffset>(null!, null!)"
			);
		await Assert.That(CountOccurrences(generated, "NonSentinelRule<global::System.DateTimeOffset>")).IsEqualTo(2);
	}

	[Test]
	public async Task Generate_GivenDataAnnotationsOnPositionalRecordParameters_ResolvesThem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Account(
					[Display(Name = "Account name")]
					[Required]
					string Name,
					[StringLength(10, MinimumLength = 3)]
					string Handle,
					[Range(typeof(DateTimeOffset), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					DateTimeOffset Window
				);
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AccountSchema");

		// Assert - the display name, the length bounds and the parsed DateTimeOffset bounds all come from the
		// parameters, so the emitted validation is the one a declared property would get.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("Field 'Account name' is required and cannot be null, empty or whitespace");
		await Assert.That(generated).ContainsGeneratedCode("if (handleLength < 3)");
		await Assert.That(generated).ContainsGeneratedCode("if (handleLength > 10)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"private static readonly global::System.DateTimeOffset RangeMinimum_Window = global::System.DateTimeOffset.Parse(\"2020-01-01\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
	}

	/// <summary>
	/// The acceptance path for the reported failure: a <see cref="DateTimeOffset"/> positional record parameter
	/// set to <c>default</c> must be rejected by the generated validator.
	/// </summary>
	[Test]
	public async Task GeneratedValidate_GivenDefaultDateTimeOffsetOnPositionalRecord_RejectsTheSummary(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileSummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.Summary")!;
		var summary = Activator.CreateInstance(modelType, [default(DateTimeOffset)])!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
		var errors = GetErrors(result);
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo(Rules.NonSentinelRule<DateTimeOffset>.ErrorCode);
		await Assert.That(errors[0].Path.Length).IsEqualTo(1);
		await Assert.That(errors[0].Path[0]).IsEqualTo("LastObservedAt");
	}

	[Test]
	public async Task GeneratedValidate_GivenDateTimeOffsetBoundsOnPositionalRecord_RejectsBothBounds(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileSummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.Summary")!;

		object?[][] bounds =
		[
			[DateTimeOffset.MinValue],
			[DateTimeOffset.MaxValue],
		];

		foreach (var bound in bounds)
		{
			var summary = Activator.CreateInstance(modelType, bound)!;

			// Act
			var result = InvokeValidate(validate, summary);

			// Assert
			await Assert.That(IsSuccess(result)).IsFalse();
		}
	}

	[Test]
	public async Task GeneratedValidate_GivenSetDateTimeOffsetOnPositionalRecord_AcceptsTheSummary(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileSummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.Summary")!;
		var summary = Activator.CreateInstance(modelType, [new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)])!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsTrue();
	}

	/// <summary>
	/// A positional record shaped like the reported one: every member carries a rule, so only the member whose
	/// value is a sentinel is rejected.
	/// </summary>
	const string InventorySummarySource = """
		using System;
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			public enum Visibility
			{
				Public,
				Private,
			}

			[ZodSchema]
			public sealed record InventorySummary(
				[NonSentinel]
				string Name,
				[NonSentinel]
				string CanonicalReference,
				[NullOrNonWhiteSpace]
				string? DefaultBranch,
				Visibility Visibility,
				bool IsArchived,
				[NonSentinel]
				DateTimeOffset LastObservedAt
			);
		}
		""";

	[Test]
	public async Task GeneratedValidate_GivenFullPositionalRecord_RejectsOnlyTheSentinelMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileInventorySummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.InventorySummary")!;
		var summary = Activator.CreateInstance(
			modelType,
			[
				"changeops",
				"purview-dev/changeops",
				null,
				Enum.ToObject(modelType.GetProperty("Visibility")!.PropertyType, 1),
				false,
				default(DateTimeOffset),
			]
		)!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
		var errors = GetErrors(result);
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo(Rules.NonSentinelRule<DateTimeOffset>.ErrorCode);
		await Assert.That(errors[0].Path[0]).IsEqualTo("LastObservedAt");
	}

	[Test]
	public async Task GeneratedValidate_GivenFullPositionalRecord_WithValidValuesAcceptsIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileInventorySummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.InventorySummary")!;
		var summary = Activator.CreateInstance(
			modelType,
			[
				"changeops",
				"purview-dev/changeops",
				null,
				Enum.ToObject(modelType.GetProperty("Visibility")!.PropertyType, 1),
				false,
				new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
			]
		)!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenFullPositionalRecord_RejectsASentinelStringToo(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileInventorySummarySchema(cancellationToken);
		var modelType = assembly.GetType("Testing.InventorySummary")!;
		var summary = Activator.CreateInstance(
			modelType,
			[
				string.Empty,
				"purview-dev/changeops",
				null,
				Enum.ToObject(modelType.GetProperty("Visibility")!.PropertyType, 1),
				false,
				new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
			]
		)!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
		var errors = GetErrors(result);
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Path[0]).IsEqualTo("Name");
	}

	[Test]
	public async Task GeneratedValidate_GivenNonSentinelOnPositionalStringParameter_RejectsAnEmptyValue(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Named([NonSentinel] string Name);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.NamedSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Named")!;

		var emptyResult = InvokeValidate(validate, Activator.CreateInstance(modelType, [string.Empty])!);
		var namedResult = InvokeValidate(validate, Activator.CreateInstance(modelType, ["changeops"])!);

		// Assert
		await Assert.That(IsSuccess(emptyResult)).IsFalse();
		await Assert.That(IsSuccess(namedResult)).IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenNullOrNonWhiteSpaceOnPositionalParameter_AcceptsNullButNotWhitespace(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Branch([NullOrNonWhiteSpace] string? DefaultBranch);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.BranchSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Branch")!;

		var nullResult = InvokeValidate(validate, Activator.CreateInstance(modelType, [null])!);
		var whitespaceResult = InvokeValidate(validate, Activator.CreateInstance(modelType, ["   "])!);

		// Assert
		await Assert.That(IsSuccess(nullResult)).IsTrue();
		await Assert.That(IsSuccess(whitespaceResult)).IsFalse();
	}

	[Test]
	public async Task GeneratedValidate_GivenPropertyTargetedNonSentinel_ReportsASingleError(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the explicit `property:` target already puts the attribute on the property, so merging the
		// parameter's copy must not add a second rule.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary(
					[NonSentinel]
					[property: NonSentinel]
					DateTimeOffset LastObservedAt
				);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.SummarySchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Summary")!;
		var summary = Activator.CreateInstance(modelType, [default(DateTimeOffset)])!;

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(GetErrors(result)).HasSingleItem();
	}

	[Test]
	public async Task GeneratedValidate_GivenRequiredOnPositionalRecordParameter_ReportsMissingField(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Account([Required] string Name);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.AccountSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Account")!;
		var missing = Activator.CreateInstance(modelType, [null])!;

		// Act
		var result = InvokeValidate(validate, missing);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
		await Assert
			.That(GetErrors(result).Any(static error => error.Path.Contains("Name") && error.Code == "missing_field"))
			.IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenDisplayOnPositionalRecordParameter_ReportsTheDisplayName(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Account(
					[Display(Name = "Account name")]
					[Required]
					string Name
				);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.AccountSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Account")!;
		var missing = Activator.CreateInstance(modelType, [null])!;

		// Act
		var result = InvokeValidate(validate, missing);

		// Assert
		await Assert.That(GetErrors(result)[0].Message).Contains("Account name");
	}

	[Test]
	public async Task GeneratedValidate_GivenRangeOnPositionalDateTimeOffsetParameter_EnforcesTheBounds(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Window(
					[Range(typeof(DateTimeOffset), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					DateTimeOffset When
				);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.WindowSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Window")!;

		var inRange = Activator.CreateInstance(modelType, [new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)])!;
		var outOfRange = Activator.CreateInstance(modelType, [new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero)])!;

		// Act
		var inRangeResult = InvokeValidate(validate, inRange);
		var outOfRangeResult = InvokeValidate(validate, outOfRange);

		// Assert
		await Assert.That(IsSuccess(inRangeResult)).IsTrue();
		await Assert.That(IsSuccess(outOfRangeResult)).IsFalse();
	}

	[Test]
	public async Task GeneratedValidate_GivenRecordStructWithPositionalParameters_ValidatesItsMembers(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public readonly record struct Observation(
					[NonSentinel]
					DateTimeOffset LastObservedAt,
					[NonSentinel]
					Guid Id
				);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.ObservationSchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Observation")!;

		var invalid = Activator.CreateInstance(modelType, [default(DateTimeOffset), Guid.Empty])!;
		var valid = Activator.CreateInstance(
			modelType,
			[new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero), Guid.NewGuid()]
		)!;

		// Act
		var invalidResult = InvokeValidate(validate, invalid);
		var validResult = InvokeValidate(validate, valid);

		// Assert
		await Assert.That(IsSuccess(invalidResult)).IsFalse();
		await Assert.That(IsSuccess(validResult)).IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenNonPositionalRecord_KeepsValidatingItsDeclaredProperties(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a record whose properties are declared in the body is unaffected by the merge.
		const string source = """
			using System;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed record Summary
				{
					[NonSentinel]
					public DateTimeOffset LastObservedAt { get; init; }
				}
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.SummarySchema", cancellationToken);
		var modelType = assembly.GetType("Testing.Summary")!;
		var summary = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("LastObservedAt")!.SetValue(summary, default(DateTimeOffset));

		// Act
		var result = InvokeValidate(validate, summary);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
	}

	[Test]
	public async Task GeneratedValidate_GivenNestedSchemaAsPositionalParameter_ReportsTheNestedErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the nested schema is discovered through the positional parameter's data annotation.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public sealed class Customer
				{
					[Required]
					public string Name { get; set; } = string.Empty;
				}

				[ZodSchema]
				public sealed record Order([Required] Customer Customer);
			}
			""";

		var (assembly, validate) = await CompileAsync(source, "Testing.OrderSchema", cancellationToken);
		var customerType = assembly.GetType("Testing.Customer")!;
		var orderType = assembly.GetType("Testing.Order")!;

		var invalidCustomer = Activator.CreateInstance(customerType)!;
		customerType.GetProperty("Name")!.SetValue(invalidCustomer, null);

		var validCustomer = Activator.CreateInstance(customerType)!;
		customerType.GetProperty("Name")!.SetValue(validCustomer, "changeops");

		// Act
		var invalidResult = InvokeValidate(validate, Activator.CreateInstance(orderType, [invalidCustomer])!);
		var validResult = InvokeValidate(validate, Activator.CreateInstance(orderType, [validCustomer])!);

		// Assert
		await Assert.That(IsSuccess(invalidResult)).IsFalse();
		await Assert.That(IsSuccess(validResult)).IsTrue();
	}

	/// <summary>
	/// A custom <c>[ZodRule]</c>-mapped attribute written on a positional record parameter, the shape a
	/// project-local rule such as <c>[NullOrValidString]</c> takes.
	/// </summary>
	const string CustomRuleOnPositionalParameterSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Testing
		{
			public readonly record struct NoWhitespaceRule(string? Message = null) : IValidationRule<string>
			{
				public bool IsValid(in string value) => value.IndexOf(' ') < 0;

				public string GetErrorMessage(in string value) => Message ?? "Whitespace is not allowed.";
			}

			[ZodRule(typeof(NoWhitespaceRule), Code = "invalid_string", Origin = "string")]
			[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
			public sealed class NoWhitespaceAttribute : ValidationAttribute
			{
				public string? Message { get; set; }
			}

			[ZodSchema]
			public sealed record User([NoWhitespace(Message = "No spaces allowed.")] string Name);
		}
		""";

	[Test]
	public async Task Generate_GivenCustomRuleAttributeOnPositionalRecordParameter_EmitsTheRule(
		CancellationToken cancellationToken
	)
	{
		// Act
		using var driverResult = await GenerateAsync(CustomRuleOnPositionalParameterSource, cancellationToken);
		var generated = driverResult.GetSource("UserSchema");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("new global::Testing.NoWhitespaceRule(");
		await Assert.That(generated).ContainsGeneratedCode("\"No spaces allowed.\"");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN030");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN031");
	}

	[Test]
	public async Task GeneratedValidate_GivenCustomRuleAttributeOnPositionalRecordParameter_FailsValidationAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var (assembly, validate) = await CompileAsync(
			CustomRuleOnPositionalParameterSource,
			"Testing.UserSchema",
			cancellationToken
		);
		var modelType = assembly.GetType("Testing.User")!;
		var instance = Activator.CreateInstance(modelType, ["John Doe"])!;

		// Act
		var result = InvokeValidate(validate, instance);

		// Assert
		await Assert.That(IsSuccess(result)).IsFalse();
		var errors = GetErrors(result);
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("invalid_string");
		await Assert.That(errors[0].Origin).IsEqualTo("string");
	}

	async Task<(Assembly Assembly, MethodInfo Validate)> CompileSummarySchema(CancellationToken cancellationToken) =>
		await CompileAsync(NonSentinelDateTimeOffsetSource, "Testing.SummarySchema", cancellationToken);

	async Task<(Assembly Assembly, MethodInfo Validate)> CompileInventorySummarySchema(
		CancellationToken cancellationToken
	) => await CompileAsync(InventorySummarySource, "Testing.InventorySummarySchema", cancellationToken);

	async Task<(Assembly Assembly, MethodInfo Validate)> CompileAsync(
		string source,
		string schemaTypeName,
		CancellationToken cancellationToken
	)
	{
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var validate = assembly.GetType(schemaTypeName)!.GetMethod("Validate")!;
		return (assembly, validate);
	}

	static object InvokeValidate(MethodInfo validate, object value) => validate.Invoke(null, [value])!;

	static bool IsSuccess(object result) => (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;

	static ImmutableArray<Core.ValidationError> GetErrors(object result) =>
		(ImmutableArray<Core.ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

	static int CountOccurrences(string text, string needle)
	{
		var count = 0;
		var index = 0;

		while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += needle.Length;
		}

		return count;
	}
}

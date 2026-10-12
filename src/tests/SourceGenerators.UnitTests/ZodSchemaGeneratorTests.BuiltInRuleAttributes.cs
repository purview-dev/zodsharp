using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// A rule that a consumer would write under the shipped <c>ZodSharp.Rules</c> namespace, used to exercise the
	/// built-in classification without depending on the runtime assembly's actual rule set.
	/// </summary>
	[Test]
	public async Task RuleAttributeGeneration_GivenBuiltInRuleCollidingWithDataAnnotations_GeneratesSuffixedAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange - `RequiredRule` would derive `RequiredAttribute`, which System.ComponentModel.DataAnnotations
		// already ships (and which already maps to the equivalent built-in rule).
		const string source = """
			using ZodSharp.Core;

			namespace ZodSharp.Rules
			{
				[ZodRule]
				public readonly record struct RequiredRule(string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Value is required.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("RequiredZodAttribute");

		// Assert - the DataAnnotations name is left alone and the rule's attribute is emitted under the "Zod"
		// suffix, so its identity and message stay usable.
		await Assert.That(generated).ContainsGeneratedCode("class RequiredZodAttribute");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::ZodSharp.Rules.RequiredRule)");
		await Assert.That(generated).ContainsGeneratedCode("public string? Message { get; set; } = null;");
		await Assert.That(driverResult.GetSource("RequiredAttribute")).IsNull();
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN043");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN032");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenGenericBoundRule_MirrorsTheDoubleBound(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a generic bound rule; the type-parameter bound is surfaced as a double on the attribute.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct ThresholdRule<T>(T threshold) : IValidationRule<T>
					where T : IComparable<T>
				{
					public bool IsValid(in T value) => value.CompareTo(threshold) >= 0;

					public string GetErrorMessage(in T value) => "Too small.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ThresholdAttribute");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("public double Threshold { get; set; } = default!;");
		await Assert.That(generated).ContainsGeneratedCode("typeof(global::Testing.Rules.ThresholdRule<>)");
		// The required bound becomes a constructor parameter, so the attribute cannot be applied without it.
		await Assert.That(generated).ContainsGeneratedCode("public ThresholdAttribute(double threshold)");
		await Assert.That(generated).ContainsGeneratedCode("Threshold = threshold;");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN043");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN032");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenRequiredValue_GeneratesAConstructorParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a rule whose value parameter has no default: the attribute must demand it.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct UpperBoundRule<T>(T maxValue, string? message = null)
					: IValidationRule<T>
					where T : IComparable<T>
				{
					public bool IsValid(in T value) => value.CompareTo(maxValue) <= 0;

					public string GetErrorMessage(in T value) => message ?? "Too big.";
				}
			}
			""";

		// Act
		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generated = driverResult.GetSource("UpperBoundAttribute");
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var attributeType = assembly.GetType("Testing.Rules.UpperBoundAttribute")!;

		// Assert - the constructor takes the required value and the message stays a named property.
		await Assert.That(generated).ContainsGeneratedCode("public UpperBoundAttribute(double maxValue)");
		await Assert.That(generated).ContainsGeneratedCode("MaxValue = maxValue;");
		await Assert.That(generated).ContainsGeneratedCode("public string? Message { get; set; } = null;");
		await Assert.That(attributeType.GetConstructor([typeof(double)])).IsNotNull();
		await Assert.That(attributeType.GetConstructor([])).IsNull();
		driverResult.AssertNoCompilationErrors();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenMissingRequiredValue_FailsToCompile(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the same attribute applied without its required value.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public partial class Sample
				{
					[LessThanOrEqual]
					public int Value { get; set; }
				}
			}
			""";

		// Act - the harness normally rejects a run whose compilation has errors, so validation is disabled here
		// to assert the error explicitly.
		var driverResult = await GenerateAsync(source, ZodSourceGeneratorTestOptions.NoValidation, cancellationToken);

		// Assert - a missing required value is a compile error at the attribute's usage site (CS7036), not a
		// generator diagnostic that only surfaces when a schema is generated.
		var diagnostics = driverResult.CompilationResult.Compilation.GetDiagnostics(cancellationToken);
		await Assert.That(diagnostics).Contains(d => d.Id == "CS7036");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenOptionalValueOnly_KeepsTheParameterlessConstructor(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a rule whose only constructor parameter is the message: nothing is required.
		const string source = """
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct WholeRule<T>(string? message = null) : IValidationRule<T>
					where T : System.Numerics.INumber<T>
				{
					public bool IsValid(in T value) => value % T.One == T.Zero;

					public string GetErrorMessage(in T value) => message ?? "Expected a whole number.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("WholeAttribute");

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("public string? Message { get; set; } = null;");
		await Assert.That(generated).DoesNotContain("public WholeAttribute(");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenBuiltInRuleWithUnrepresentableParameter_ReportsZodsgen043(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a constructor parameter that cannot be represented as an attribute property.
		const string source = """
			using System;
			using ZodSharp.Core;

			namespace ZodSharp.Rules
			{
				[ZodRule]
				public readonly record struct BlobRule(Guid Blob, string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN043");
		await Assert.That(driverResult.GetSource("BlobAttribute")).IsNull();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenCustomRuleWithUnrepresentableParameter_ReportsZodsgen032(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the same shape, but authored by a consumer: it stays an error so the author notices.
		const string source = """
			using System;
			using ZodSharp.Core;

			namespace Testing.Rules
			{
				[ZodRule]
				public readonly record struct BlobRule(Guid Blob, string? Message = null) : IValidationRule<string>
				{
					public bool IsValid(in string value) => value.Length != 0;

					public string GetErrorMessage(in string value) => Message ?? "Invalid.";
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(driverResult).HasDiagnostic("ZODSGEN032");
		await Assert.That(driverResult).DoesNotHaveDiagnostic("ZODSGEN043");
	}

	/// <summary>
	/// A <c>[ZodSchema]</c> class that uses the validation attributes generated into the shipped
	/// <c>Purview.ZodSharp</c> assembly for the built-in rules.
	/// </summary>
	const string BuiltInAttributeSource = """
		using System;
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			[ZodSchema]
			public partial class Contact
			{
				[Email]
				public string Email { get; set; } = string.Empty;

				[E164]
				public string Phone { get; set; } = string.Empty;

				[Regex("^[a-z]+$")]
				public string Code { get; set; } = string.Empty;

				[NonSentinel(Message = "Value must not be the default.")]
				public Guid Id { get; set; }
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenShippedAttributes_ResolvesTheBuiltInRules(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(BuiltInAttributeSource, cancellationToken);
		var generated = driverResult.GetSource("ContactSchema");

		// Assert - each shipped attribute resolves to its built-in rule. The regex attribute mirrors the
		// string overload (not the Regex one) and the open generic is closed with the member type. Every
		// rule now takes (message, code), so an unset message/code is emitted as null!.
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.EmailRule(null!, null!)");
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.E164Rule(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.RegexRule(\"^[a-z]+$\", null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(\"Value must not be the default.\", null!)"
			);
		// The built-in rules own their identity, so the generated validation reads it from the rule.
		await Assert.That(generated).Contains("((global::ZodSharp.Core.IZodRule)");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenShippedAttributes_ReportsTheRuleErrorCode(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			BuiltInAttributeSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.Contact")!;
		var validate = assembly.GetType("Testing.ContactSchema")!.GetMethod("Validate")!;

		// Act - an invalid email address is rejected by the built-in EmailRule.
		var invalid = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Email")!.SetValue(invalid, "not-an-email");
		var invalidResult = validate.Invoke(null, [invalid])!;

		// Assert
		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			invalidResult.GetType().GetProperty("Errors")!.GetValue(invalidResult)!;
		await Assert.That(errors.Any(error => error.Code == Rules.EmailRule.ErrorCode)).IsTrue();
	}

	/// <summary>
	/// The acceptance path: a scalar value object annotated with the shipped <c>[NonSentinel]</c> attribute,
	/// adapted through the value-object scalar rule adapter.
	/// </summary>
	const string BuiltInScalarAttributeSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;
		using ZodSharp.Rules;

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
			[Purview.ValueObjects.Serialization.Scalar]
			[NonSentinel(Message = "TenantId must not be the default.")]
			[ZodSchema]
			public partial record struct TenantId : Purview.ValueObjects.IScalarValueObject<TenantId, Guid>
			{
				public Guid Value { get; init; }
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenShippedNonSentinelOnScalar_AdaptsAndKeepsTheRuleIdentity(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(BuiltInScalarAttributeSource, cancellationToken);
		var generated = driverResult.GetSource("TenantIdSchema");

		// Assert - the shipped open-generic attribute closes over the scalar's underlying value and the wrapped
		// built-in rule owns the reported identity.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::Purview.ValueObjects.ScalarRuleAdapter<global::Testing.TenantId, global::System.Guid, global::ZodSharp.Rules.NonSentinelRule<global::System.Guid>>("
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"((global::ZodSharp.Core.IZodRule)tenantIdCustomRuleInner0).Code ?? \"validation_failed\""
			);
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenShippedNonSentinelOnScalar_ReportsEmptyPathAndRuleCode(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			BuiltInScalarAttributeSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.TenantId")!;
		var validate = assembly.GetType("Testing.TenantIdSchema")!.GetMethod("Validate")!;

		// Act - the default Guid is Guid.Empty, which the built-in rule rejects.
		var invalidResult = validate.Invoke(null, [Activator.CreateInstance(modelType)!])!;

		// Assert - the error is reported at the scalar itself (empty path) with the built-in rule's own code.
		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			invalidResult.GetType().GetProperty("Errors")!.GetValue(invalidResult)!;
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo(Rules.NonSentinelRule<Guid>.ErrorCode);
		await Assert.That(errors[0].Path.Length).IsEqualTo(0);
		await Assert.That(errors[0].Message).IsEqualTo("TenantId must not be the default.");
	}

	/// <summary>
	/// A <c>[ZodSchema]</c> class that uses the new numeric rules, a generic bound rule closed with an
	/// <c>int</c>, and a collision rule's suffixed attribute.
	/// </summary>
	const string NewBuiltInAttributeSource = """
		using System;
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			[ZodSchema]
			public partial class Metrics
			{
				[MinValue(3)]
				public int Count { get; set; }

				[Even]
				public int EvenCount { get; set; }

				[GreaterThanOrEqual(1.5)]
				public double Ratio { get; set; }

				[LessThanOrEqual(10)]
				public int Bounded { get; set; }

				[MinLengthZod(3, Code = "too_short", Message = "Too short.")]
				public string Code { get; set; } = string.Empty;
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenNewAndCollisionAttributes_ResolvesTheRules(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(NewBuiltInAttributeSource, cancellationToken);
		var generated = driverResult.GetSource("MetricsSchema");

		// Assert - the generic bound rules are closed with the member type (their double bound converted to
		// int), the generic even rule is closed with int, and the suffixed length attribute carries code and
		// message. The inclusive bounds close with the member type too, so an int member works. Every rule now
		// takes (message, code), so an unset message/code is emitted as null!.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.MinValueRule<int>(3, null!, null!)");
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.EvenRule<int>(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.GreaterThanOrEqualRule<double>(1.5D, null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.LessThanOrEqualRule<int>(10, null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.MinLengthRule(3, \"Too short.\", \"too_short\")");
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenNewAndCollisionAttributes_ReportsTheirCodes(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			NewBuiltInAttributeSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.Metrics")!;
		var validate = assembly.GetType("Testing.MetricsSchema")!.GetMethod("Validate")!;

		// Act - the default metrics are all invalid; the even member is set odd and the upper bound is exceeded.
		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("EvenCount")!.SetValue(instance, 3);
		modelType.GetProperty("Bounded")!.SetValue(instance, 11);
		var result = validate.Invoke(null, [instance])!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;
		await Assert.That(errors.Any(error => error.Code == Rules.MinValueRule<int>.ErrorCode)).IsTrue();
		await Assert.That(errors.Any(error => error.Code == Rules.EvenRule<int>.ErrorCode)).IsTrue();
		await Assert.That(errors.Any(error => error.Code == Rules.GreaterThanOrEqualRule<double>.ErrorCode)).IsTrue();
		await Assert.That(errors.Any(error => error.Code == Rules.LessThanOrEqualRule<int>.ErrorCode)).IsTrue();
		await Assert.That(errors.Any(error => error.Code == "too_short")).IsTrue();
	}

	/// <summary>
	/// A <c>[ZodSchema]</c> class that uses the shipped attributes' optional value parameters (string
	/// comparison, IP family) alongside message/code overrides.
	/// </summary>
	const string OptionalValueAttributeSource = """
		using System;
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			[ZodSchema]
			public partial class Preferences
			{
				[StartsWith("https://", StringComparison.OrdinalIgnoreCase, Code = "bad_scheme")]
				public string Endpoint { get; set; } = string.Empty;

				[IPAddress(IPAddressRuleType.IPv4)]
				public string Address { get; set; } = string.Empty;

				[IPAddress]
				public string AnyAddress { get; set; } = string.Empty;

				[Email(Message = "Not an email.", Code = "bad_email")]
				public string Contact { get; set; } = string.Empty;
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenOptionalValueAttributes_ResolvesTheRules(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(OptionalValueAttributeSource, cancellationToken);
		var generated = driverResult.GetSource("PreferencesSchema");

		// Assert - the string comparison and IP family flow into the rule (rendered as their enum value) and
		// the message/code overrides are carried through.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.StartsWithRule(\"https://\", (global::System.StringComparison)5, null!, \"bad_scheme\")"
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.IPAddressRule((global::ZodSharp.Rules.IPAddressRuleType)0, null!, null!)"
			);
		// A bare [IPAddress] keeps the rule's Any default.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.IPAddressRule((global::ZodSharp.Rules.IPAddressRuleType)2, null!, null!)"
			);
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.EmailRule(\"Not an email.\", \"bad_email\")");
	}

	/// <summary>
	/// A <c>[ZodSchema]</c> class that applies the shipped <c>[UUID]</c> attribute both without a version and
	/// with an explicit RFC 9562 version. The rule overloads its constructor, so the attribute overloads too.
	/// </summary>
	const string UuidAttributeSource = """
		using ZodSharp;
		using ZodSharp.Rules;

		namespace Testing
		{
			[ZodSchema]
			public partial class Identifiers
			{
				[UUID]
				public string AnyUuid { get; set; } = string.Empty;

				[UUID(UuidVersion.V4)]
				public string V4Uuid { get; set; } = string.Empty;

				[UUID(Version = UuidVersion.V7)]
				public string V7Uuid { get; set; } = string.Empty;
			}
		}
		""";

	[Test]
	public async Task RuleAttributeGeneration_GivenVersionlessAndVersionedUuid_SelectsTheMatchingRuleOverload(
		CancellationToken cancellationToken
	)
	{
		// Act
		var driverResult = await GenerateAsync(UuidAttributeSource, cancellationToken);
		var generated = driverResult.GetSource("IdentifiersSchema");

		// Assert - the bare [UUID] maps to the versionless overload and [UUID(UuidVersion.V4)] to the versioned
		// one, rather than both collapsing to the widest constructor (which would demand a version for [UUID]).
		await Assert.That(generated).ContainsGeneratedCode("new global::ZodSharp.Rules.UUIDRule(null!, null!)");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.UUIDRule((global::ZodSharp.UuidVersion)4, null!, null!)"
			);
		// A version supplied through the property rather than positionally selects the versioned overload too.
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.UUIDRule((global::ZodSharp.UuidVersion)7, null!, null!)"
			);
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenVersionlessAndVersionedUuid_ValidatesWithTheMatchingOverload(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			UuidAttributeSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.Identifiers")!;
		var validate = assembly.GetType("Testing.IdentifiersSchema")!.GetMethod("Validate")!;

		// Act - each member receives a UUID of the version its attribute requires; the versionless member accepts
		// either. A V7 UUID supplied to the V4 member fails only that rule.
		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("AnyUuid")!.SetValue(validInstance, "550e8400-e29b-41d4-a716-446655440000");
		modelType.GetProperty("V4Uuid")!.SetValue(validInstance, "550e8400-e29b-41d4-a716-446655440000");
		modelType.GetProperty("V7Uuid")!.SetValue(validInstance, "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");
		var validResult = validate.Invoke(null, [validInstance])!;

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("AnyUuid")!.SetValue(invalidInstance, "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");
		modelType.GetProperty("V4Uuid")!.SetValue(invalidInstance, "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");
		modelType.GetProperty("V7Uuid")!.SetValue(invalidInstance, "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c");
		var invalidResult = validate.Invoke(null, [invalidInstance])!;

		// Assert
		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenUuidRule_GeneratedAttributeExposesBothRuleOverloads(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange - the shipped attribute is generated when the runtime assembly builds, so reflect over it here.
		var attributeType = typeof(Rules.UUIDAttribute);

		// Assert - the versionless and versioned rule overloads are both reachable through the attribute.
		await Assert.That(attributeType.GetConstructor([])).IsNotNull();
		await Assert.That(attributeType.GetConstructor([typeof(UuidVersion)])).IsNotNull();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenRequiredZodAttribute_ResolvesTheBuiltInRequiredRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the shipped RequiredZod attribute is the Zod-suffixed form of RequiredRule.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public partial class Account
				{
					[RequiredZod(AllowEmptyStrings = false, TrimWhitespace = true)]
					public string Name { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("AccountSchema");

		// Assert - the open generic is closed with the member type and carries both options.
		await Assert
			.That(generated)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.RequiredRule<string>(false, true, null!, null!)");
	}

	[Test]
	public async Task GeneratedValidate_GivenRequiredZodOnMissingValue_ReportsMissingFieldLikeRequired(
		CancellationToken cancellationToken
	)
	{
		// Arrange - [RequiredZod] must reject an absent value exactly as [Required] does, so the rule is
		// emitted outside the generator's non-null guard instead of being skipped.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public sealed class RequiredParityModel
				{
					[RequiredZod(AllowEmptyStrings = false, TrimWhitespace = true)]
					public string ZodName { get; set; }

					[Required(AllowEmptyStrings = false)]
					public string DataAnnotationName { get; set; }
				}
			}
			""";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.RequiredParityModel")!;
		var model = Activator.CreateInstance(modelType)!;

		// Act
		var result = InvokeValidate(assembly, model, "Testing.RequiredParityModelSchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert
			.That(errors.Any(static error => error.Path.Contains("ZodName") && error.Code == "missing_field"))
			.IsTrue();
		await Assert
			.That(
				errors.Any(static error => error.Path.Contains("DataAnnotationName") && error.Code == "missing_field")
			)
			.IsTrue();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenAllowedValuesZodAttribute_ClosesTheArrayWithTheMemberType(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the params object[] attribute form supplies every allowed value.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public partial class Choice
				{
					[AllowedValuesZod("a", "b", "c")]
					public string Code { get; set; } = string.Empty;
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("ChoiceSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.AllowedValuesRule<string>(new string[] { \"a\", \"b\", \"c\" }, null!, null!)"
			);
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenAllowedValuesZodAttribute_ValidatesTheAllowedSet(
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
				public partial class Choice
				{
					[AllowedValuesZod("a", "b", "c")]
					public string Code { get; set; } = string.Empty;
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
		var modelType = assembly.GetType("Testing.Choice")!;
		var validate = assembly.GetType("Testing.ChoiceSchema")!.GetMethod("Validate")!;

		var validInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Code")!.SetValue(validInstance, "b");
		var validResult = validate.Invoke(null, [validInstance])!;

		var invalidInstance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Code")!.SetValue(invalidInstance, "z");
		var invalidResult = validate.Invoke(null, [invalidInstance])!;

		// Assert
		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
		await Assert.That((bool)invalidResult.GetType().GetProperty("IsSuccess")!.GetValue(invalidResult)!).IsFalse();
	}

	[Test]
	public async Task RuleAttributeGeneration_GivenDeniedValuesZodAttribute_ConvertsEachElementToTheMemberType(
		CancellationToken cancellationToken
	)
	{
		// Arrange - the elements are object-typed at the attribute, so the resolver converts each to int.
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public partial class Rating
				{
					[DeniedValuesZod(1, 2, 3)]
					public int Score { get; set; }
				}
			}
			""";

		// Act
		var driverResult = await GenerateAsync(source, cancellationToken);
		var generated = driverResult.GetSource("RatingSchema");

		// Assert
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"new global::ZodSharp.Rules.DeniedValuesRule<int>(new int[] { 1, 2, 3 }, null!, null!)"
			);
	}
}

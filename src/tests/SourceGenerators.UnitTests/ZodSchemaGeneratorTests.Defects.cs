using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// One regression test per generator defect in the error-model register (G1-G24). Each test failed before
/// its fix.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	const string RequiredSemanticsSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing
		{
			[ZodSchema]
			public class Contact
			{
				[Required]
				public string? Name { get; set; }

				[Required(AllowEmptyStrings = true)]
				public string? Nickname { get; set; }
			}
		}
		""";

	// Defect G1
	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task Required_GivenDefaultAllowEmptyStringsAndMissingText_ReportsMissingField(
		string? name,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			RequiredSemanticsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var contact = Create(assembly, "Testing.Contact", ("Name", name), ("Nickname", "nick"));

		// Act
		var errors = RunValidate(assembly, "Testing.ContactSchema", contact);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(errors[0].Message).IsEqualTo("Name is required");
	}

	// Defect G1
	[Test]
	[Arguments("", 0)]
	[Arguments("   ", 0)]
	[Arguments(null, 1)]
	public async Task Required_GivenAllowEmptyStrings_RejectsOnlyNull(
		string? nickname,
		int expectedErrors,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			RequiredSemanticsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var contact = Create(assembly, "Testing.Contact", ("Name", "Ada"), ("Nickname", nickname));

		// Act
		var errors = RunValidate(assembly, "Testing.ContactSchema", contact);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(expectedErrors);
	}

	// Defect G2
	[Test]
	public async Task Required_GivenErrorMessageWithUnknownPlaceholder_FormatsAtRuntimeWithoutThrowing(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the generator used to string.Format this message at generation time, which threw.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Ticket
				{
					[Required(ErrorMessage = "{0} needs {1} and a { brace")]
					public string? Title { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var ticket = Create(assembly, "Testing.Ticket");

		// Act
		var errors = RunValidate(assembly, "Testing.TicketSchema", ticket);

		// Assert — {0} binds the display name, the unknown {1} and the stray brace are written literally, and the
		// named template is kept for hosts that re-format it.
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Title needs {1} and a { brace");
		await Assert.That(errors[0].MessageFormat).IsEqualTo("{displayName} needs {1} and a { brace");
		await Assert.That(errors[0].Parameters!["displayName"]).IsEqualTo("Title");
		await Assert.That(errors[0].Parameters!["member"]).IsEqualTo("Title");
		await Assert.That(errors[0].Parameters!["declaringType"]).IsEqualTo("Testing.Ticket");
	}

	// Defect G3
	[Test]
	public async Task JsonNames_GivenJsonPropertyName_RendersJsonNamedPathWithoutReflection(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using System.Text.Json.Serialization;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Order
				{
					[JsonPropertyName("order_lines")]
					public List<Line> Lines { get; set; } = new();
				}

				[ZodSchema]
				public class Line
				{
					[Required]
					public string? ProductCode { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var path = ZodPath.From(
			ZodPathSegment.Member("Lines"),
			ZodPathSegment.At(1),
			ZodPathSegment.Member("ProductCode")
		);

		// Act
		var jsonName = InvokeStatic<string?>(assembly, "Testing.OrderSchema+JsonNames", "Get", "Lines");
		var renamed = InvokeStatic<ZodPath>(
			assembly,
			"Testing.OrderSchema",
			"ToJsonNamedPath",
			path,
			(Func<string, string>)System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName
		);

		// Assert
		await Assert.That(jsonName).IsEqualTo("order_lines");
		await Assert.That(renamed.ToDottedString()).IsEqualTo("order_lines[1].productCode");
	}

	// Defect G4
	[Test]
	public async Task Length_GivenCustomMessage_FormatsLikeLengthAttribute(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Code
				{
					[Length(2, 5, ErrorMessage = "{0}:{1}:{2}")]
					public string? Value { get; set; }
				}
			}
			""";
		var expected = new LengthAttribute(2, 5) { ErrorMessage = "{0}:{1}:{2}" }.FormatErrorMessage("Value");

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var code = Create(assembly, "Testing.Code", ("Value", "A"));

		// Act
		var errors = RunValidate(assembly, "Testing.CodeSchema", code);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo(expected);
	}

	// Defect G5
	[Test]
	public async Task StringLength_GivenTooShort_ReportsBothBoundsAndDirectionCategory(
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
				public class Code
				{
					[StringLength(5, MinimumLength = 2)]
					public string? Value { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var code = Create(assembly, "Testing.Code", ("Value", "A"));

		// Act
		var errors = RunValidate(assembly, "Testing.CodeSchema", code);

		// Assert — the runtime StringLengthRule's code (defect B3), both bounds, the count.
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo(Rules.StringLengthRule.ErrorCode);
		await Assert.That(errors[0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(errors[0].Lower).IsEqualTo(ErrorBound.Including(2));
		await Assert.That(errors[0].Upper).IsEqualTo(ErrorBound.Including(5));
		await Assert.That(errors[0].Parameters!["count"]).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Field 'Value' must contain at least 2 characters.");
	}

	// Defect G6
	[Test]
	public async Task Range_GivenExclusiveMinimum_ReportsTooSmallWithExclusiveLowerBound(
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
				public class Booking
				{
					[Range(1, 10, MinimumIsExclusive = true)]
					public int Nights { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var booking = Create(assembly, "Testing.Booking", ("Nights", 1));

		// Act
		var errors = RunValidate(assembly, "Testing.BookingSchema", booking);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_range");
		await Assert.That(errors[0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(errors[0].Origin).IsEqualTo("number");
		await Assert.That(errors[0].Lower).IsEqualTo(ErrorBound.Excluding(1));
		await Assert.That(errors[0].Upper).IsEqualTo(ErrorBound.Including(10));
		await Assert
			.That(errors[0].Message)
			.IsEqualTo("Field 'Nights' must be greater than 1 and less than or equal to 10.");
	}

	const string NestedCollectionsSource = """
		using System.Collections.Generic;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing
		{
			[ZodSchema]
			public class Order
			{
				[Required]
				public string? Number { get; set; }

				public Customer Customer { get; set; } = new();

				public List<Line> Lines { get; set; } = new();

				public Dictionary<string, Line> ByCode { get; set; } = new();
			}

			[ZodSchema]
			public class Customer
			{
				[Required]
				[MinLength(2)]
				public string? Name { get; set; }
			}

			[ZodSchema]
			public class Line
			{
				[Range(1, 10)]
				public int Quantity { get; set; } = 1;
			}
		}
		""";

	static object CreateInvalidOrder(System.Reflection.Assembly assembly, out dynamic order)
	{
		order = Create(assembly, "Testing.Order");
		order.Customer.Name = "A";
		order.Lines.Add((dynamic)Create(assembly, "Testing.Line"));
		order.Lines.Add((dynamic)Create(assembly, "Testing.Line", ("Quantity", 0)));
		order.Lines.Add(null);
		order.ByCode.Add("a.b", (dynamic)Create(assembly, "Testing.Line", ("Quantity", 11)));
		return order;
	}

	// Defect G7, G8, G9, G10
	[Test]
	public async Task NestedCollections_GivenInvalidElements_UseTypedIndexAndKeySegmentsAndKeepEveryField(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var order = CreateInvalidOrder(assembly, out _);

		// Act
		var errors = RunValidate(assembly, "Testing.OrderSchema", order);
		var byPath = errors.ToDictionary(static error => error.FullPath.ToDottedString(), StringComparer.Ordinal);

		// Assert — G7 index segments, G8 escaped key segments, G9 null-element origin, G10 every field kept.
		await Assert
			.That(byPath.Keys.Order(StringComparer.Ordinal).ToArray())
			.IsEquivalentTo(["ByCode[\"a.b\"].Quantity", "Customer.Name", "Lines[1].Quantity", "Lines[2]", "Number"]);
		await Assert.That(byPath["Lines[1].Quantity"].FullPath[1].Kind).IsEqualTo(ZodPathSegmentKind.Index);
		await Assert.That(byPath["Lines[1].Quantity"].Path.ToArray()).IsEquivalentTo(["Lines", "1", "Quantity"]);
		await Assert.That(byPath["ByCode[\"a.b\"].Quantity"].FullPath[1].Kind).IsEqualTo(ZodPathSegmentKind.Key);
		await Assert.That(byPath["Lines[2]"].Origin).IsEqualTo("array");
		await Assert.That(byPath["Lines[2]"].Code).IsEqualTo("invalid_type");
		await Assert.That(byPath["Customer.Name"].Origin).IsEqualTo("string");
		await Assert.That(byPath["Customer.Name"].Lower).IsEqualTo(ErrorBound.Including(2));
		await Assert.That(byPath["Customer.Name"].Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	// Defect G8
	[Test]
	public async Task Dictionary_GivenNumericKeysUnderAnotherCulture_RendersKeysInvariantly(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Prices
				{
					public Dictionary<double, Price> ByWeight { get; set; } = new();
				}

				[ZodSchema]
				public class Price
				{
					[Range(0, 100)]
					public int Amount { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		dynamic prices = Create(assembly, "Testing.Prices");
		prices.ByWeight.Add(1.5, (dynamic)Create(assembly, "Testing.Price", ("Amount", 500)));

		var culture = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
		try
		{
			// Act
			var errors = RunValidate(assembly, "Testing.PricesSchema", (object)prices);

			// Assert
			await Assert.That(errors.Length).IsEqualTo(1);
			await Assert.That(errors[0].FullPath[1].Name).IsEqualTo("1.5");
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}
	}

	// Defect G11, G12
	[Test]
	public async Task ValidateAsync_GivenNestedTypeWithCustomAsyncMethod_AwaitsNestedAndPrefixesItsErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Parent
				{
					public Child Child { get; set; } = new();
				}

				[ZodSchema]
				public class Child
				{
					public string? Code { get; set; }

					internal static ValueTask<ValidationResult<Child>> CustomValidationAsync(Child value, CancellationToken ct) =>
						ValueTask.FromResult(
							ValidationResult<Child>.Failure(ValidationError.Create("taken", "Code is taken", ChildSchema.Paths.Code))
						);
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var parent = Create(assembly, "Testing.Parent");

		// Act
		var asyncErrors = await RunValidationAsync(assembly, "Testing.ParentSchema", parent, null, cancellationToken);
		var syncErrors = RunValidate(assembly, "Testing.ParentSchema", parent);
		var requiresAsync = (bool)GetStaticField(assembly, "Testing.ParentSchema", "RequiresAsyncValidation")!;

		// Assert — the nested custom error used to be lost; it is now awaited and placed under Child.
		await Assert.That(asyncErrors.Length).IsEqualTo(1);
		await Assert.That(asyncErrors[0].Code).IsEqualTo("taken");
		await Assert.That(asyncErrors[0].FullPath.ToDottedString()).IsEqualTo("Child.Code");
		await Assert.That(syncErrors).IsEmpty();
		await Assert.That(requiresAsync).IsTrue();
	}

	// Defect G13
	[Test]
	public async Task Paths_GivenSchema_ExposesMemberPathConstantsForRefinements(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;
			using ZodSharp.Schemas;

			namespace Testing
			{
				[ZodSchema]
				public partial class Signup
				{
					public string? Password { get; set; }

					public string? Confirm { get; set; }

					partial void OnZodValidate(RefineCtx<Signup> context)
					{
						if (context.Value.Password != context.Value.Confirm)
							context.AddIssue("mismatch", "Passwords differ", SignupSchema.Paths.Confirm);
					}
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var signup = Create(assembly, "Testing.Signup", ("Password", "a"), ("Confirm", "b"));

		// Act
		var errors = RunValidate(assembly, "Testing.SignupSchema", signup);
		var path = (ZodPath)GetStaticField(assembly, "Testing.SignupSchema+Paths", "Confirm")!;

		// Assert
		await Assert.That(path).IsEqualTo(ZodPath.Member("Confirm"));
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath).IsEqualTo(path);
	}

	const string DefectCustomRuleSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Testing
		{
			public readonly struct NoSpacesRule : IValidationRule<string>
			{
				public const string ErrorCode = "no_spaces";
				public const string MessageFormat = "Spaces are not allowed";

				public string Code => ErrorCode;

				public bool IsValid(in string value) => !value.Contains(' ');

				public string GetErrorMessage(in string value) => MessageFormat;
			}

			[ZodRule(typeof(NoSpacesRule))]
			[AttributeUsage(AttributeTargets.Property)]
			public sealed class NoSpacesAttribute : ValidationAttribute
			{
			}

			[ZodSchema]
			public class Handle
			{
				[NoSpaces]
				public string? Plain { get; set; }

				[NoSpaces(ErrorMessage = "{0} has {1} spaces")]
				public string? Custom { get; set; }
			}
		}
		""";

	// Defect G14, G15, G16
	[Test]
	public async Task CustomRule_GivenMessagesAndNoOrigin_FormatsSafelyWithDisplayNameAndStringOrigin(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			DefectCustomRuleSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var handle = Create(assembly, "Testing.Handle", ("Plain", "a b"), ("Custom", "c d"));

		// Act
		var errors = RunValidate(assembly, "Testing.HandleSchema", handle)
			.ToDictionary(static error => error.FullPath.ToDottedString(), StringComparer.Ordinal);

		// Assert — G14: "{1}" used to throw FormatException at runtime; G15: the display name is a parameter;
		// G16: a string rule without an origin reports "string".
		await Assert.That(errors["Custom"].Message).IsEqualTo("Custom has {1} spaces");
		await Assert.That(errors["Plain"].Message).IsEqualTo("Spaces are not allowed");
		await Assert.That(errors["Plain"].Parameters!["displayName"]).IsEqualTo("Plain");
		await Assert.That(errors["Plain"].Origin).IsEqualTo("string");
		await Assert.That(errors["Plain"].Category).IsEqualTo(ZodIssueCategories.Custom);
	}

	// Defect G17, G18
	[Test]
	public async Task Enums_GivenAllowedValuesAndEnumRule_ReportMemberNamesAndEnumOrigin(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public enum Status { Draft, Active, Retired }

				[ZodSchema]
				public class Item
				{
					[AllowedValues(Status.Draft, Status.Active)]
					public Status Allowed { get; set; }

					public Status Defined { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var statusType = assembly.GetType("Testing.Status")!;
		var item = Create(
			assembly,
			"Testing.Item",
			("Allowed", Enum.ToObject(statusType, 2)),
			("Defined", Enum.ToObject(statusType, 42))
		);

		// Act
		var errors = RunValidate(assembly, "Testing.ItemSchema", item)
			.ToDictionary(static error => error.FullPath.ToDottedString(), StringComparer.Ordinal);

		// Assert
		await Assert.That(errors["Allowed"].Parameters!["options"]).IsEqualTo("Draft, Active");
		await Assert
			.That(errors["Allowed"].Message)
			.IsEqualTo("Field 'Allowed' must be one of the following values: Draft, Active.");
		await Assert.That(errors["Allowed"].Category).IsEqualTo(ZodIssueCategories.InvalidValue);
		await Assert.That(errors["Defined"].Origin).IsEqualTo("enum");
	}

	// Defect G19
	[Test]
	public async Task Display_GivenResourceType_ReadsTheLocalizedNameAtRuntime(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public static class Labels
				{
					public static string Current { get; set; } = "Full name";

					public static string FullName => Current;
				}

				[ZodSchema]
				public class Person
				{
					[Required]
					[Display(Name = nameof(Labels.FullName), ResourceType = typeof(Labels))]
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var person = Create(assembly, "Testing.Person");

		// Act
		var first = RunValidate(assembly, "Testing.PersonSchema", person);
		assembly.GetType("Testing.Labels")!.GetProperty("Current")!.SetValue(null, "Nom complet");
		var second = RunValidate(assembly, "Testing.PersonSchema", person);

		// Assert — the resource is read per failure (not baked in at compile time, not cached).
		await Assert.That(first[0].Message).IsEqualTo("Full name is required");
		await Assert.That(second[0].Message).IsEqualTo("Nom complet is required");
	}

	// Defect G20
	[Test]
	public async Task NonNullableMember_GivenNull_ReportsTheSharedNullValueIssue(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Profile
				{
					[MinLength(1)]
					public string Bio { get; set; } = string.Empty;
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var profile = Create(assembly, "Testing.Profile", ("Bio", null));

		// Act
		var errors = RunValidate(assembly, "Testing.ProfileSchema", profile);
		var rootErrors = RunValidate(assembly, "Testing.ProfileSchema", null);

		// Assert — the same identity the runtime schemas report.
		await Assert.That(errors[0].Message).IsEqualTo("Expected string, received null");
		await Assert.That(errors[0].Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(errors[0].MessageFormat).IsEqualTo(ZodMessages.NullValue);
		await Assert.That(rootErrors[0].Message).IsEqualTo("Expected object, received null");
	}

	// Defect G21
	[Test]
	public async Task OptionsValidator_GivenFailure_ReportsPathMessageAndCode(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public class SmtpOptions
				{
					[Range(1, 65535)]
					public int Port { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var options = Create(assembly, "Testing.SmtpOptions");
		dynamic validator = Activator.CreateInstance(assembly.GetType("Testing.SmtpOptionsValidator")!)!;

		// Act
		Microsoft.Extensions.Options.ValidateOptionsResult outcome = validator.Validate(null, (dynamic)options);

		// Assert
		await Assert.That(outcome.Failed).IsTrue();
		await Assert
			.That(outcome.Failures!.Single())
			.IsEqualTo(
				"Port: Field 'Port' must be greater than or equal to 1 and less than or equal to 65535. (invalid_range)"
			);
	}

	// Defect G23
	[Test]
	public async Task ValidatableObject_GivenMemberErrors_RunsOnlyAfterMembersPass(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Stay : IValidatableObject
				{
					[Range(1, 30)]
					public int Nights { get; set; } = 1;

					public int Guests { get; set; }

					public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
					{
						if (Guests > Nights * 4)
							yield return new ValidationResult("Too many guests", new[] { nameof(Guests) });
					}
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;

		// Act
		var objectErrors = RunValidate(assembly, "Testing.StaySchema", Create(assembly, "Testing.Stay", ("Guests", 9)));
		var memberErrors = RunValidate(
			assembly,
			"Testing.StaySchema",
			Create(assembly, "Testing.Stay", ("Nights", 0), ("Guests", 9))
		);

		// Assert — the validatable object is honoured (it was ignored), placed at its member names, and runs after
		// the members like DataAnnotations and Microsoft.Extensions.Validation.
		await Assert.That(objectErrors.Length).IsEqualTo(1);
		await Assert.That(objectErrors[0].FullPath.ToDottedString()).IsEqualTo("Guests");
		await Assert.That(objectErrors[0].Message).IsEqualTo("Too many guests");
		await Assert.That(memberErrors.Length).IsEqualTo(1);
		await Assert.That(memberErrors[0].FullPath.ToDottedString()).IsEqualTo("Nights");
	}

	// Defect G24
	[Test]
	public async Task Validate_GivenCyclicGraph_ThrowsAtTheMaximumDepth(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Node
				{
					[Required]
					public string? Name { get; set; } = "node";

					public Node? Next { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		dynamic node = Create(assembly, "Testing.Node");
		node.Next = node;
		object cyclic = node;

		// Act
		var exception = Unwrap(() => RunValidate(assembly, "Testing.NodeSchema", cyclic));

		// Assert — a cycle used to overflow the stack; it now fails like Microsoft.Extensions.Validation.
		await Assert.That(exception).IsTypeOf<InvalidOperationException>();
		await Assert
			.That(exception!.Message)
			.Contains($"Maximum validation depth of {ZodValidationDepth.MaxDepth} exceeded");
	}
}

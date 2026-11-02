using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

/// <summary>
/// Regression tests for the runtime-schema defects (A1-A21) of the error-model overhaul. Each test is named after
/// the behaviour it pins and tagged with the defect it would have caught.
/// </summary>
public class SchemaErrorModelTests
{
	static readonly string[] SingleTag = ["a"];

	[Test]
	public async Task ZodArray_GivenInvalidElement_KeepsEveryFieldOfTheElementError()
	{
		// Defect A1
		// Arrange
		var schema = Z.Array(Z.String().Min(3));

		// Act
		var result = schema.Validate(["ok-value", "x"]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.From(ZodPathSegment.At(1)));
		await Assert.That(error.Origin).IsEqualTo("string");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(error.Minimum).IsEqualTo(3);
		await Assert.That(error.Lower).IsEqualTo(ErrorBound.Including(3));
	}

	[Test]
	public async Task ZodObject_GivenInvalidField_KeepsEveryFieldOfTheFieldError()
	{
		// Defect A2
		// Arrange
		var schema = Z.Object().Field("name", Z.String().Max(2)).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "long" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("name");
		await Assert.That(error.Origin).IsEqualTo("string");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(error.Maximum).IsEqualTo(2);
	}

	[Test]
	public async Task ZodObject_GivenInvalidCatchallValue_KeepsEveryFieldOfTheError()
	{
		// Defect A2
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build().Catchall(new MaxLengthObjectSchema(2));

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "a", ["extra"] = "long" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("extra");
		await Assert.That(error.Origin).IsEqualTo("string");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooBig);
	}

	[Test]
	public async Task ZodArray_GivenMinAndMaxMessages_ReportsMessageForFailedBound()
	{
		// Defect A3
		// Arrange
		var schema = Z.Array(Z.String()).Min(2, "too few").Max(5, "too many");

		// Act
		var tooSmall = schema.Validate(["a"]);
		var tooBig = schema.Validate(["a", "b", "c", "d", "e", "f"]);

		// Assert
		await Assert.That(tooSmall.Errors[0].Code).IsEqualTo("too_small");
		await Assert.That(tooSmall.Errors[0].Message).IsEqualTo("too few");
		await Assert.That(tooBig.Errors[0].Code).IsEqualTo("too_big");
		await Assert.That(tooBig.Errors[0].Message).IsEqualTo("too many");
	}

	[Test]
	public async Task ZodArray_GivenNoCustomMessage_ReportsTheCount()
	{
		// Defect A3
		// Arrange
		var schema = Z.Array(Z.String()).Min(2).Max(5);

		// Act
		var result = schema.Validate(["a"]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Message).IsEqualTo("Array must have at least 2 elements, but got 1");
		await Assert.That(error.MessageFormat).IsEqualTo(ZodMessages.ArrayTooSmall);
		await Assert.That(error.Parameters!.Get<int>(ZodIssueParameters.Count)).IsEqualTo(1);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	[Test]
	public async Task ZodArray_GivenExactLength_ReportsExactLengthMessageWithCount()
	{
		// Defect A3
		// Arrange
		var schema = Z.Array(Z.String()).Length(2);

		// Act
		var result = schema.Validate(["a", "b", "c"]);

		// Assert
		await Assert.That(result.Errors[0].Message).IsEqualTo("Array must have exactly 2 elements, but got 3");
		await Assert.That(result.Errors[0].Code).IsEqualTo("too_big");
	}

	[Test]
	public async Task ZodTuple_GivenInvalidElement_PrefixesTheElementIndex()
	{
		// Defect A4
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.String().Min(3));

		// Act
		var result = schema.Validate(["first", "x"]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath).IsEqualTo(ZodPath.From(ZodPathSegment.At(1)));
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	[Test]
	public async Task ZodTuple_GivenWrongElementType_ReportsZodTypeNamesAtTheIndex()
	{
		// Defect A5
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number());

		// Act
		var result = schema.Validate(["first", "not-a-number"]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Path).IsEquivalentTo(ImmutableArray.Create("1"));
		await Assert.That(error.Message).IsEqualTo("Expected number, received string");
		await Assert.That(error.Message).DoesNotContain("Double");
	}

	[Test]
	public async Task ZodTuple_GivenNullElementAndNullableSchema_LetsTheSchemaAcceptIt()
	{
		// Defect A5
		// Arrange
		var schema = Z.Tuple(Z.Optional(Z.String()), Z.Number());

		// Act
		var result = schema.Validate([null, 1.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ZodTuple_GivenWrongLength_ReportsArityBoundsAndCategoryByDirection()
	{
		// Defect A6
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.String());

		// Act
		var tooShort = schema.Validate(["a"]);
		var tooLong = schema.Validate(["a", "b", "c"]);

		// Assert
		await Assert.That(tooShort.Errors[0].Code).IsEqualTo("invalid_tuple_length");
		await Assert.That(tooShort.Errors[0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(tooShort.Errors[0].Origin).IsEqualTo("array");
		await Assert.That(tooShort.Errors[0].Minimum).IsEqualTo(2);
		await Assert.That(tooShort.Errors[0].Maximum).IsEqualTo(2);
		await Assert.That(tooLong.Errors[0].Category).IsEqualTo(ZodIssueCategories.TooBig);
	}

	[Test]
	public async Task ZodUnion_GivenNoMatchingOption_GroupsIssuesPerOptionUnderUnionErrors()
	{
		// Defect A7
		// Arrange
		var schema = Z.Union(new FailingObjectSchema("first_failed"), new FailingObjectSchema("second_failed"));

		// Act
		var result = schema.Validate(new object());

		// Assert
		var error = result.Errors.Single();
		var unionErrors = error.GetUnionErrors();
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidUnion);
		await Assert.That(error.Parameters!.ContainsKey("errors")).IsFalse();
		await Assert.That(unionErrors.Length).IsEqualTo(2);
		await Assert.That(unionErrors[0][0].Code).IsEqualTo("first_failed");
		await Assert.That(unionErrors[1][0].Code).IsEqualTo("second_failed");
	}

	[Test]
	public async Task ZodTypedUnion_GivenNoMatchingOption_KeepsEachOptionsIssues()
	{
		// Defect A8
		// Arrange
		var schema = Z.Union(Z.String().Min(10), Z.Number().Min(100));

		// Act
		var result = schema.Validate("hi");

		// Assert
		var error = result.Errors.Single();
		var unionErrors = error.GetUnionErrors();
		await Assert.That(unionErrors.Length).IsEqualTo(2);
		await Assert.That(unionErrors[0][0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(unionErrors[1][0].Code).IsEqualTo("invalid_type");
		await Assert.That(error.Message).IsEqualTo("Value does not match any of the union options (string, number)");
	}

#if NET11_0_OR_GREATER
	[Test]
	public async Task ZodTypedNativeUnion_GivenNoMatchingOption_KeepsEachOptionsIssues()
	{
		// Defect A8
		// Arrange
		var schema = Z.NativeUnion(Z.String().Min(10), Z.Array(Z.String()));

		// Act
		var result = schema.Validate("hi");

		// Assert
		var unionErrors = result.Errors.Single().GetUnionErrors();
		await Assert.That(unionErrors.Length).IsEqualTo(2);
		await Assert.That(unionErrors[0][0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(unionErrors[1][0].Message).IsEqualTo("Expected array, received string");
	}
#endif

	[Test]
	public async Task ZodDiscriminatedUnion_GivenUnknownDiscriminator_ReportsAtTheDiscriminatorPathWithOptions()
	{
		// Defect A9
		// Arrange
		var schema = Z.DiscriminatedUnion("type")
			.Option("user", new PassThroughObjectSchema())
			.Option("admin", new PassThroughObjectSchema())
			.Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["type"] = "secret-input" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("invalid_discriminator");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidUnion);
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("type");
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.Discriminator)).IsEqualTo("type");
		await Assert.That(error.Message).DoesNotContain("secret-input");
	}

	[Test]
	public async Task ZodDiscriminatedUnion_GivenMissingDiscriminator_ReportsAtTheDiscriminatorPath()
	{
		// Defect A9
		// Arrange
		var schema = Z.DiscriminatedUnion("type").Option("user", new PassThroughObjectSchema()).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "x" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("missing_discriminator");
		await Assert.That(error.Path).IsEquivalentTo(ImmutableArray.Create("type"));
	}

	[Test]
	public async Task ZodDiscriminatedUnion_GivenOptions_ListsThemInDeclarationOrder()
	{
		// Defect A10
		// Arrange
		var schema = Z.DiscriminatedUnion("kind")
			.Option("zebra", new PassThroughObjectSchema())
			.Option("apple", new PassThroughObjectSchema())
			.Option("mango", new PassThroughObjectSchema())
			.Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["kind"] = "other" });

		// Assert
		await Assert
			.That(result.Errors[0].Message)
			.IsEqualTo("Invalid discriminator value. Expected one of: zebra, apple, mango");
	}

	[Test]
	public async Task ZodEnum_GivenValueNotAllowed_ReportsOptionsWithoutEchoingTheValue()
	{
		// Defect A11
		// Arrange
		var schema = Z.Enum("b", "a");

		// Act
		var result = schema.Validate("secret");

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidValue);
		await Assert.That(error.Message).IsEqualTo("Expected one of: a, b");
		await Assert
			.That(error.Parameters!.Get<ImmutableArray<string>>(ZodIssueParameters.Options))
			.IsEquivalentTo(ImmutableArray.Create("a", "b"));
	}

	[Test]
	public async Task ZodNativeEnum_GivenUndefinedValue_ReportsTheEnumRuleIdentity()
	{
		// Defect A11
		// Arrange
		var schema = Z.Enum<Colour>();

		// Act
		var result = schema.Validate((Colour)42);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(Rules.EnumRule<Colour>.ErrorCode);
		await Assert.That(error.Message).IsEqualTo("Value is not a defined member of Colour");
		await Assert.That(error.Origin).IsEqualTo("enum");
		await Assert
			.That(error.Parameters!.Get<ImmutableArray<string>>(ZodIssueParameters.Options))
			.IsEquivalentTo(ImmutableArray.Create("Red", "Green"));
	}

	[Test]
	public async Task ZodObject_GivenMissingField_ReportsTheSharedRequiredIdentity()
	{
		// Defect A12
		// Arrange
		var schema = Z.Object().Field("email", Z.String()).Build();

		// Act
		var result = schema.Validate([]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(ZodIssueCodes.MissingField);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(error.Message).IsEqualTo("email is required");
		await Assert.That(error.MessageFormat).IsEqualTo(ZodMessages.Required);
		await Assert.That(error.Path).IsEquivalentTo(ImmutableArray.Create("email"));
	}

	[Test]
	public async Task ZodObject_GivenStrictAndSeveralUnknownKeys_ReportsOneAggregatedIssue()
	{
		// Defect A13
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build().Strict();

		// Act
		var result = schema.Validate(
			new Dictionary<string, object?>
			{
				["name"] = "a",
				["x"] = 1,
				["y"] = 2,
			}
		);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo("unrecognized_keys");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.UnrecognizedKeys);
		await Assert.That(error.FullPath.IsRoot).IsTrue();
		await Assert
			.That(error.Parameters!.Get<ImmutableArray<string>>(ZodIssueParameters.Keys))
			.IsEquivalentTo(ImmutableArray.Create("x", "y"));
		await Assert.That(error.Message).IsEqualTo("Unrecognized keys: x, y");
	}

	[Test]
	public async Task ZodLiteral_GivenDifferentValue_ReportsExpectedInvariantlyWithoutTheInput()
	{
		// Defect A14
		// Arrange
		var schema = Z.Literal(1.5);
		var culture = System.Globalization.CultureInfo.CurrentCulture;

		// Act
		ValidationResult<double> result;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
			result = schema.Validate(2.25);
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = culture;
		}

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Message).IsEqualTo("Expected literal 1.5");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidValue);
		await Assert.That(error.Parameters!.Get<double>(ZodIssueParameters.Expected)).IsEqualTo(1.5);
	}

	[Test]
	public async Task ZodObjectField_GivenValueOfWrongClrType_ReportsZodTypeNames()
	{
		// Defect A15
		// Arrange
		var schema = Z.Object().Field("age", Z.Number()).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["age"] = "forty" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Message).IsEqualTo("Expected number, received string");
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.Expected)).IsEqualTo("number");
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.Received)).IsEqualTo("string");
	}

	[Test]
	public async Task ZodDiscriminatedUnionOption_GivenValueOfWrongClrType_ReportsZodTypeNames()
	{
		// Defect A15
		// Arrange
		var schema = Z.DiscriminatedUnion("type").Option("n", Z.Number()).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["type"] = "n" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Message).IsEqualTo("Expected number, received object");
		await Assert.That(error.Message).DoesNotContain("Dictionary");
	}

	[Test]
	public async Task NullInput_GivenEachNullRejectingSchema_ReportsTheSharedNullMessage()
	{
		// Defect A16
		// Arrange / Act
		string[] messages =
		[
			Z.String().Validate(null!).Errors[0].Message,
			Z.Array(Z.String()).Validate(null!).Errors[0].Message,
			Z.Record(Z.String()).Validate(null!).Errors[0].Message,
			Z.Object().Build().Validate(null!).Errors[0].Message,
			Z.Enum("a").Validate(null!).Errors[0].Message,
			Z.Tuple(Z.String(), Z.String()).Validate(null!).Errors[0].Message,
		];

		// Assert
		await Assert
			.That(messages)
			.IsEquivalentTo([
				"Expected string, received null",
				"Expected array, received null",
				"Expected record, received null",
				"Expected object, received null",
				"Expected string, received null",
				"Expected tuple, received null",
			]);
	}

	[Test]
	public async Task Refine_GivenFailure_ReportsRefinementFailedWithCustomCategory()
	{
		// Defect A17
		// Arrange
		var schema = Z.String().Refine(static value => value.Length > 3);

		// Act
		var result = schema.Validate("ab");

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(ZodIssueCodes.RefinementFailed);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.Custom);
	}

	[Test]
	public async Task SuperRefine_GivenStructuredIssue_KeepsEveryFieldAndPrefixesTheBasePath()
	{
		// Defect A18
		// Arrange
		var issue = ValidationError.Create(
			"too_short",
			"Too short",
			ZodPath.Member("Name"),
			new Dictionary<string, object?> { ["min"] = 3 },
			origin: "string",
			minimum: 3,
			category: ZodIssueCategories.TooSmall
		);
		RefineCtx<string> ctx = new("x", ZodPath.From(ZodPathSegment.At(2)));

		// Act
		ctx.AddIssue(issue);

		// Assert
		var added = ctx.Issues.Single();
		await Assert.That(added.FullPath.ToDottedString()).IsEqualTo("[2].Name");
		await Assert.That(added.Origin).IsEqualTo("string");
		await Assert.That(added.Minimum).IsEqualTo(3);
		await Assert.That(added.Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	[Test]
	public async Task SuperRefine_GivenTypedPathIssue_UsesTheTypedSegments()
	{
		// Defect A18
		// Arrange
		var schema = Z.Array(Z.String())
			.SuperRefine(static ctx =>
				ctx.AddIssue("dup", "Duplicate", ZodPath.From(ZodPathSegment.At(1)), category: "custom")
			);

		// Act
		var result = schema.Validate(["a", "a"]);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath[0].Kind).IsEqualTo(ZodPathSegmentKind.Index);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.Custom);
	}

	[Test]
	public async Task SuperRefine_GivenNestedInObject_PrefixesTheFieldPath()
	{
		// Defect A19
		// Arrange
		var schema = Z.Object()
			.Field("tags", Z.Array(Z.String()).SuperRefine(static ctx => ctx.AddIssue("bad", ["0"])))
			.Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["tags"] = SingleTag });

		// Assert
		await Assert.That(result.Errors.Single().FullPath.ToDottedString()).IsEqualTo("tags[0]");
	}

	[Test]
	public async Task ZodIntersection_GivenSameIssueFromBothSides_ReportsItOnce()
	{
		// Defect A20
		// Arrange
		var schema = Z.Intersection(Z.String().Min(5), Z.String().Min(5).Max(10));

		// Act
		var result = schema.Validate("abc");

		// Assert
		await Assert.That(result.Errors.Length).IsEqualTo(1);
	}

	[Test]
	public async Task ZodStringSpan_GivenFailingRule_ReportsTheRulesStructuredIdentity()
	{
		// Defect A21
		// Arrange
		var schema = Z.String().Min(5);

		// Act
		var valid = schema.IsValidSpan("abc".AsSpan(), out var errors);

		// Assert
		await Assert.That(valid).IsFalse();
		await Assert.That(errors[0].Origin).IsEqualTo("string");
		await Assert.That(errors[0].Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(errors[0].Lower).IsEqualTo(ErrorBound.Including(5));
		await Assert.That(errors[0].Parameters!.Get<int>(ZodIssueParameters.Count)).IsEqualTo(3);
	}

	[Test]
	public async Task ZodRecord_GivenInvalidValue_PrefixesAKeySegment()
	{
		// Defect A1 (record wrapper consistency)
		// Arrange
		var schema = Z.Record(Z.String().Min(3));

		// Act
		var result = schema.Validate(new Dictionary<string, string> { ["a.b"] = "x" });

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.FullPath[0].Kind).IsEqualTo(ZodPathSegmentKind.Key);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
	}

	[Test]
	public async Task ZodTypeNames_GivenClrTypes_ReturnsZodNames()
	{
		// Defect A15
		// Arrange / Act
		string[] names =
		[
			ZodTypeNames.Of(typeof(int)),
			ZodTypeNames.Of(typeof(double?)),
			ZodTypeNames.Of(typeof(string)),
			ZodTypeNames.Of(typeof(bool)),
			ZodTypeNames.Of(typeof(DateTimeOffset)),
			ZodTypeNames.Of(typeof(int[])),
			ZodTypeNames.Of(typeof(List<string>)),
			ZodTypeNames.Of(typeof(Dictionary<string, object?>)),
			ZodTypeNames.Of(typeof(System.Numerics.BigInteger)),
			ZodTypeNames.OfValue(null),
			ZodTypeNames.OfValue(double.NaN),
		];

		// Assert
		await Assert
			.That(names)
			.IsEquivalentTo([
				"number",
				"number",
				"string",
				"boolean",
				"date",
				"array",
				"array",
				"object",
				"bigint",
				"null",
				"nan",
			]);
	}

	enum Colour
	{
		Red,
		Green,
	}

	sealed class FailingObjectSchema(string code) : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) =>
			ValidationResult<object>.Failure(new ValidationError(code, "failed"));

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	sealed class PassThroughObjectSchema : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}

	sealed class MaxLengthObjectSchema(int max) : IZodSchema<object, object>
	{
		readonly ZodString _inner = Z.String().Max(max);

		public ValidationResult<object> Validate(object value)
		{
			var result = _inner.Validate((string)value);
			return result.IsSuccess
				? ValidationResult<object>.Success(result.Value)
				: ValidationResult<object>.Failure(result.Errors);
		}

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}

using ZodSharp.Core;

namespace ZodSharp.Rules;

/// <summary>
/// Regression tests for the rule defects (B1-B10) of the error-model overhaul. Each test is tagged with the defect
/// it would have caught.
/// </summary>
public class RuleErrorModelTests
{
	[Test]
	public async Task StringRules_GivenFailure_ReportOriginStringAndCategory()
	{
		// Defect B1
		// Arrange
		ValidationError[] errors =
		[
			ZodRuleErrors.Create(new MinLengthRule(3), "a", ZodPath.Root),
			ZodRuleErrors.Create(new MaxLengthRule(1), "abc", ZodPath.Root),
			ZodRuleErrors.Create(new EmailRule(), "nope", ZodPath.Root),
		];

		// Act
		string[] origins = [.. errors.Select(static e => e.Origin ?? string.Empty)];
		string[] categories = [.. errors.Select(static e => e.Category ?? string.Empty)];

		// Assert
		await Assert.That(origins).IsEquivalentTo(["string", "string", "string"]);
		await Assert
			.That(categories)
			.IsEquivalentTo([ZodIssueCategories.TooSmall, ZodIssueCategories.TooBig, ZodIssueCategories.InvalidFormat]);
		await Assert.That(errors[2].Parameters!.Get<string>(ZodIssueParameters.Format)).IsEqualTo("email");
	}

	[Test]
	public async Task NumericBoundRules_GivenFailure_ReportStructuredBounds()
	{
		// Defect B1
		// Arrange
		var schema = Z.Number().Min(2.5);

		// Act
		var result = schema.Validate(1.0);

		// Assert
		var error = result.Errors.Single();
		await Assert.That(error.Origin).IsEqualTo("number");
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(error.Lower).IsEqualTo(ErrorBound.Including(2.5));
	}

	[Test]
	public async Task DateBoundRule_GivenFailure_ReportsOriginDateAndBound()
	{
		// Defect B1
		// Arrange
		DateTime bound = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		MinValueRule<DateTime> rule = new(bound);

		// Act
		var error = ZodRuleErrors.Create(rule, bound.AddDays(-1), ZodPath.Root);

		// Assert
		await Assert.That(error.Origin).IsEqualTo("date");
		await Assert.That(error.Lower).IsEqualTo(ErrorBound.Including(bound));
	}

	[Test]
	public async Task GreaterThanAndLessThan_GivenFailure_ReportExclusiveBounds()
	{
		// Defect B2
		// Arrange
		GreaterThanRule<double> greaterThan = new(5);
		LessThanRule<double> lessThan = new(5);
		GreaterThanOrEqualRule<double> greaterThanOrEqual = new(5);

		// Act
		var greater = ZodRuleErrors.Create(greaterThan, 5.0, ZodPath.Root);
		var less = ZodRuleErrors.Create(lessThan, 5.0, ZodPath.Root);
		var greaterOrEqual = ZodRuleErrors.Create(greaterThanOrEqual, 4.0, ZodPath.Root);

		// Assert
		await Assert.That(greater.Lower).IsEqualTo(ErrorBound.Excluding(5.0));
		await Assert.That(less.Upper).IsEqualTo(ErrorBound.Excluding(5.0));
		await Assert.That(greaterOrEqual.Lower).IsEqualTo(ErrorBound.Including(5.0));
	}

	[Test]
	public async Task LengthRule_GivenTooShortOrTooLong_KeepsCodeAndReportsCategoryByDirection()
	{
		// Defect B3
		// Arrange
		LengthRule rule = new(2, 4);

		// Act
		var tooShort = ZodRuleErrors.Create(rule, "a", ZodPath.Root);
		var tooLong = ZodRuleErrors.Create(rule, "abcde", ZodPath.Root);

		// Assert
		await Assert.That(tooShort.Code).IsEqualTo(LengthRule.ErrorCode);
		await Assert.That(tooShort.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(tooLong.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(tooShort.Minimum).IsEqualTo(2);
		await Assert.That(tooShort.Maximum).IsEqualTo(4);
	}

	[Test]
	public async Task StringLengthRule_GivenSpanTooLong_ReportsCategoryAndCount()
	{
		// Defect B3
		// Arrange
		StringLengthRule rule = new(3, 1);

		// Act
		var error = ZodRuleErrors.Create(rule, "abcd".AsSpan(), ZodPath.Root);

		// Assert
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(error.Parameters!.Get<int>(ZodIssueParameters.Count)).IsEqualTo(4);
	}

	[Test]
	public async Task RangeRule_GivenValueOnEachSide_ReportsCategoryByDirectionAndInclusivity()
	{
		// Defect B3
		// Arrange
		RangeRule<double> rule = new(1, 10, minimumIsExclusive: true);

		// Act
		var below = ZodRuleErrors.Create(rule, 1.0, ZodPath.Root);
		var above = ZodRuleErrors.Create(rule, 11.0, ZodPath.Root);

		// Assert
		await Assert.That(below.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(above.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(below.Lower).IsEqualTo(ErrorBound.Excluding(1.0));
		await Assert.That(below.Upper).IsEqualTo(ErrorBound.Including(10.0));
		await Assert.That(below.Origin).IsEqualTo("number");
	}

	[Test]
	public async Task SafeIntegerRule_GivenEachFailure_ReportsCategoryByKind()
	{
		// Defect B4
		// Arrange
		SafeIntegerRule rule = new();

		// Act
		var fraction = ZodRuleErrors.Create(rule, 1.5, ZodPath.Root);
		var tooSmall = ZodRuleErrors.Create(rule, int.MinValue - 1d, ZodPath.Root);
		var tooBig = ZodRuleErrors.Create(rule, int.MaxValue + 1d, ZodPath.Root);

		// Assert
		await Assert.That(fraction.Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(fraction.Parameters!.Get<string>(ZodIssueParameters.Expected)).IsEqualTo("int");
		await Assert.That(tooSmall.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(tooBig.Category).IsEqualTo(ZodIssueCategories.TooBig);
	}

	[Test]
	public async Task Schema_GivenCustomMessageWithStrayBraces_DoesNotThrowAtValidationTime()
	{
		// Defect B5
		// Arrange
		var schema = Z.String().Min(3, "Use {braces} and {0 and }} freely {1}");

		// Act
		var result = schema.Validate("a");

		// Assert
		await Assert.That(result.Errors.Single().Message).IsEqualTo("Use {braces} and {0 and }} freely 1");
	}

	[Test]
	public async Task UuidRule_GivenCustomMessage_ReceivesTheSameArgumentsAsTheDefault()
	{
		// Defect B6
		// Arrange
		UUIDRule rule = new(UuidVersion.V4, "bad {0} v{1}");

		// Act
		var message = rule.GetErrorMessage("value");

		// Assert
		await Assert.That(message).IsEqualTo("bad value v4");
	}

	[Test]
	public async Task UuidRule_GivenVersionedDefault_ReportsTheVersionAsAnInteger()
	{
		// Defect B6
		// Arrange
		UUIDRule rule = new(UuidVersion.V7);

		// Act
		var error = ZodRuleErrors.Create(rule, "value", ZodPath.Root);

		// Assert
		await Assert.That(error.Message).IsEqualTo("Invalid UUID v7 format");
		await Assert.That(error.Parameters!.Get<int>(ZodIssueParameters.Version)).IsEqualTo(7);
	}

	[Test]
	public async Task IPAddressAndCidrRules_GivenNoMessage_FallBackToTheDefaultFormat()
	{
		// Defect B7
		// Arrange
		IPAddressRule defaultStruct = default;
		IPAddressRule explicitIp = new(IPAddressRuleType.IPv4);
		CidrRule defaultCidr = new();

		// Act
		var defaultStructMessage = defaultStruct.GetErrorMessage("x");
		var explicitMessage = explicitIp.GetErrorMessage("x");
		var cidrMessage = defaultCidr.GetErrorMessage("x");

		// Assert
		await Assert.That(defaultStructMessage).IsEqualTo("Invalid IPv4 address");
		await Assert.That(explicitMessage).IsEqualTo("Invalid IPv4 address");
		await Assert.That(cidrMessage).IsEqualTo("Invalid IPv4/IPv6 CIDR");
	}

	[Test]
	public async Task NumericMessage_GivenNonInvariantCurrentCulture_FormatsInvariantly()
	{
		// Defect B8
		// Arrange
		var schema = Z.Number().Min(2.5);
		var culture = System.Globalization.CultureInfo.CurrentCulture;

		// Act
		string message;
		try
		{
			System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
			message = schema.Validate(1.25).Errors[0].Message;
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = culture;
		}

		// Assert
		await Assert.That(message).IsEqualTo("Value must be at least 2.5, but got 1.25");
	}

	[Test]
	public async Task SensitiveRules_GivenFailure_NeverEchoTheInputInTheMessage()
	{
		// Defect B9
		// Arrange
		const string secret = "4111-SECRET-1111";
		string[] messages =
		[
			new EmailRule().GetErrorMessage(secret),
			new CreditCardRule().GetErrorMessage(secret),
			new JWTRule().GetErrorMessage(secret),
			new PhoneRule().GetErrorMessage(secret),
			new CompareRule("expected").GetErrorMessage(secret),
			new StartsWithRule("pre").GetErrorMessage(secret),
			new EndsWithRule("suf").GetErrorMessage(secret),
			new IncludesRule("inc").GetErrorMessage(secret),
			new RegexRule("^x$").GetErrorMessage(secret),
			new UUIDRule().GetErrorMessage(secret),
			new AllowedValuesRule<string>(["a"]).GetErrorMessage(secret),
		];

		// Act
		var leaking = messages.Where(static m => m.Contains(secret, StringComparison.Ordinal)).ToArray();

		// Assert
		await Assert.That(leaking).IsEmpty();
	}

	[Test]
	[NotInParallel(nameof(ZodErrorOptions))]
	public async Task RuleError_GivenReceivedValuesDisabled_DoesNotCarryTheInput()
	{
		// Defect B9
		// Arrange
		EmailRule rule = new();

		// Act
		var error = ZodRuleErrors.Create(rule, "secret@", ZodPath.Root);

		// Assert
		await Assert.That(ZodErrorOptions.IncludeReceivedValues).IsFalse();
		await Assert.That(error.Parameters!.ContainsKey(ZodIssueParameters.Received)).IsFalse();
	}

	[Test]
	[NotInParallel(nameof(ZodErrorOptions))]
	public async Task RuleError_GivenReceivedValuesEnabled_CarriesTheInputAsReceived()
	{
		// Defect B9
		// Arrange
		EmailRule rule = new();

		// Act
		ValidationError error;
		try
		{
			ZodErrorOptions.IncludeReceivedValues = true;
			error = ZodRuleErrors.Create(rule, "secret@", ZodPath.Root);
		}
		finally
		{
			ZodErrorOptions.IncludeReceivedValues = false;
		}

		// Assert
		await Assert.That(error.Parameters!.Get<string>(ZodIssueParameters.Received)).IsEqualTo("secret@");
		await Assert.That(error.Message).DoesNotContain("secret@");
	}

	[Test]
	public async Task RequiredRule_GivenFailure_ReportsTheSharedRequiredIdentity()
	{
		// Defect B10
		// Arrange
		RequiredRule<string?> rule = new();

		// Act
		var error = ZodRuleErrors.Create(rule, null, ZodPath.Root);

		// Assert
		await Assert.That(error.Code).IsEqualTo(ZodIssueCodes.MissingField);
		await Assert.That(error.Category).IsEqualTo(ZodIssueCategories.InvalidType);
		await Assert.That(error.Message).IsEqualTo("Value is required");
		await Assert.That(error.MessageFormat).IsEqualTo(ZodMessages.Required);
	}

	[Test]
	public async Task RequiredRule_GivenHostReformatsAtAMemberPath_NamesTheMember()
	{
		// Defect B10
		// Arrange
		RequiredRule<string?> rule = new();
		var error = ZodRuleErrors.Create(rule, null, ZodPath.Member("Email"));

		// Act
		var message = ZodMessageFormat.Format(
			error.MessageFormat!,
			error,
			System.Globalization.CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(message).IsEqualTo("Email is required");
	}
}

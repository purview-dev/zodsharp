using System.Globalization;
using ZodSharp.Core;

namespace ZodSharp.Rules;

/// <summary>
/// The default messages and the named templates (<see cref="IZodRule.MessageFormat"/>) of the built-in rules: a
/// default never contains the input, and every runtime-schema error carries a template a host can re-format into
/// exactly the same message.
/// </summary>
public class RuleMessageTemplateTests
{
	const double Input = 123456.789;

	const string InputText = "123456.789";

	public static IEnumerable<Func<(string Name, ValidationError Error)>> NumericFailures()
	{
		yield return () => ("Range", Z.Number().Range(1, 10).Validate(Input).Errors.Single());
		yield return () => ("MultipleOf", Z.Number().MultipleOf(5).Validate(Input).Errors.Single());
		yield return () => ("Int", Z.Number().Int().Validate(Input).Errors.Single());
		yield return () => ("SafeInteger", Z.Number().Safe().Validate(Input).Errors.Single());
		yield return () => ("Min", Z.Number().Min(1e9).Validate(Input).Errors.Single());
		yield return () => ("Max", Z.Number().Max(1).Validate(Input).Errors.Single());
		yield return () => ("Gt", Z.Number().Gt(1e9).Validate(Input).Errors.Single());
		yield return () => ("Gte", Z.Number().Gte(1e9).Validate(Input).Errors.Single());
		yield return () => ("Lt", Z.Number().Lt(1).Validate(Input).Errors.Single());
		yield return () => ("Lte", Z.Number().Lte(1).Validate(Input).Errors.Single());
		yield return () => ("Even", ZodRuleErrors.Create(new EvenRule<double>(), Input, ZodPath.Root));
		yield return () => ("Odd", ZodRuleErrors.Create(new OddRule<double>(), 123456.0, ZodPath.Root));
		yield return () => ("Finite", ZodRuleErrors.Create(new FiniteRule<double>(), double.NaN, ZodPath.Root));
	}

	[Test]
	[MethodDataSource(nameof(NumericFailures))]
	[NotInParallel(nameof(ZodErrorOptions))]
	public async Task NumericRule_GivenFailure_DefaultMessageDoesNotContainTheInput(string name, ValidationError error)
	{
		// Arrange
		await Assert.That(name).IsNotEmpty();

		// Act
		var message = error.Message;

		// Assert
		await Assert.That(message).DoesNotContain(InputText).Because(name);
		await Assert.That(message).DoesNotContain("NaN").Because(name);
		await Assert.That(message).DoesNotContain("but got").Because(name);
		await Assert.That(error.Parameters?.ContainsKey(ZodIssueParameters.Received) ?? false).IsFalse().Because(name);
	}

	[Test]
	public async Task NumericRule_GivenDefaults_UsesZodLikeWording()
	{
		// Arrange
		var range = Z.Number().Range(1, 10).Validate(11);
		var multipleOf = Z.Number().MultipleOf(5).Validate(7);
		var integer = Z.Number().Int().Validate(1.5);
		var minimum = Z.Number().Min(3).Validate(1);

		// Act
		string[] messages =
		[
			range.Errors.Single().Message,
			multipleOf.Errors.Single().Message,
			integer.Errors.Single().Message,
			minimum.Errors.Single().Message,
		];

		// Assert
		await Assert
			.That(messages)
			.IsEquivalentTo([
				"Value must be between 1 and 10",
				"Number must be a multiple of 5",
				"Expected integer, received float",
				"Value must be at least 3",
			]);
	}

	[Test]
	public async Task NumericRule_GivenCustomMessageWithValuePlaceholder_StillReceivesTheValue()
	{
		// Arrange
		var schema = Z.Number().Range(1, 10, message: "{2} is outside {0}-{1}");
		MinValueRule<double> minimum = new(3, "{1} is below {0}");

		// Act
		var rangeMessage = schema.Validate(11).Errors.Single().Message;
		var minimumMessage = minimum.GetErrorMessage(1);

		// Assert
		await Assert.That(rangeMessage).IsEqualTo("11 is outside 1-10");
		await Assert.That(minimumMessage).IsEqualTo("1 is below 3");
	}

	[Test]
	[NotInParallel(nameof(ZodErrorOptions))]
	public async Task NumericRule_GivenReceivedValuesEnabled_ReportsReceivedAndResolvesACustomTemplate()
	{
		// Arrange
		var schema = Z.Number().Range(1, 10, message: "{2} is outside {0}-{1}");

		// Act
		ValidationError error;
		try
		{
			ZodErrorOptions.IncludeReceivedValues = true;
			error = schema.Validate(11).Errors.Single();
		}
		finally
		{
			ZodErrorOptions.IncludeReceivedValues = false;
		}

		var reformatted = DefaultZodMessageFormatter.Instance.Format(error, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(error.Parameters!.Get<double>(ZodIssueParameters.Received)).IsEqualTo(11);
		await Assert.That(error.MessageFormat).IsEqualTo("{received} is outside {minimum}-{maximum}");
		await Assert.That(reformatted).IsEqualTo("11 is outside 1-10");
	}

	public static IEnumerable<Func<(string Name, ValidationError Error)>> RuntimeFailures()
	{
		yield return () => ("String.Min", Z.String().Min(3).Validate("a").Errors.Single());
		yield return () => ("String.Max", Z.String().Max(1).Validate("abc").Errors.Single());
		yield return () => ("String.Length", Z.String().Length(2, 3).Validate("a").Errors.Single());
		yield return () => ("String.StringLength", Z.String().StringLength(3, 2).Validate("a").Errors.Single());
		yield return () => ("String.Email", Z.String().Email().Validate("nope").Errors.Single());
		yield return () => ("String.Regex", Z.String().Regex("^[a-z]+$").Validate("123").Errors.Single());
		yield return () => ("String.StartsWith", Z.String().StartsWith("pre").Validate("x").Errors.Single());
		yield return () => ("String.EndsWith", Z.String().EndsWith("suf").Validate("x").Errors.Single());
		yield return () => ("String.Includes", Z.String().Includes("inc").Validate("x").Errors.Single());
		yield return () => ("String.UUID", Z.String().UUID().Validate("x").Errors.Single());
		yield return () => ("String.IP", Z.String().IP().Validate("x").Errors.Single());
		yield return () => ("String.Cidr", Z.String().Cidr().Validate("x").Errors.Single());
		yield return () => ("String.Url", Z.String().Url().Validate("x").Errors.Single());
		yield return () => ("String.Uri", Z.String().Uri().Validate("").Errors.Single());
		yield return () => ("String.Guid", Z.String().Guid().Validate("x").Errors.Single());
		yield return () => ("String.Compare", Z.String().Compare("other").Validate("x").Errors.Single());
		yield return () => ("Number.Range", Z.Number().Range(1, 10).Validate(11).Errors.Single());
		yield return () => ("Number.MultipleOf", Z.Number().MultipleOf(5).Validate(7).Errors.Single());
		yield return () => ("Number.Int", Z.Number().Int().Validate(1.5).Errors.Single());
		yield return () => ("Number.Safe", Z.Number().Safe().Validate(1e12).Errors.Single());
		yield return () => ("Number.Min", Z.Number().Min(3).Validate(1).Errors.Single());
		yield return () => ("Number.Lt", Z.Number().Lt(1).Validate(1).Errors.Single());
		yield return () => ("Number.Even", Z.Number().Even().Validate(3).Errors.Single());
		yield return () => ("Number.Odd", Z.Number().Odd().Validate(2).Errors.Single());
		yield return () => ("Number.Finite", Z.Number().Finite().Validate(double.PositiveInfinity).Errors.Single());
		yield return () => ("MinCount", ZodRuleErrors.Create(new MinCountRule(2), (int[])[1], ZodPath.Root));
		yield return () => ("MaxCount", ZodRuleErrors.Create(new MaxCountRule(1), (int[])[1, 2], ZodPath.Root));
		yield return () => ("CountRange", ZodRuleErrors.Create(new CountRangeRule(2, 3), (int[])[1], ZodPath.Root));
		yield return () => ("Allowed", ZodRuleErrors.Create(new AllowedValuesRule<string>(["a"]), "b", ZodPath.Root));
		yield return () => ("Denied", ZodRuleErrors.Create(new DeniedValuesRule<string>(["a"]), "a", ZodPath.Root));
		yield return () => ("Enum", ZodRuleErrors.Create(new EnumRule<DayOfWeek>(), (DayOfWeek)42, ZodPath.Root));
		yield return () => ("NonSentinel", ZodRuleErrors.Create(new NonSentinelRule<Guid>(), Guid.Empty, ZodPath.Root));
		yield return () =>
			("NullOrNonWhiteSpace", ZodRuleErrors.Create(new NullOrNonWhiteSpaceRule(), " ", ZodPath.Root));
	}

	[Test]
	[MethodDataSource(nameof(RuntimeFailures))]
	public async Task RuntimeRule_GivenFailure_ReportsATemplateThatReformatsToTheMessage(
		string name,
		ValidationError error
	)
	{
		// Arrange
		await Assert.That(name).IsNotEmpty();

		// Act
		var complete = ZodMessageFormat.TryFormatComplete(
			error.MessageFormat ?? string.Empty,
			error,
			out var reformatted,
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(error.MessageFormat).IsNotNull().Because(name);
		await Assert.That(complete).IsTrue().Because(name);
		await Assert.That(reformatted).IsEqualTo(error.Message).Because(name);
	}

	[Test]
	public async Task StringRequired_GivenFailureAtTheRoot_ReportsAResolvableTemplate()
	{
		// Arrange
		var error = Z.String().Required().Validate("").Errors.Single();

		// Act
		var complete = ZodMessageFormat.TryFormatComplete(
			error.MessageFormat ?? string.Empty,
			error,
			out var reformatted,
			CultureInfo.InvariantCulture
		);

		// Assert
		await Assert.That(error.MessageFormat).IsEqualTo(ZodMessages.Required);
		await Assert.That(complete).IsTrue();
		await Assert.That(reformatted).IsEqualTo("Value is required");
		await Assert.That(reformatted).IsEqualTo(error.Message);
	}

	[Test]
	public async Task StringRequired_GivenFailureUnderAMember_NamesTheMember()
	{
		// Arrange
		var error = Z.String().Required().Validate("").Errors.Single().WithPathPrefix(ZodPathSegment.Member("Email"));

		// Act
		var reformatted = DefaultZodMessageFormatter.Instance.Format(error, CultureInfo.InvariantCulture);

		// Assert
		await Assert.That(reformatted).IsEqualTo("Email is required");
	}

	[Test]
	public async Task RuntimeRule_GivenCustomMessage_ReportsItAsANamedTemplate()
	{
		// Arrange
		var schema = Z.String().Min(3, "At least {0} characters");

		// Act
		var error = schema.Validate("a").Errors.Single();

		// Assert
		await Assert.That(error.Message).IsEqualTo("At least 3 characters");
		await Assert.That(error.MessageFormat).IsEqualTo("At least {minimum} characters");
	}

	[Test]
	public async Task StringRules_GivenFailure_ReportTheTemplatesTheyDocument()
	{
		// Arrange
		var minimum = Z.String().Min(3).Validate("a").Errors.Single();
		var ip = Z.String().IP(IPAddressRuleType.IPv6).Validate("x").Errors.Single();
		var regex = Z.String().Regex("^[a-z]+$").Validate("1").Errors.Single();

		// Act
		string[] templates = [minimum.MessageFormat ?? "", ip.MessageFormat ?? "", regex.MessageFormat ?? ""];

		// Assert
		await Assert
			.That(templates)
			.IsEquivalentTo([
				"String must be at least {minimum} characters long, but got {count}",
				"Invalid IPv6 address",
				"String does not match the required pattern: {pattern}",
			]);
	}
}

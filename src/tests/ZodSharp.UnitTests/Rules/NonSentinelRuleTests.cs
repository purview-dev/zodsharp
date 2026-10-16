using System.Globalization;
using System.Text;

namespace ZodSharp.Rules;

public class NonSentinelRuleTests
{
	[Test]
	public async Task IsValid_GivenGuidEmpty_ReturnsFalse()
	{
		// Arrange
		NonSentinelRule<Guid> rule = new();

		// Act
		var isValid = rule.IsValid(Guid.Empty);
		var isDefaultValid = rule.IsValid(default);

		// Assert
		await Assert.That(isValid).IsFalse();
		await Assert.That(isDefaultValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenNonEmptyGuid_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<Guid> rule = new();

		// Act
		var isValid = rule.IsValid(Guid.NewGuid());

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task IsValid_GivenDateTimeBounds_ReturnsFalse()
	{
		// Arrange
		NonSentinelRule<DateTime> rule = new();

		// Act
		var minIsValid = rule.IsValid(DateTime.MinValue);
		var maxIsValid = rule.IsValid(DateTime.MaxValue);
		var defaultIsValid = rule.IsValid(default);

		// Assert
		await Assert.That(minIsValid).IsFalse();
		await Assert.That(maxIsValid).IsFalse();
		await Assert.That(defaultIsValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenRegularDateTime_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<DateTime> rule = new();

		// Act
		var isValid = rule.IsValid(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task IsValid_GivenDateTimeOffsetBounds_ReturnsFalse()
	{
		// Arrange
		NonSentinelRule<DateTimeOffset> rule = new();

		// Act
		var minIsValid = rule.IsValid(DateTimeOffset.MinValue);
		var maxIsValid = rule.IsValid(DateTimeOffset.MaxValue);
		var defaultIsValid = rule.IsValid(default);

		// Assert
		await Assert.That(minIsValid).IsFalse();
		await Assert.That(maxIsValid).IsFalse();
		await Assert.That(defaultIsValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenRegularDateTimeOffset_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<DateTimeOffset> rule = new();

		// Act
		var isValid = rule.IsValid(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task IsValid_GivenDateOnlyBounds_ReturnsFalse()
	{
		// Arrange
		NonSentinelRule<DateOnly> rule = new();

		// Act
		var minIsValid = rule.IsValid(DateOnly.MinValue);
		var maxIsValid = rule.IsValid(DateOnly.MaxValue);
		var defaultIsValid = rule.IsValid(default);

		// Assert
		await Assert.That(minIsValid).IsFalse();
		await Assert.That(maxIsValid).IsFalse();
		await Assert.That(defaultIsValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenRegularDateOnly_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<DateOnly> rule = new();

		// Act
		var isValid = rule.IsValid(new DateOnly(2024, 1, 2));

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task IsValid_GivenTimeOnlyBounds_ReturnsFalse()
	{
		// Arrange
		NonSentinelRule<TimeOnly> rule = new();

		// Act
		var minIsValid = rule.IsValid(TimeOnly.MinValue);
		var maxIsValid = rule.IsValid(TimeOnly.MaxValue);
		var defaultIsValid = rule.IsValid(default);

		// Assert
		await Assert.That(minIsValid).IsFalse();
		await Assert.That(maxIsValid).IsFalse();
		await Assert.That(defaultIsValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenRegularTimeOnly_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<TimeOnly> rule = new();

		// Act
		var isValid = rule.IsValid(new TimeOnly(12, 30));

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public async Task IsValid_GivenNullEmptyOrWhitespaceString_ReturnsFalse(string? value)
	{
		// Arrange
		NonSentinelRule<string> rule = new();

		// Act
		var isValid = rule.IsValid(value!);
		var defaultIsValid = rule.IsValid(default!);

		// Assert
		await Assert.That(isValid).IsFalse();
		await Assert.That(defaultIsValid).IsFalse();
	}

	[Test]
	public async Task IsValid_GivenNonEmptyString_ReturnsTrue()
	{
		// Arrange
		NonSentinelRule<string> rule = new();

		// Act
		var isValid = rule.IsValid("value");

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task IsValid_GivenTypeWithoutKnownSentinel_ReturnsTrue()
	{
		// Arrange — int has no built-in sentinel, so its default value is accepted.
		NonSentinelRule<int> rule = new();

		// Act
		var isValid = rule.IsValid(0);

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task GetErrorMessage_GivenNoCustomMessage_FormatsDefaultMessage()
	{
		// Arrange
		NonSentinelRule<Guid> rule = new();

		// Act
		var message = rule.GetErrorMessage(Guid.Empty);
		var expected = string.Format(
			CultureInfo.CurrentCulture,
			CompositeFormat.Parse(NonSentinelRule<Guid>.MessageFormat),
			"an empty GUID"
		);

		// Assert
		await Assert.That(message).IsEqualTo(expected);
		await Assert.That(message).IsEqualTo("Value has a sentinel value, but got an empty GUID");
	}

	[Test]
	public async Task GetErrorMessage_GivenEmptyString_DescribesTheSentinel()
	{
		// Arrange
		NonSentinelRule<string> rule = new();

		// Act
		var message = rule.GetErrorMessage("");

		// Assert
		await Assert.That(message).IsEqualTo("Value has a sentinel value, but got an empty string");
	}

	[Test]
	public async Task GetErrorMessage_GivenDateTimeMinValue_DescribesTheSentinel()
	{
		// Arrange
		NonSentinelRule<DateTime> rule = new();

		// Act
		var message = rule.GetErrorMessage(DateTime.MinValue);

		// Assert
		await Assert.That(message).IsEqualTo("Value has a sentinel value, but got DateTime.MinValue");
	}

	[Test]
	public async Task GetErrorMessage_GivenCustomMessage_ReturnsCustomMessage()
	{
		// Arrange
		NonSentinelRule<Guid> rule = new("Custom sentinel message.");

		// Act
		var message = rule.GetErrorMessage(Guid.Empty);

		// Assert
		await Assert.That(message).IsEqualTo("Custom sentinel message.");
	}

	[Test]
	public async Task Code_GivenRule_ReturnsErrorCodeConstant()
	{
		// Arrange
		NonSentinelRule<Guid> rule = new();

		// Act
		var code = rule.Code;

		// Assert
		await Assert.That(code).IsEqualTo(NonSentinelRule<Guid>.ErrorCode);
	}

	[Test]
	public async Task Validate_GivenSentinelDate_ReportsErrorCodeAndEmptyPath()
	{
		// Arrange
		var schema = Z.Date().AddRule(new NonSentinelRule<DateTime>());

		// Act
		var result = schema.Validate(DateTime.MinValue);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors).HasSingleItem();
		await Assert.That(result.Errors[0].Code).IsEqualTo(NonSentinelRule<DateTime>.ErrorCode);
		await Assert.That(result.Errors[0].Path).IsEmpty();
	}

	[Test]
	public async Task Validate_GivenWhitespaceString_ReportsErrorCode()
	{
		// Arrange
		var schema = Z.String().AddRule(new NonSentinelRule<string>());

		// Act
		var result = schema.Validate("   ");

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors).HasSingleItem();
		await Assert.That(result.Errors[0].Code).IsEqualTo(NonSentinelRule<string>.ErrorCode);
	}

	[Test]
	public async Task NonSentinel_GivenFluentStringSchema_RejectsWhitespace()
	{
		// Arrange
		var schema = Z.String().NonSentinel();

		// Act
		var result = schema.Validate("   ");

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors).HasSingleItem();
		await Assert.That(result.Errors[0].Code).IsEqualTo(NonSentinelRule<string>.ErrorCode);
	}

	[Test]
	public async Task NonSentinel_GivenFluentDateSchema_RejectsMinValue()
	{
		// Arrange
		var schema = Z.Date().NonSentinel();

		// Act
		var result = schema.Validate(DateTime.MinValue);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo(NonSentinelRule<DateTime>.ErrorCode);
	}

	[Test]
	public async Task NonSentinel_GivenFluentChain_KeepsConcreteSchema()
	{
		// Arrange / Act — the covariant override keeps ZodString so later fluent calls still compile.
		var result = Z.String().NonSentinel().Min(3).Validate("ab");

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task NonSentinel_GivenTypeWithoutKnownSentinel_AlwaysPasses()
	{
		// Arrange
		var schema = Z.Number().NonSentinel();

		// Act
		var result = schema.Validate(42.0);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}
}

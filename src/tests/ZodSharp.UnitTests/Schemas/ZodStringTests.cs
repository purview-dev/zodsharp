using ZodSharp.Core;

namespace ZodSharp.Schemas;

public class ZodStringTests
{
	[Test]
	[Arguments("John", true)]
	[Arguments("AB", false)]
	[Arguments("ABC", true)]
	public async Task StringMin_GivenValue_ReturnsExpectedResult(string value, bool expected)
	{
		var result = Z.String().Min(3).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task StringMin_GivenEmptyStringAndZeroMinimum_ReturnsSuccess()
	{
		var result = Z.String().Min(0).Validate(string.Empty);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task StringMax_GivenTooLongValue_ReturnsFailure()
	{
		var result = Z.String().Max(50).Validate(new string('a', 51));

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors).Count().IsEqualTo(1);
	}

	[Test]
	public async Task StringMax_GivenExactlyMaximumLength_ReturnsSuccess()
	{
		var result = Z.String().Max(50).Validate(new string('a', 50));

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task StringMax_GivenVeryLongValue_ReturnsFailure()
	{
		var result = Z.String().Max(50).Validate(new string('a', 1001));

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	[Arguments("user@example.com", true)]
	[Arguments("first.last+tag@domain.co.uk", true)]
	[Arguments("invalid", false)]
	[Arguments("user@", false)]
	[Arguments("@example.com", false)]
	public async Task StringEmail_GivenValue_ReturnsExpectedResult(string value, bool expected)
	{
		var result = Z.String().Email().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments("https://example.com", true)]
	[Arguments("http://sub.domain.co.uk/path?x=1#fragment", true)]
	[Arguments("not-a-url", false)]
	public async Task StringUrl_GivenValue_ReturnsExpectedResult(string value, bool expected)
	{
		var result = Z.String().Url().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments("550e8400-e29b-41d4-a716-446655440000", true)]
	[Arguments("550e8400-e29b-11d4-a716-446655440000", true)]
	[Arguments("0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c", true)]
	[Arguments("550E8400-E29B-41D4-A716-446655440000", true)]
	[Arguments("00000000-0000-0000-0000-000000000000", true)]
	[Arguments("ffffffff-ffff-ffff-ffff-ffffffffffff", true)]
	[Arguments("550e8400-e29b-01d4-a716-446655440000", false)]
	[Arguments("550e8400-e29b-91d4-a716-446655440000", false)]
	[Arguments("550e8400-e29b-f1d4-a716-446655440000", false)]
	[Arguments("550e8400-e29b-41d4-0716-446655440000", false)]
	[Arguments("550e8400-e29b-41d4-5716-446655440000", false)]
	[Arguments("550e8400-e29b-41d4-c716-446655440000", false)]
	[Arguments("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", false)]
	[Arguments("not-a-uuid", false)]
	[Arguments("550e8400-e29b-41d4-a716", false)]
	public async Task StringUuid_GivenValue_ReturnsExpectedResult(string value, bool expected)
	{
		var result = Z.String().UUID().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(UuidVersion.V7, "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c", true)]
	[Arguments(UuidVersion.V7, "550e8400-e29b-41d4-a716-446655440000", false)]
	[Arguments(UuidVersion.V7, "0192b4c1-7a9b-7f5e-03a3-2d4e6f8a0b1c", false)]
	[Arguments(UuidVersion.V4, "550e8400-e29b-41d4-a716-446655440000", true)]
	[Arguments(UuidVersion.V4, "00000000-0000-0000-0000-000000000000", false)]
	[Arguments(UuidVersion.V4, "ffffffff-ffff-ffff-ffff-ffffffffffff", false)]
	[Arguments(UuidVersion.V8, "550e8400-e29b-81d4-a716-446655440000", true)]
	public async Task StringUuid_GivenVersion_ReturnsExpectedResult(UuidVersion version, string value, bool expected)
	{
		var result = Z.String().UUID(version).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task StringStartsWith_GivenMatchingPrefix_ReturnsSuccess()
	{
		var result = Z.String().StartsWith("https://").Validate("https://example.com");

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task StringStartsWith_GivenNonMatchingPrefix_ReturnsFailure()
	{
		var result = Z.String().StartsWith("https://").Validate("http://example.com");

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task StringEndsWith_GivenMatchingSuffix_ReturnsSuccess()
	{
		var result = Z.String().EndsWith(".com").Validate("example.com");

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task StringEndsWith_GivenNonMatchingSuffix_ReturnsFailure()
	{
		var result = Z.String().EndsWith(".com").Validate("example.org");

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	[Arguments("  hello  ", "hello")]
	[Arguments("\t hello \t", "hello")]
	[Arguments("hello", "hello")]
	public async Task StringTrim_GivenValue_ReturnsTrimmedValue(string value, string expected)
	{
		var result = Z.String().Trim().Validate(value);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo(expected);
	}

	[Test]
	public async Task StringToUpper_GivenLowercase_ReturnsUppercaseValue()
	{
		var result = Z.String().ToUpper().Validate("hello");

		await Assert.That(result.Value).IsEqualTo("HELLO");
	}

	[Test]
	public async Task StringToLower_GivenUppercase_ReturnsLowercaseValue()
	{
		var result = Z.String().ToLower().Validate("HELLO");

		await Assert.That(result.Value).IsEqualTo("hello");
	}

	[Test]
	[Arguments("1234567890", true)]
	[Arguments("123", false)]
	[Arguments("12345678901", false)]
	public async Task StringLength_GivenValue_ReturnsExpectedResult(string value, bool expected)
	{
		var result = Z.String().Length(10).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task ValidateSpan_GivenValidEmailSpan_ReturnsSuccess()
	{
		var result = Z.String().Min(3).Max(50).Email().ValidateSpan("user@example.com".AsSpan());

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ValidateSpan_GivenInvalidEmailSpan_ReturnsFailure()
	{
		var result = Z.String().Email().ValidateSpan("not-an-email".AsSpan());

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Parse_GivenInvalidString_ThrowsZodExceptionWithErrors()
	{
		var exception = Assert.Throws<ZodException>(static () => Z.String().Min(3).Parse("AB"));

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception.Errors).Count().IsEqualTo(1);
	}
}

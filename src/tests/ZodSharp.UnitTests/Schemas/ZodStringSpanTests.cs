namespace ZodSharp.Schemas;

public class ZodStringSpanTests
{
	[Test]
	public async Task IsValidSpan_GivenValidValue_ReturnsTrueWithoutErrors()
	{
		var schema = Z.String().Min(3).Max(50).Email();

		var isValid = schema.IsValidSpan("user@example.com".AsSpan(), out var errors);

		await Assert.That(isValid).IsTrue();
		await Assert.That(errors.Length).IsEqualTo(0);
	}

	[Test]
	public async Task IsValidSpan_GivenTooShortValue_ReturnsFalseWithErrors()
	{
		var schema = Z.String().Min(3);

		var isValid = schema.IsValidSpan("AB".AsSpan(), out var errors);

		await Assert.That(isValid).IsFalse();
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("validation_failed");
	}

	[Test]
	public async Task ValidateSpan_GivenEmptySpan_AppliesRules()
	{
		// Regression: an empty span previously bypassed the rules and always succeeded.
		var schema = Z.String().Min(3);

		var result = schema.ValidateSpan(ReadOnlySpan<char>.Empty);

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ValidateSpan_GivenValidValue_ReturnsMaterialisedValue()
	{
		var schema = Z.String().Min(3).Max(50);

		var result = schema.ValidateSpan("John".AsSpan());

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo("John");
	}

	[Test]
	public async Task ValidateSpan_GivenRuleWithoutSpanContract_FallsBackToRules()
	{
		// NoWhitespaceRule only implements IValidationRule<string>, so the span path falls back.
		var schema = Z.String();
		schema.Rule(new Rules.NoWhitespaceRule());

		var valid = schema.IsValidSpan("John".AsSpan(), out var validErrors);
		var invalid = schema.IsValidSpan("John Doe".AsSpan(), out var invalidErrors);

		await Assert.That(valid).IsTrue();
		await Assert.That(validErrors.Length).IsEqualTo(0);
		await Assert.That(invalid).IsFalse();
		await Assert.That(invalidErrors).HasSingleItem();
	}

	[Test]
	public async Task ValidateSpan_GivenTransformSchema_AppliesTransform()
	{
		var schema = Z.String().Trim().ToUpper();

		var result = schema.ValidateSpan("  hi  ".AsSpan());

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsEqualTo("HI");
	}

	[Test]
	public async Task IsValidSpan_GivenUuidRule_ValidatesWithoutString()
	{
		var schema = Z.String().UUID();

		var valid = schema.IsValidSpan("550e8400-e29b-41d4-a716-446655440000".AsSpan(), out _);
		var invalid = schema.IsValidSpan("not-a-uuid".AsSpan(), out _);

		await Assert.That(valid).IsTrue();
		await Assert.That(invalid).IsFalse();
	}
}

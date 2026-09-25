namespace ZodSharp.Rules;

/// <summary>
/// Example custom rule used by the custom-rule tests and the documentation. Rejects strings that
/// contain whitespace.
/// </summary>
public readonly record struct NoWhitespaceRule(string? Message = null) : Core.IValidationRule<string>
{
	/// <inheritdoc/>
	public bool IsValid(in string value)
	{
		if (value is null)
			return false;

		foreach (var character in value)
		{
			if (char.IsWhiteSpace(character))
				return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public string GetErrorMessage(in string value) => Message ?? $"Whitespace is not allowed in '{value}'.";
}

public class CustomRuleTests
{
	[Test]
	[Arguments("John", true)]
	[Arguments("John Doe", false)]
	[Arguments("John\tDoe", false)]
	public async Task Rule_GivenCustomRule_ReturnsExpectedResult(string value, bool expected)
	{
		// Arrange
		var schema = Z.String().Rule(new NoWhitespaceRule());

		// Act
		var result = schema.Validate(value);

		// Assert
		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task AddRule_GivenCustomRule_ProducesRuleErrorMessage()
	{
		// Arrange
		var schema = Z.String().AddRule(new NoWhitespaceRule("Spaces are not allowed."));

		// Act
		var result = schema.Validate("John Doe");

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("validation_failed");
		await Assert.That(result.Errors[0].Message).IsEqualTo("Spaces are not allowed.");
		await Assert.That(result.Errors[0].Path).IsEmpty();
	}

	[Test]
	public async Task AddRule_GivenNull_ThrowsArgumentNullException()
	{
		// Arrange
		var schema = Z.String();

		// Act / Assert
		var exception = Assert.Throws<ArgumentNullException>(() => schema.AddRule(null!));

		await Assert.That(exception).IsNotNull();
	}
}

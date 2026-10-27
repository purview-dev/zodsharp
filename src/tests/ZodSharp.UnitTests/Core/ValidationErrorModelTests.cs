using ZodSharp.Rules;

namespace ZodSharp.Core;

public class ValidationErrorModelTests
{
	// Defect A23
	[Test]
	public async Task Equals_GivenSeparatelyBuiltIdenticalErrors_AreEqual()
	{
		// Arrange
		ValidationError first = new(
			"too_small",
			"Too small",
			["Items", "0"],
			new Dictionary<string, object?> { ["min"] = 1 }
		);
		ValidationError second = new(
			"too_small",
			"Too small",
			["Items", "0"],
			new Dictionary<string, object?> { ["min"] = 1 }
		);

		// Assert
		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task WithPathPrefix_GivenStructuredError_KeepsEveryField()
	{
		// Arrange
		var error = ValidationError.Create(
			"too_small",
			"Too small",
			ZodPath.Member("Quantity"),
			new Dictionary<string, object?> { ["count"] = 0 },
			origin: "number",
			minimum: 1,
			maximum: 9,
			inclusive: true,
			category: ZodIssueCategories.TooSmall
		) with
		{
			MessageFormat = "{member} too small",
		};

		// Act
		var prefixed = error.WithPathPrefix(ZodPathSegment.At(2)).WithPathPrefix(ZodPathSegment.Member("Items"));

		// Assert
		await Assert.That(prefixed.FullPath.ToDottedString()).IsEqualTo("Items[2].Quantity");
		await Assert.That(prefixed.Origin).IsEqualTo("number");
		await Assert.That(prefixed.Minimum).IsEqualTo(1);
		await Assert.That(prefixed.Maximum).IsEqualTo(9);
		await Assert.That(prefixed.Inclusive == true).IsTrue();
		await Assert.That(prefixed.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(prefixed.MessageFormat).IsEqualTo("{member} too small");
		await Assert.That(prefixed.Parameters!["count"]).IsEqualTo(0);
	}

	[Test]
	public async Task Lower_GivenOnlyIntegerMinimum_FallsBackToMinimum()
	{
		// Act
		var error = ValidationError.Create("too_small", "x", ZodPath.Root, minimum: 3, inclusive: false);

		// Assert
		await Assert.That(error.Lower).IsEqualTo(new ErrorBound(3, false));
		await Assert.That(error.Upper).IsNull();
	}

	[Test]
	public async Task Lower_GivenExplicitBound_PrefersIt()
	{
		// Act
		var error = ValidationError.Create("too_small", "x", ZodPath.Root) with
		{
			Lower = ErrorBound.Excluding(0.5),
		};

		// Assert
		await Assert.That(error.Lower).IsEqualTo(new ErrorBound(0.5, false));
	}

	// Defect A22
	[Test]
	public async Task Merge_GivenDefaultResult_DoesNotThrow()
	{
		// Arrange
		var failure = ValidationResult<string>.Failure(ValidationError.Create("x", "y", ZodPath.Root));

		// Act
		var merged = ValidationResult<string>.Merge(default, failure);

		// Assert
		await Assert.That(merged.IsSuccess).IsFalse();
		await Assert.That(merged.Errors.Length).IsEqualTo(1);
	}

	// Defects O2, O3
	[Test]
	public async Task ZodException_GivenErrors_DescribesIssuesWithDottedPaths()
	{
		// Arrange
		ZodException exception = new([
			ValidationError.Create("too_small", "Too small", ZodPath.ParseDotted("Items[0].Quantity")),
			ValidationError.Create("custom", "Root failed", ZodPath.Root),
		]);

		// Act
		var message = exception.Message;
		var text = exception.ToString();

		// Assert
		await Assert.That(message).Contains("Items[0].Quantity: Too small (too_small)");
		await Assert.That(message).Contains("(root): Root failed (custom)");
		await Assert.That(text).DoesNotContain(".[0]");
	}

	[Test]
	public async Task ZodException_GivenManyErrors_SummarisesTheRest()
	{
		// Arrange
		var errors = Enumerable.Range(0, 8).Select(i => ValidationError.Create("c", $"m{i}", ZodPath.Root));

		// Act
		ZodException exception = new(errors);

		// Assert
		await Assert.That(exception.Message).Contains("and 3 more");
	}

	// Defect B5
	[Test]
	public async Task RuleMessageFormat_GivenCustomMessageWithStrayBraces_DoesNotThrow()
	{
		// Act
		var message = RuleMessage.Format("Use {name} or { here", "value");

		// Assert
		await Assert.That(message).IsEqualTo("Use {name} or { here");
	}

	[Test]
	public async Task RuleMessageFormat_GivenPlaceholderBeyondArguments_DoesNotThrow()
	{
		// Act
		var message = RuleMessage.Format("{0} and {1}", "value");

		// Assert
		await Assert.That(message).IsEqualTo("value and {1}");
	}
}

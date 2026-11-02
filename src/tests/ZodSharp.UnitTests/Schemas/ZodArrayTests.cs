using ZodSharp.Core;

namespace ZodSharp.Schemas;

public class ZodArrayTests
{
	[Test]
	public async Task ArrayMin_GivenEnoughItems_ReturnsSuccess()
	{
		var result = Z.Array(Z.Number()).Min(1).Validate([1.0, 2.0, 3.0]);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ArrayMin_GivenEmptyArray_ReturnsFailure()
	{
		var result = Z.Array(Z.Number()).Min(1).Validate([]);

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ArrayMax_GivenExactlyMaximum_ReturnsSuccess()
	{
		var result = Z.Array(Z.Number()).Max(3).Validate([1.0, 2.0, 3.0]);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ArrayMax_GivenTooManyItems_ReturnsFailure()
	{
		var result = Z.Array(Z.Number()).Max(10).Validate([1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0]);

		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ArrayElementValidation_GivenInvalidElement_ReturnsFailureWithIndexedPath()
	{
		var result = Z.Array(Z.String().Min(2)).Validate(["ok", "x"]);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path).Contains("1");
		await Assert.That(result.Errors[0].FullPath[0]).IsEqualTo(ZodPathSegment.At(1));
	}

	[Test]
	public async Task ArrayMax_GivenOversizedArray_DoesNotValidateEveryElementFirst()
	{
		// Arrange — the length constraint depends only on the array's length, so an array the schema has
		// already declared too long must not be walked element by element. Previously it was: a large
		// array against a small Max ran a validation and allocated a ValidationError per element, with an
		// interpolated index string each, before reporting that it was simply too long.
		var elementValidations = 0;
		var counting = Z.Number()
			.Refine(_ =>
			{
				elementValidations++;

				return true;
			});

		var schema = Z.Array(counting).Max(2);

		// Act
		var result = schema.Validate([1.0, 2.0, 3.0, 4.0, 5.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("too_big");
		await Assert.That(elementValidations).IsEqualTo(0);
	}

	[Test]
	public async Task ArrayMin_GivenUndersizedArray_DoesNotValidateEveryElementFirst()
	{
		// Arrange
		var elementValidations = 0;
		var counting = Z.Number()
			.Refine(_ =>
			{
				elementValidations++;

				return true;
			});

		var schema = Z.Array(counting).Min(5);

		// Act
		var result = schema.Validate([1.0, 2.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("too_small");
		await Assert.That(elementValidations).IsEqualTo(0);
	}

	[Test]
	public async Task ArrayLength_WhenWithinBounds_StillValidatesEveryElement()
	{
		// Reordering the checks must not skip element validation for an acceptable length.
		var elementValidations = 0;
		var counting = Z.Number()
			.Refine(_ =>
			{
				elementValidations++;

				return true;
			});

		var result = Z.Array(counting).Max(10).Validate([1.0, 2.0, 3.0]);

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(elementValidations).IsEqualTo(3);
	}

	[Test]
	public async Task ArrayElementValidation_GivenScalarElement_PathIsExactlyTheIndex()
	{
		// Arrange
		var schema = Z.Array(Z.String().Min(2));

		// Act
		var result = schema.Validate(["ok", "x"]);

		// Assert — the whole path, not just a containment check. A containment assertion cannot
		// distinguish ["1"] from [null, "1"].
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path.SequenceEqual(["1"])).IsTrue();
		await Assert.That(result.Errors[0].FullPath.ToDottedString()).IsEqualTo("[1]");
	}

	[Test]
	public async Task ArrayElementValidation_GivenObjectElement_PrefixesIndexAndKeepsTheFieldName()
	{
		// Arrange — an array of objects is the case that exposes element-path composition: the
		// element schema produces a path of its own, which the index must prefix rather than erase.
		var element = Z.Object().Field("email", Z.String().Min(5)).Build();
		var schema = Z.Array(element);

		// Act
		var result = schema.Validate([
			new Dictionary<string, object?> { ["email"] = "valid@example.com" },
			new Dictionary<string, object?> { ["email"] = "no" },
		]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Path.SequenceEqual(["1", "email"])).IsTrue();
		await Assert.That(result.Errors[0].FullPath.ToDottedString()).IsEqualTo("[1].email");
		await Assert.That(result.Errors[0].Path.All(static segment => segment is not null)).IsTrue();
	}

	[Test]
	public async Task ArrayElementValidation_GivenEmptyArrayWithoutMin_ReturnsSuccess()
	{
		var result = Z.Array(Z.Number()).Validate([]);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task ArrayMin_GivenTooFewItems_ReturnsStructuredTooSmallIssue()
	{
		var result = Z.Array(Z.Number()).Min(2).Validate([1.0]);

		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors[0];
		await Assert.That(error.Code).IsEqualTo("too_small");
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Minimum).IsEqualTo(2);
		await Assert.That(error.Inclusive).IsTrue();
	}

	[Test]
	public async Task ArrayMax_GivenTooManyItems_ReturnsStructuredTooBigIssue()
	{
		var result = Z.Array(Z.Number()).Max(2).Validate([1.0, 2.0, 3.0]);

		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors[0];
		await Assert.That(error.Code).IsEqualTo("too_big");
		await Assert.That(error.Origin).IsEqualTo("array");
		await Assert.That(error.Maximum).IsEqualTo(2);
		await Assert.That(error.Inclusive).IsTrue();
	}

	[Test]
	public async Task ArrayLength_GivenWrongCount_ReturnsStructuredBounds()
	{
		var result = Z.Array(Z.Number()).Length(2).Validate([1.0]);

		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors[0];
		await Assert.That(error.Code).IsEqualTo("too_small");
		await Assert.That(error.Minimum).IsEqualTo(2);
		await Assert.That(error.Maximum).IsEqualTo(2);
	}
}

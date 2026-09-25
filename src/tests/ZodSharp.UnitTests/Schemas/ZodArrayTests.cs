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
		await Assert.That(result.Errors[0].Path).Contains("[1]");
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

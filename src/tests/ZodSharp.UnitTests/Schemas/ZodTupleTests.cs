using ZodSharp.Core;
namespace ZodSharp.Schemas;

public class ZodTupleTests
{
	[Test]
	public async Task Tuple2_GivenValidElements_ReturnsTypedTuple()
	{
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number().Min(0));

		// Act
		var result = schema.Validate(["hello", 42.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value.Item1).IsEqualTo("hello");
		await Assert.That(result.Value.Item2).IsEqualTo(42.0);
	}

	[Test]
	public async Task Tuple2_GivenWrongLength_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number());

		// Act
		var result = schema.Validate(["hello"]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_tuple_length");
	}

	[Test]
	public async Task Tuple2_GivenWrongTypeAtPosition_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number());

		// Act — first element is a number, not a string.
		var result = schema.Validate([42.0, 99.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].FullPath[0]).IsEqualTo(ZodPathSegment.At(0));
	}

	[Test]
	public async Task Tuple2_GivenInvalidValueAtPosition_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Tuple(Z.String().Min(3), Z.Number().Min(0));

		// Act — string too short, number negative.
		var result = schema.Validate(["hi", -1.0]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Length).IsEqualTo(2);
	}

	[Test]
	public async Task Tuple3_GivenValidElements_ReturnsTypedTuple()
	{
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number(), Z.Boolean());

		// Act
		var result = schema.Validate(["hello", 42.0, true]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value.Item1).IsEqualTo("hello");
		await Assert.That(result.Value.Item2).IsEqualTo(42.0);
		await Assert.That(result.Value.Item3).IsTrue();
	}

	[Test]
	public async Task Tuple_GivenNull_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Tuple(Z.String(), Z.Number());

		// Act
		var result = schema.Validate(null!);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_type");
	}
}

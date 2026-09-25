namespace ZodSharp.Schemas;

public class ZodNumberTests
{
	[Test]
	[Arguments(25.0, true)]
	[Arguments(-1.0, false)]
	[Arguments(0.0, true)]
	[Arguments(0.5, true)]
	public async Task NumberMin_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Min(0).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(25.0, true)]
	[Arguments(121.0, false)]
	[Arguments(120.0, true)]
	[Arguments(119.5, true)]
	public async Task NumberMax_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Max(120).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(25.0, true)]
	[Arguments(25.5, false)]
	[Arguments(-25.0, true)]
	public async Task NumberInt_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Int().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task NumberValidate_GivenNaN_ReturnsFailure()
	{
		var result = Z.Number().Validate(double.NaN);

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_type");
	}

	[Test]
	[Arguments(10.0, true)]
	[Arguments(0.1, true)]
	[Arguments(0.0, false)]
	[Arguments(-1.0, false)]
	public async Task NumberPositive_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Positive().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(-5.0, true)]
	[Arguments(-0.1, true)]
	[Arguments(0.0, false)]
	[Arguments(5.0, false)]
	public async Task NumberNegative_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Negative().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(0.0, true)]
	[Arguments(5.0, true)]
	[Arguments(-0.1, false)]
	public async Task NumberNonNegative_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().NonNegative().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(0.0, true)]
	[Arguments(-5.0, true)]
	[Arguments(0.1, false)]
	public async Task NumberNonPositive_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().NonPositive().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(30.0, true)]
	[Arguments(25.0, false)]
	[Arguments(0.0, true)]
	public async Task NumberMultipleOf_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().MultipleOf(10).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(0.3, true)]
	[Arguments(0.3000000001, false)]
	[Arguments(-0.3, true)]
	public async Task NumberMultipleOf_GivenFractionalDivisor_HandlesFloatingPointRounding(double value, bool expected)
	{
		var result = Z.Number().MultipleOf(0.1).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	public async Task NumberMultipleOf_GivenZeroDivisor_ThrowsArgumentException()
	{
		var exception = Assert.Throws<ArgumentException>(static () => Z.Number().MultipleOf(0));

		await Assert.That(exception).IsNotNull();
	}

	[Test]
	[Arguments(double.PositiveInfinity, false)]
	[Arguments(double.NaN, false)]
	public async Task NumberMultipleOf_GivenNonFiniteValue_ReturnsFailure(double value, bool expected)
	{
		var result = Z.Number().MultipleOf(10).Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(42.0, true)]
	[Arguments(-1000.0, true)]
	[Arguments(0.5, true)]
	[Arguments(double.PositiveInfinity, false)]
	[Arguments(double.NegativeInfinity, false)]
	public async Task NumberFinite_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Finite().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}

	[Test]
	[Arguments(2147483647.0, true)]
	[Arguments(2147483648.0, false)]
	[Arguments(-2147483649.0, false)]
	[Arguments(1.5, false)]
	public async Task NumberSafe_GivenValue_ReturnsExpectedResult(double value, bool expected)
	{
		var result = Z.Number().Safe().Validate(value);

		await Assert.That(result.IsSuccess).IsEqualTo(expected);
	}
}

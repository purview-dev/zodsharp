#if NET11_0_OR_GREATER
namespace ZodSharp.Unions;

/// <summary>
/// Coverage for the hand-written native union: the C# 15 language contract (implicit conversions, pattern
/// matching, exhaustiveness) plus the non-boxing access pattern that the declaration exists for.
/// </summary>
public class NativeUnionTests
{
	[Test]
	public async Task NativeUnion_GivenValueTypeCases_PatternMatchesWithoutBoxing(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		NativeUnion<int, bool> warmup = 42;
		_ = Describe(warmup); // Warm the JIT so the measurement covers the conversion and the switch.

		// Act
		var before = GC.GetAllocatedBytesForCurrentThread();
		NativeUnion<int, bool> union = 42; // implicit union conversion runs the case constructor
		var described = Describe(union);
		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// Assert
		await Assert.That(described).IsEqualTo(42);
		await Assert.That(allocated).IsEqualTo(0);

		// Every case type is handled and there is no discard arm, so losing exhaustiveness is a compile error.
		static int Describe(NativeUnion<int, bool> value) =>
			value switch
			{
				int number => number,
				bool flag => flag ? 1 : 0,
			};
	}

	[Test]
	public async Task NativeUnion_GivenSecondCase_TryGetValueAndTagIdentifyTheCase(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		NativeUnion<string, int> union = 7;

		// Act
		var foundInt = union.TryGetValue(out int number);
		var foundString = union.TryGetValue(out string? text);

		// Assert
		await Assert.That(foundInt).IsTrue();
		await Assert.That(number).IsEqualTo(7);
		await Assert.That(foundString).IsFalse();
		await Assert.That(text).IsNull();
		await Assert.That(union.Tag).IsEqualTo(2);
		await Assert.That(union.HasValue).IsTrue();
	}

	[Test]
	public async Task NativeUnion_GivenNullReferenceCase_ReportsNoValue(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		// A default union reached through an array element, the way one reaches a default-initialized field.
		var union = (new NativeUnion<string, int>[1])[0];

		// Act
		var hasValue = union.HasValue;
		var value = union.Value;
		var matchedNull = union is null;

		// Assert
		await Assert.That(hasValue).IsFalse();
		await Assert.That(value).IsNull();
		await Assert.That(matchedNull).IsTrue();
		await Assert.That(union.Tag).IsEqualTo(0);
	}

	[Test]
	public async Task NativeUnion_GivenDefault_HasNoValue(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		NativeUnion<Guid, int> union = default;

		// Act
		var hasValue = union.HasValue;

		// Assert
		await Assert.That(hasValue).IsFalse();
		await Assert.That(union.Value).IsNull();
	}

	[Test]
	public async Task NativeUnionSchema_GivenValueTypeCases_UnwrapsWithoutBoxing(CancellationToken cancellationToken)
	{
		// Arrange
		cancellationToken.ThrowIfCancellationRequested();
		var schema = Z.NativeUnion(Z.Number(), Z.Boolean());

		// Act
		var result = schema.Validate(3d);
		var described = result.IsSuccess
			? result.Value switch
			{
				double number => number,
				bool flag => flag ? 1d : 0d,
			}
			: -1d;

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(described).IsEqualTo(3d);
	}
}
#endif

using ZodSharp.Unions;

namespace ZodSharp.Schemas;

public class UnionTests
{
	[Test]
	public async Task Union_GivenT1Value_CreateStoresT1()
	{
		// Arrange
		var union = Union<string, int>.Create("hello");

		// Act
		var isString = union.TryGetValue(out string? s);
		var isInt = union.TryGetValue(out int _);

		// Assert
		await Assert.That(union.Tag).IsEqualTo(1);
		await Assert.That(isString).IsTrue();
		await Assert.That(s).IsEqualTo("hello");
		await Assert.That(isInt).IsFalse();
	}

	[Test]
	public async Task Union_GivenT2Value_CreateStoresT2()
	{
		// Arrange
		var union = Union<string, int>.Create(42);

		// Act
		var isString = union.TryGetValue(out string? _);
		var isInt = union.TryGetValue(out int i);

		// Assert
		await Assert.That(union.Tag).IsEqualTo(2);
		await Assert.That(isInt).IsTrue();
		await Assert.That(i).IsEqualTo(42);
		await Assert.That(isString).IsFalse();
	}

	[Test]
	public async Task Union_GivenImplicitConversionFromT1_StoresT1()
	{
		// Arrange
		Union<string, int> union = "world";

		// Act & Assert
		await Assert.That(union.Tag).IsEqualTo(1);
		await Assert.That(union.TryGetValue(out string? s)).IsTrue();
		await Assert.That(s).IsEqualTo("world");
	}

	[Test]
	public async Task Union_GivenImplicitConversionFromT2_StoresT2()
	{
		// Arrange
		Union<string, int> union = 99;

		// Act & Assert
		await Assert.That(union.Tag).IsEqualTo(2);
		await Assert.That(union.TryGetValue(out int i)).IsTrue();
		await Assert.That(i).IsEqualTo(99);
	}

	[Test]
	public async Task Union_Match_GivenT1_RunsCase1()
	{
		// Arrange
		Union<string, int> union = "hello";

		// Act
		var result = union.Match(static s => $"str:{s}", static i => $"int:{i}");

		// Assert
		await Assert.That(result).IsEqualTo("str:hello");
	}

	[Test]
	public async Task Union_Match_GivenT2_RunsCase2()
	{
		// Arrange
		Union<string, int> union = 7;

		// Act
		var result = union.Match(static s => $"str:{s}", static i => $"int:{i}");

		// Assert
		await Assert.That(result).IsEqualTo("int:7");
	}

	[Test]
	public async Task Union_Switch_GivenT1_RunsCase1Action()
	{
		// Arrange
		Union<string, int> union = "hello";
		var captured = string.Empty;

		// Act
		union.Switch(static s => { }, static i => { });
		union.Switch(s => captured = $"s:{s}", _ => captured = "i");

		// Assert
		await Assert.That(captured).IsEqualTo("s:hello");
	}

	[Test]
	public async Task Union_GivenEqualValues_AreEqual()
	{
		// Arrange
		Union<string, int> a = "test";
		Union<string, int> b = "test";

		// Act & Assert
		await Assert.That(a == b).IsTrue();
		await Assert.That(a.Equals(b)).IsTrue();
	}

	[Test]
	public async Task Union_GivenDifferentValues_AreNotEqual()
	{
		// Arrange
		Union<string, int> a = "test";
		Union<string, int> b = 42;

		// Act & Assert
		await Assert.That(a != b).IsTrue();
	}

	[Test]
	public async Task Union_GivenT1Value_ValueReturnsT1()
	{
		// Arrange
		Union<string, int> union = "hello";

		// Act & Assert
		await Assert.That(union.Value).IsEqualTo("hello");
	}

	[Test]
	public async Task Union3_GivenT3Value_StoresT3()
	{
		// Arrange
		var union = Union<string, int, bool>.Create(true);

		// Act & Assert
		await Assert.That(union.Tag).IsEqualTo(3);
		await Assert.That(union.TryGetValue(out bool b)).IsTrue();
		await Assert.That(b).IsTrue();
	}

	[Test]
	public async Task Union3_Match_GivenT3_RunsCase3()
	{
		// Arrange
		Union<string, int, bool> union = 42;

		// Act
		var result = union.Match(static s => 0, static i => i, static b => -1);

		// Assert
		await Assert.That(result).IsEqualTo(42);
	}

	[Test]
	public async Task Union_GivenIUnion_CastAndAccessValue()
	{
		// Arrange
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
		IUnion sut = Union<string, int>.Create("hello");
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

		// Act & Assert
		await Assert.That(sut.Tag).IsEqualTo(1);
		await Assert.That(sut.Value).IsEqualTo("hello");
	}
}

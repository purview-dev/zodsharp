#if NET11_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Unions;

namespace ZodSharp.Json;

/// <summary>
/// System.Text.Json reads and writes a <see cref="NativeUnion{T1,T2}"/> transparently, and the classifier
/// applied to it resolves the token shapes .NET 11's default classifier cannot: a string read as a number
/// under <see cref="JsonSerializerDefaults.Web"/>, and two object-shaped cases.
/// </summary>
public class NativeUnionSerializationTests
{
	static JsonSerializerOptions WebOptions() =>
		new(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

	[Test]
	public async Task Deserialize_GivenIntAndStringUnionUnderWebDefaults_SelectsTheStringCase(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — ASP.NET Core's JsonSerializerDefaults.Web lets a number be read from a string, so the
		// platform's token classifier sees a string token claimed by both cases.
		var options = WebOptions();

		// Act
		var union = JsonSerializer.Deserialize<NativeUnion<int, string>>("\"hello\"", options);

		// Assert
		await Assert.That(union.Tag).IsEqualTo(2);
		await Assert.That(union.TryGetValue(out string? value)).IsTrue();
		await Assert.That(value).IsEqualTo("hello");
	}

	[Test]
	public async Task Deserialize_GivenIntAndStringUnionAndNumber_SelectsTheIntCase(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		var options = WebOptions();

		// Act
		var union = JsonSerializer.Deserialize<NativeUnion<int, string>>("42", options);

		// Assert
		await Assert.That(union.Tag).IsEqualTo(1);
		await Assert.That(union.TryGetValue(out int value)).IsTrue();
		await Assert.That(value).IsEqualTo(42);
	}

	[Test]
	public async Task Deserialize_GivenTwoObjectCases_SelectsTheCaseWhoseMembersMatch(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		var options = WebOptions();

		// Act
		var cat = JsonSerializer.Deserialize<NativeUnion<UnionCat, UnionDog>>("""{ "name": "Rex" }""", options);
		var dog = JsonSerializer.Deserialize<NativeUnion<UnionCat, UnionDog>>("""{ "breed": "Husky" }""", options);

		// Assert
		await Assert.That(cat.TryGetValue(out UnionCat? catValue)).IsTrue();
		await Assert.That(catValue!.Name).IsEqualTo("Rex");
		await Assert.That(dog.TryGetValue(out UnionDog? dogValue)).IsTrue();
		await Assert.That(dogValue!.Breed).IsEqualTo("Husky");
	}

	[Test]
	public async Task Deserialize_GivenAmbiguousObjectCases_ThrowsJsonException(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — both cases declare the same JSON member, so no classifier can choose between them.
		var options = WebOptions();

		// Act
		var exception = Assert.Throws<JsonException>(() =>
			JsonSerializer.Deserialize<NativeUnion<AmbiguousCat, AmbiguousDog>>("""{ "name": "Rex" }""", options)
		);

		// Assert
		await Assert.That(exception).IsNotNull();
	}

	[Test]
	public async Task Serialize_GivenNativeUnion_WritesTheActiveCase(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		var options = WebOptions();

		// Act
		var text = JsonSerializer.Serialize<NativeUnion<int, string>>("hello", options);
		var number = JsonSerializer.Serialize<NativeUnion<int, string>>(42, options);

		// Assert — no wrapper or discriminator is written.
		await Assert.That(text).IsEqualTo("\"hello\"");
		await Assert.That(number).IsEqualTo("42");
	}

	[Test]
	public async Task Deserialize_GivenSourceGeneratedMetadata_UsesTheSameClassifier(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — proves the classifier works on the trim- and AOT-safe source-generated path, not only
		// through the reflection resolver.
		var context = NativeUnionJsonContext.Default;

		// Act
		var text = JsonSerializer.Deserialize<NativeUnion<int, string>>("\"hello\"", context.Options);
		var dog = JsonSerializer.Deserialize<NativeUnion<UnionCat, UnionDog>>(
			"""{ "breed": "Husky" }""",
			context.Options
		);

		// Assert
		await Assert.That(text.TryGetValue(out string? value)).IsTrue();
		await Assert.That(value).IsEqualTo("hello");
		await Assert.That(dog.TryGetValue(out UnionDog? dogValue)).IsTrue();
		await Assert.That(dogValue!.Breed).IsEqualTo("Husky");
	}
}

public sealed class UnionCat
{
	public string Name { get; set; } = "";
}

public sealed class UnionDog
{
	public string Breed { get; set; } = "";
}

public sealed class AmbiguousCat
{
	public string Name { get; set; } = "";
}

public sealed class AmbiguousDog
{
	public string Name { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NativeUnion<int, string>))]
[JsonSerializable(typeof(NativeUnion<UnionCat, UnionDog>))]
sealed partial class NativeUnionJsonContext : JsonSerializerContext;
#endif

#if NET11_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Unions;

namespace ZodSharp.Core;

/// <summary>
/// System.Text.Json reports a path in JSON names. A union member is not a member of the union contract, so
/// the segments below it belong to the active case; the mapper resolves them through the case contracts.
/// </summary>
public class ZodJsonExceptionsUnionTests
{
	static JsonTypeInfo<UnionEnvelope> Envelope()
	{
		JsonSerializerOptions options = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
		};

		return options.GetTypeInfo<UnionEnvelope>();
	}

	[Test]
	public async Task ToMemberPath_GivenMemberEveryCaseShares_MapsItToItsMemberName(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var path = ZodJsonExceptions.ToMemberPath(ZodPath.ParseJsonPath("$.pet.name"), Envelope());

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo("Pet.Name");
	}

	[Test]
	public async Task ToMemberPath_GivenMemberOnlyOneCaseDeclares_MapsItToTheMatchingMemberName(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var path = ZodJsonExceptions.ToMemberPath(ZodPath.ParseJsonPath("$.pet.breed"), Envelope());

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo("Pet.Breed");
	}

	[Test]
	public async Task ToMemberPath_GivenUnionCollectionCase_MapsThroughTheElementContract(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var path = ZodJsonExceptions.ToMemberPath(ZodPath.ParseJsonPath("$.pets[0].name"), Envelope());

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo("Pets[0].Name");
	}

	[Test]
	public async Task ToMemberPath_GivenMemberNoCaseDeclares_KeepsTheJsonName(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Act
		var path = ZodJsonExceptions.ToMemberPath(ZodPath.ParseJsonPath("$.pet.unknown"), Envelope());

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo("Pet.unknown");
	}

	public sealed class UnionEnvelope
	{
		public NativeUnion<UnionCat, UnionDog> Pet { get; set; }

		public NativeUnion<List<UnionCat>, string> Pets { get; set; }
	}

	public sealed class UnionCat
	{
		public string Name { get; set; } = "";
	}

	public sealed class UnionDog
	{
		public string Breed { get; set; } = "";
	}
}
#endif

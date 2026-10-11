namespace ZodSharp.Rules;

/// <summary>
/// A <see cref="Guid"/> is a UUID, so the UUID rule validates its bytes directly. This is what lets a
/// <c>[Scalar&lt;Guid&gt;]</c> or a plain <see cref="Guid"/> member carry a UUID-version constraint through the
/// generated <c>[UUID]</c> attribute.
/// </summary>
public class UuidRuleGuidTests
{
	const string Nil = "00000000-0000-0000-0000-000000000000";
	const string Max = "ffffffff-ffff-ffff-ffff-ffffffffffff";
	const string V4 = "550e8400-e29b-41d4-a716-446655440000";
	const string V7 = "0192b4c1-7a9b-7f5e-9a3c-2d4e6f8a0b1c";

	// Version nibble `f` and variant nibble `0`, so it is a valid Guid but not an RFC UUID.
	const string NonRfc = "9f41585f-31c5-fc4b-0d04-b1fe5b523139";

	[Test]
	[Arguments(Nil, true)]
	[Arguments(Max, true)]
	[Arguments(V4, true)]
	[Arguments(V7, true)]
	[Arguments(NonRfc, false)]
	public async Task Versionless_GivenGuid_MatchesTheRfcUuidPredicate(string value, bool expected)
	{
		// Arrange
		UUIDRule rule = new();

		// Act
		var isValid = rule.IsValid(Guid.Parse(value));

		// Assert
		await Assert.That(isValid).IsEqualTo(expected);
	}

	[Test]
	[Arguments(UuidVersion.V4, V4, true)]
	[Arguments(UuidVersion.V4, V7, false)]
	[Arguments(UuidVersion.V4, Nil, false)]
	[Arguments(UuidVersion.V4, NonRfc, false)]
	[Arguments(UuidVersion.V7, V7, true)]
	[Arguments(UuidVersion.V7, V4, false)]
	[Arguments(UuidVersion.V7, NonRfc, false)]
	public async Task Versioned_GivenGuid_RequiresTheDeclaredVersion(UuidVersion version, string value, bool expected)
	{
		// Arrange
		UUIDRule rule = new(version);

		// Act
		var isValid = rule.IsValid(Guid.Parse(value));

		// Assert
		await Assert.That(isValid).IsEqualTo(expected);
	}

	[Test]
	public async Task Versioned_GivenGuid_UsesTheVersionedMessageFormat(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		UUIDRule rule = new(UuidVersion.V4);

		// Act
		var message = rule.GetErrorMessage(Guid.Parse(V7));

		// Assert
		await Assert.That(message).Contains("v4");
		await Assert.That(message).Contains(V7);
	}
}
